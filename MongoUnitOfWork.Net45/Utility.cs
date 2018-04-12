using System.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Data.Entity {
    internal class Utility<TKey> {
        public static string GetConnectionStringFromConfigFile(string name) {
            return ConfigurationManager.ConnectionStrings[name].ConnectionString;
        }

        public static IMongoDatabase GetDatabase(MongoUrl url) {
            var client = new MongoClient(url);
            return client.GetDatabase(url.DatabaseName);
        }

        public static IMongoDatabase GetDatabase(MongoUrl url, string databaseName) {
            var client = new MongoClient(url);
            return client.GetDatabase(databaseName);
        }

        public static IMongoDatabase GetDatabase(string connectionString) {
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            return client.GetDatabase(url.DatabaseName);
        }

        public static IMongoDatabase GetDatabase(string connectionString, string databaseName) {
            var client = new MongoClient(connectionString);
            return client.GetDatabase(databaseName);
        }

        public static IMongoCollection<T> GetCollection<T>(MongoUrl url) where T : IEntity<TKey> {
            var collectionName = GetCollectionName<T>();
            var db = GetDatabase(url);
            return db.GetCollection<T>(collectionName);
        }

        public static IMongoCollection<T> GetCollection<T>(MongoUrl url, string collectionName) where T : IEntity<TKey> {
            var db = GetDatabase(url);
            return db.GetCollection<T>(collectionName);
        }

        public static IMongoCollection<T> GetCollection<T>(string connectionString) where T : IEntity<TKey> {
            var collectionName = GetCollectionName<T>();
            var db = GetDatabase(connectionString);
            return db.GetCollection<T>(collectionName);
        }

        public static IMongoCollection<T> GetCollection<T>(string connectionString, string collectionName) where T : IEntity<TKey> {
            var db = GetDatabase(connectionString);
            return db.GetCollection<T>(collectionName);
        }

        private static string GetCollectionName<T>() where T : IEntity<TKey> {
            return typeof(T).Name;
        }
    }

    internal class Utility {
        public static void SetId(object obj) {
            var id = obj.GetType().GetProperty(Constants.ID);
            if (id.PropertyType == typeof(ObjectId)) {
                if ((ObjectId)id.GetValue(obj) == default(ObjectId)) {
                    id.SetValue(obj, ObjectId.GenerateNewId());
                }
            }
        }
    }
}
