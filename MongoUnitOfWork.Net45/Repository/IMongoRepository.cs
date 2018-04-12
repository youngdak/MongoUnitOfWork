using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Data.Entity {
    public interface IMongoRepository<T, TKey> where T : class, IEntity<TKey> {
        IMongoCollection<T> Collection { get; }
        IFindFluent<T, T> Get();
        IEnumerable<T> GetCachedData();
        T Get(TKey id);
        void Add(T entity);
        void Add(IEnumerable<T> entities);
        void Update(T entity);
        void Update(IEnumerable<T> entities);
        void Delete(T entity);
        void Delete(IEnumerable<T> entities);
    }
        
    public interface IMongoRepository<T> : IMongoRepository<T, ObjectId> where T : class, IEntity<ObjectId> {}
}
