using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Data.Entity {
    public abstract class MongoContext {
        protected MongoContext() {
            this.Initialize();
        }

        public MongoContext(string connectionName) {
            this.connectionName = connectionName;
            this.Initialize();
        }

        public void Commit(bool enableTransaction = false) {
            if (enableTransaction)
                this.CommitWithTransaction();
            else
                this.CommitWithoutTransaction();
        }

        private void Initialize() {
            this.mongoRepositoryPropertyInfos = this.GetType().GetProperties().Where(x => x.PropertyType.IsGenericType && x.PropertyType.GetInterfaces().FirstOrDefault(y => y.GUID == iMongoType.GUID) != null);
            this.InitializeMongoRepositoryProperties();
        }

        private void InitializeMongoRepositoryProperties() {
            foreach (var mongoRepositoryPropertyInfo in mongoRepositoryPropertyInfos) {
                var connection = this.Connection(mongoRepositoryPropertyInfo);
                var instance = Activator.CreateInstance(mongoRepositoryPropertyInfo.PropertyType, connection.ConnectionString, connection.CollectionName);
                mongoRepositoryPropertyInfo.SetValue(this, instance, null);
            }
        }

        private Connection Connection(PropertyInfo propertyInfo) {
            var collectionAttribute = propertyInfo.GetCustomAttribute(typeof(Collection)) as Collection;
            var connectionName = this.connectionName;
            var collectionName = propertyInfo.Name;

            if (collectionAttribute != null) {
                if (!string.IsNullOrEmpty(collectionAttribute.ConnectionName))
                    connectionName = collectionAttribute.ConnectionName;
                if (!string.IsNullOrEmpty(collectionAttribute.Name))
                    collectionName = collectionAttribute.Name;
            }

            var connectionString = Utility<BsonDocument>.GetConnectionStringFromConfigFile(connectionName);
            return new Connection {
                CollectionName = collectionName,
                ConnectionString = connectionString
            };
        }

        private void CommitWithTransaction() {
            var transactionModels = this.TransactionModels();
            var transaction = new Transaction(this.connectionName, transactionModels);
            transaction.StartTransaction();
        }

        private Tuple<IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>> TransactionModels() {
            var newItems = new List<TransactionCollectionModel>();
            var modifiedItems = new List<TransactionCollectionModel>();
            var removedItems = new List<TransactionCollectionModel>();

            foreach (var mongoRepositoryPropertyInfo in mongoRepositoryPropertyInfos) {
                var mongoRepositoryProperty = mongoRepositoryPropertyInfo.GetValue(this);

                var connection = this.Connection(mongoRepositoryPropertyInfo);
                var database = Utility<BsonDocument>.GetDatabase(connection.ConnectionString);
                var collection = database.GetCollection<BsonDocument>(connection.CollectionName);

                var crudItems = this.GetCrudItems(mongoRepositoryProperty);
                if (crudItems.Item1.Count() > 0)
                    newItems.Add(new TransactionCollectionModel { Items = crudItems.Item1, Collection = collection });

                if (crudItems.Item2.Count() > 0)
                    modifiedItems.Add(new TransactionCollectionModel { Items = crudItems.Item2, Collection = collection });

                if (crudItems.Item3.Count() > 0)
                    removedItems.Add(new TransactionCollectionModel { Items = crudItems.Item3, Collection = collection });

                mongoRepositoryProperty.GetType().GetMethod(Constants.CLEAR, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mongoRepositoryProperty, null);
            }

            var transactionModels = new Tuple<IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>, IEnumerable<TransactionCollectionModel>>(newItems, modifiedItems, removedItems);
            return transactionModels;
        }

        private void CommitWithoutTransaction() {
            var errorResults = new Dictionary<string, ErrorResult>();
            int insertedCount = 0, updatedCount = 0, deletedCount = 0;
            foreach (var mongoRepositoryPropertyInfo in mongoRepositoryPropertyInfos) {
                var mongoRepositoryProperty = mongoRepositoryPropertyInfo.GetValue(this);

                var connection = this.Connection(mongoRepositoryPropertyInfo);
                var database = Utility<BsonDocument>.GetDatabase(connection.ConnectionString);
                var collection = database.GetCollection<BsonDocument>(connection.CollectionName);

                var crud = this.GetCrudItems(mongoRepositoryProperty);

                var insertedResult = this.InsertItems(collection, crud.Item1);
                var updatedResult = this.UpdateItems(collection, crud.Item2);
                var deletedResult = this.DeleteItems(collection, crud.Item3);

                mongoRepositoryProperty.GetType().GetMethod(Constants.CLEAR, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mongoRepositoryProperty, null);

                if (insertedResult.Item1.Count == 0 && updatedResult.Item1.Count == 0 && deletedResult.Item1.Count == 0) continue;

                insertedCount += insertedResult.Item2;
                updatedCount += updatedResult.Item2;
                deletedCount += deletedResult.Item2;

                CommitErrorResult(insertedResult.Item1, updatedResult.Item1, deletedResult.Item1, connection.CollectionName, errorResults);
            }

            if (errorResults.Count > 0) {
                throw new ActionResult(null) {
                    ErrorResults = errorResults,
                    InsertedCount = insertedCount,
                    UpdatedCount = updatedCount,
                    DeletedCount = deletedCount,
                    HasError = errorResults.Count > 0
                };
            }
        }

        private Tuple<IEnumerable<object>, IEnumerable<object>, IEnumerable<object>> GetCrudItems(object mongoRepositoryProperty) {
            var crud = mongoRepositoryProperty.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic).Where(x => Attribute.IsDefined(x, typeof(CRUD)));
            var insertedItems = crud.ElementAt(0).GetValue(mongoRepositoryProperty) as IEnumerable<object>;
            var updatedItems = crud.ElementAt(1).GetValue(mongoRepositoryProperty) as IEnumerable<object>;
            var deletedItems = crud.ElementAt(2).GetValue(mongoRepositoryProperty) as IEnumerable<object>;

            return new Tuple<IEnumerable<object>, IEnumerable<object>, IEnumerable<object>>(insertedItems.ToList(), updatedItems.ToList(), deletedItems.ToList());
        }

        private Tuple<IList<object>, int> InsertItems(IMongoCollection<BsonDocument> collection, IEnumerable<object> items) {
            var insertedErrors = new List<object>();
            var insertedCount = 0;
            if (items.Count() > 0) {
                var bsons = items.Select(x => {
                    Utility.SetId(x);
                    return x.ToBsonDocument(x.GetType());
                });
                try {
                    collection.InsertMany(bsons, new InsertManyOptions { IsOrdered = false });
                } catch (Exception ex) {
                    var insertItems = bsons.ToArray();
                    var exception = ex as MongoBulkWriteException;
                    foreach (var error in exception.WriteErrors) {
                        insertedErrors.Add(insertItems[error.Index]);
                    }
                    insertedCount = items.Count() - exception.WriteErrors.Count;
                }
            }

            return new Tuple<IList<object>, int>(insertedErrors, insertedCount);
        }

        private Tuple<IList<object>, int> UpdateItems(IMongoCollection<BsonDocument> collection, IEnumerable<object> items) {
            var updatedErrors = new List<object>();
            var updatedCount = 0;
            foreach (var updatedItem in items) {
                var bson = updatedItem.ToBsonDocument(updatedItem.GetType());
                var filter = Builders<BsonDocument>.Filter.Eq(Constants._ID, bson.GetValue(Constants._ID));
                try {
                    var result = collection.ReplaceOne(filter, bson);
                    updatedCount += (int)result.ModifiedCount;
                } catch (Exception) {
                    updatedErrors.Add(updatedItem);
                }
            }

            return new Tuple<IList<object>, int>(updatedErrors, updatedCount);
        }

        private Tuple<IList<object>, int> DeleteItems(IMongoCollection<BsonDocument> collection, IEnumerable<object> items) {
            var deletedErrors = new List<object>();
            var deletedCount = 0;
            foreach (var updatedItem in items) {
                var bson = updatedItem.ToBsonDocument(updatedItem.GetType());
                var filter = Builders<BsonDocument>.Filter.Eq(Constants._ID, bson.GetValue(Constants._ID));
                try {
                    var result = collection.DeleteOne(filter);
                    deletedCount += (int)result.DeletedCount;
                } catch (Exception) {
                    deletedErrors.Add(updatedItem);
                }
            }

            return new Tuple<IList<object>, int>(deletedErrors, deletedCount);
        }

        private void CommitErrorResult(IList<object> insertedResult, IList<object> updatedResult, IList<object> deletedResult, string collectionName, IDictionary<string, ErrorResult> errorResults) {
            var errorResult = new ErrorResult {
                InsertedErrors = insertedResult,
                UpdatedErrors = updatedResult,
                DeletedErrors = deletedResult,
                InsertedErrorCount = insertedResult.Count,
                UpdatedErrorCount = updatedResult.Count,
                DeletedErrorCount = deletedResult.Count
            };

            errorResults.Add(collectionName, errorResult);
        }

        private readonly Type iMongoType = typeof(IMongoRepository<,>);
        private readonly string connectionName = Constants.DEFAULTCONNECTION;
        private IEnumerable<PropertyInfo> mongoRepositoryPropertyInfos;
    }
}
