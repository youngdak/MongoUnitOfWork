using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq;

namespace Mongo.Data.Entity {
    internal class Transaction {
        public Transaction(string connectionName, Tuple<IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>> transactionModels) {
            var connectionString = Utility<BsonDocument>.GetConnectionStringFromConfigFile(connectionName);
            this.database = Utility<BsonDocument>.GetDatabase(connectionString);
            this.collection = database.GetCollection<TransactionModel>(Constants.TRANSACTION);
            this.transactionModels = transactionModels;
        }

        public void StartTransaction() {
            var insertedTransactions = this.InsertTransaction();
            var pendingTransactions = this.UpdateTransactionStateToPending(insertedTransactions);
            this.ApplyTransaction(pendingTransactions);
            this.RemoveAppliedTransaction(pendingTransactions);
        }

        private IList<TransactionModel> InsertTransaction() {
            var transactionItems = new List<TransactionModel>();
            transactionItems.AddRange(this.InsertNewItems());
            transactionItems.AddRange(this.InsertModifiedItems());
            transactionItems.AddRange(this.InsertRemovedItems());

            return transactionItems;
        }

        private IList<TransactionModel> InsertNewItems() {
            var newItems = new List<TransactionModel>();
            foreach (var transactionModel in this.transactionModels.Item1) {
                foreach (var newItem in transactionModel.Items) {
                    Utility.SetId(newItem);
                    var bson = newItem.ToBsonDocument(newItem.GetType());
                    var transaction = new TransactionModel {
                        ItemId = bson.GetValue(Constants._ID),
                        Name = transactionModel.Collection.CollectionNamespace.CollectionName,
                        NewValue = newItem.ToBsonDocument(newItem.GetType()),
                        State = TransactionState.Initial.ToString(),
                        Type = TransactionType.Insert.ToString(),
                        Collection = transactionModel.Collection
                    };

                    newItems.Add(transaction);
                }
            }

            try {
                collection.InsertMany(newItems);
            } catch (Exception ex) {
            }

            return newItems;
        }

        private IList<TransactionModel> InsertModifiedItems() {
            var modifiedItems = new List<TransactionModel>();
            foreach (var transactionModel in this.transactionModels.Item2) {
                foreach (var modifiedItem in transactionModel.Items) {
                    var bson = modifiedItem.ToBsonDocument(modifiedItem.GetType());
                    var oldValue = transactionModel.Collection.Find(Builders<BsonDocument>.Filter.Eq(Constants._ID, bson.GetValue(Constants._ID).AsString)).FirstOrDefault();
                    var transaction = new TransactionModel {
                        ItemId = bson.GetValue(Constants._ID),
                        Name = transactionModel.Collection.CollectionNamespace.CollectionName,
                        OldValue = oldValue,
                        NewValue = modifiedItem.ToBsonDocument(modifiedItem.GetType()),
                        State = TransactionState.Initial.ToString(),
                        Type = TransactionType.Update.ToString(),
                        Collection = transactionModel.Collection
                    };

                    modifiedItems.Add(transaction);
                }
            }

            try {
                collection.InsertMany(modifiedItems);
            } catch (Exception ex) {
            }

            return modifiedItems;
        }

        private IList<TransactionModel> InsertRemovedItems() {
            var removedItems = new List<TransactionModel>();
            foreach (var transactionModel in this.transactionModels.Item3) {
                foreach (var removedItem in transactionModel.Items) {
                    var bson = removedItem.ToBsonDocument(removedItem.GetType());
                    var oldValue = transactionModel.Collection.Find(Builders<BsonDocument>.Filter.Eq(Constants._ID, bson.GetValue(Constants._ID).AsString)).FirstOrDefault();
                    var transaction = new TransactionModel {
                        ItemId = bson.GetValue(Constants._ID),
                        Name = transactionModel.Collection.CollectionNamespace.CollectionName,
                        OldValue = oldValue,
                        State = TransactionState.Initial.ToString(),
                        Type = TransactionType.Delete.ToString(),
                        Collection = transactionModel.Collection
                    };

                    removedItems.Add(transaction);
                }
            }

            try {
                collection.InsertMany(removedItems);
            } catch (Exception ex) {
            }

            return removedItems;
        }

