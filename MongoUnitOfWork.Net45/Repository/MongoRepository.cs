using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Data.Entity {
    public class MongoRepository<T, TKey> : NotificationBase, IMongoRepository<T, TKey> where T : class, IEntity<TKey> {
        public MongoRepository() {
            this.connectionString = Utility<TKey>.GetConnectionStringFromConfigFile(Constants.DEFAULTCONNECTION);
            this.MongoCollection = Utility<TKey>.GetCollection<T>(this.connectionString);
        }

        public MongoRepository(string name) {
            var conString = Utility<TKey>.GetConnectionStringFromConfigFile(name);
            this.connectionString = conString;
            this.MongoCollection = Utility<TKey>.GetCollection<T>(conString);
        }

        public MongoRepository(string connectionString, string collectionName) {
            this.MongoCollection = Utility<TKey>.GetCollection<T>(connectionString, collectionName);
            this.connectionString = connectionString;
        }

        public MongoRepository(MongoUrl url) {
            this.connectionString = url.Url;
            this.MongoCollection = Utility<TKey>.GetCollection<T>(url);
        }

        public MongoRepository(MongoUrl url, string collectionName) {
            this.MongoCollection = Utility<TKey>.GetCollection<T>(url, collectionName);
        }

        internal bool HasChanges {
            get {
                return this.insertedEntities.Count > 0 || this.updatedEntities.Count > 0 || this.deletedEntities.Count > 0;
            }
        }

        public IMongoCollection<T> Collection {
            get { return this.MongoCollection; }
        }

        public IFindFluent<T, T> Get() {
            return this.MongoCollection.Find(new BsonDocument());
        }

        public virtual IEnumerable<T> GetCachedData() {
            var currentCached = GlobalCachingProvider.Instance.GetItem(this.Collection.CollectionNamespace.CollectionName, false) as IEnumerable<T>;
            if (currentCached == null) {
                currentCached = Collection.Find(new BsonDocument()).ToEnumerable();
                GlobalCachingProvider.Instance.AddItem(this.CollectionName, currentCached);
            }

            return currentCached;
        }

        public string CollectionName {
            get {
                return this.MongoCollection.CollectionNamespace.CollectionName;
            }
        }

        public T Get(TKey id) {
            return this.MongoCollection.Find(Builders<T>.Filter.Eq(Constants._ID, id)).FirstOrDefault();
        }

        public void Add(T entity) {
            var hasChanges = this.HasChanges;
            if (this.DeletedEntities.Contains(entity)) return;
            else if (this.UpdatedEntities.Contains(entity)) return;

            this.InsertedEntities.Add(entity);
            this.RaiseHasChangesProperty(hasChanges);
        }

        public void Add(IEnumerable<T> entities) {
            foreach (var entity in entities) {
                this.Add(entity);
            }
        }

        public void Update(T entity) {
            var hasChanges = this.HasChanges;
            if (!this.InsertedEntities.Contains(entity) && !this.DeletedEntities.Contains(entity)) {
                this.UpdatedEntities.Add(entity);
            }

            this.RaiseHasChangesProperty(hasChanges);
        }

        public void Update(IEnumerable<T> entities) {
            foreach (var entity in entities) {
                this.Update(entity);
            }
        }

        public void Delete(T entity) {
            var hasChanges = this.HasChanges;
            this.UpdatedEntities.Remove(entity);
            var insertedEntity = this.InsertedEntities.Remove(entity);

            if (!insertedEntity) {
                this.DeletedEntities.Add(entity);
            }

            this.RaiseHasChangesProperty(hasChanges);
        }

        public void Delete(IEnumerable<T> entities) {
            foreach (var entity in entities) {
                this.Delete(entity);
            }
        }

        internal void Clear() {
            var hasChanges = this.HasChanges;
            this.InsertedEntities.Clear();
            this.UpdatedEntities.Clear();
            this.DeletedEntities.Clear();
            this.RaiseHasChangesProperty(hasChanges);
        }

        [CRUD(State.Inserted)]
        internal HashSet<T> InsertedEntities {
            get { return this.insertedEntities; }
        }

        [CRUD(State.Updated)]
        internal HashSet<T> UpdatedEntities {
            get { return this.updatedEntities; }
        }

        [CRUD(State.Deleted)]
        internal HashSet<T> DeletedEntities {
            get { return this.deletedEntities; }
        }

        protected IMongoCollection<T> MongoCollection;
        protected string connectionString = string.Empty;

        private readonly HashSet<T> insertedEntities = new HashSet<T>();
        private readonly HashSet<T> updatedEntities = new HashSet<T>();
        private readonly HashSet<T> deletedEntities = new HashSet<T>();

        private void RaiseHasChangesProperty(bool hasChanges) {
            if (hasChanges != this.HasChanges) {
                this.OnPropertyChanged(() => this.HasChanges);
            }
        }
    }

    public class MongoRepository<T> : MongoRepository<T, ObjectId>, IMongoRepository<T> where T : class, IEntity<ObjectId> {
        public MongoRepository() {
        }

        public MongoRepository(MongoUrl url)
            : base(url) {
        }

        public MongoRepository(MongoUrl url, string collectionName)
            : base(url, collectionName) {
        }

        public MongoRepository(string name)
            : base(name) {
        }

        public MongoRepository(string connectionString, string collectionName)
            : base(connectionString, collectionName) {
        }
    }
}
