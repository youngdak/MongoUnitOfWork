using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mongo.Data.Entity {
    public interface IEntity<TKey> {
        [BsonId]
        TKey Id { get; set; }
    }

    public abstract class EntityBase : IEntity<ObjectId> {
        public ObjectId Id { get; set; }
    }
}