        private IList<TransactionModel> UpdateTransactionStateToPending(IList<TransactionModel> transactions) {
            foreach (var transaction in transactions) {
                transaction.State = TransactionState.Pending.ToString();
                var filter = Builders<TransactionModel>.Filter.Eq(x => x.Id, transaction.Id) & Builders<TransactionModel>.Filter.Eq(x => x.State, TransactionState.Initial.ToString());
                try {
                    this.collection.ReplaceOne(filter, transaction);
                } catch (Exception ex) {
                    throw ex;
                }
            }

            return transactions;
        }

        private void ApplyTransaction(IList<TransactionModel> transactions) {
            var insertType = transactions.Where(x => x.Type == TransactionType.Insert.ToString());
            var updateType = transactions.Where(x => x.Type == TransactionType.Update.ToString());
            var deleteType = transactions.Where(x => x.Type == TransactionType.Delete.ToString());

            foreach (var insert in insertType) {
                try {
                    insert.Collection.InsertOne(insert.NewValue);
                } catch (Exception ex) {
                    this.RollBackInsertedItems(insertType);
                    return;
                }
            }

            foreach (var update in updateType) {
                try {
                    var filter = Builders<BsonDocument>.Filter.Eq(Constants._ID, update.Id);
                    update.Collection.ReplaceOne(filter, update.NewValue);
                } catch (Exception ex) {
                    this.RollBackInsertedItems(insertType);
                    this.RollBackModifiedItems(updateType);
                    return;
                }
            }

            foreach (var delete in deleteType) {
                try {
                    var filter = Builders<BsonDocument>.Filter.Eq(Constants._ID, delete.Id);
                    delete.Collection.DeleteOne(filter);
                } catch (Exception ex) {
                    this.RollBackInsertedItems(insertType);
                    this.RollBackModifiedItems(updateType);
                    this.RollBackDeletedItems(deleteType);
                    return;
                }
            }
        }

        private void RollBackInsertedItems(IEnumerable<TransactionModel> transactions) {
            foreach (var transaction in transactions) {
                try {
                    var filter = Builders<BsonDocument>.Filter.Eq(Constants._ID, transaction.NewValue.GetValue(Constants._ID));
                    var result = transaction.Collection.DeleteOne(filter);
                } catch (Exception ex) {
                }
            }
        }

        private void RollBackModifiedItems(IEnumerable<TransactionModel> transactions) {
            foreach (var transaction in transactions) {
                try {
                    var filter = Builders<BsonDocument>.Filter.Eq(Constants._ID, transaction.NewValue.GetValue(Constants._ID));
                    transaction.Collection.ReplaceOne(filter, transaction.OldValue);
                } catch (Exception ex) {
                }
            }
        }

        private void RollBackDeletedItems(IEnumerable<TransactionModel> transactions) {
            foreach (var transaction in transactions) {
                try {
                    transaction.Collection.InsertOne(transaction.OldValue);
                } catch (Exception ex) {
                }
            }
        }

        private void RemoveAppliedTransaction(IList<TransactionModel> transactions) {
            foreach (var transaction in transactions) {
                var filter = Builders<TransactionModel>.Filter.Eq(x => x.Id, transaction.Id) & Builders<TransactionModel>.Filter.Eq(x => x.State, TransactionState.Pending.ToString());
                try {
                    this.collection.DeleteOne(filter);
                } catch (Exception ex) {
                    throw ex;
                }
            }
        }


        private readonly Tuple<IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>> transactionModels;
        private readonly IMongoCollection<TransactionModel> collection;
        private readonly IMongoDatabase database;
    }
}
