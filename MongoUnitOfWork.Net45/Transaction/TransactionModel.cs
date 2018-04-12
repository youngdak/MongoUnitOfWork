using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Mongo.Data.Entity {
    internal class TransactionModel {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public object ItemId { get; set; }
        public BsonDocument OldValue { get; set; }
        public BsonDocument NewValue { get; set; }
        public string State { get; set; }
        public string Type { get; set; }

        [BsonIgnore]
        public IMongoCollection<BsonDocument> Collection { get; set; }
    }

    internal class TransactionCollectionModel {
        public IEnumerable<object> Items { get; set; }
        public IMongoCollection<BsonDocument> Collection { get; set; }
    }
}
