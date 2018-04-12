namespace Mongo.Data.Entity {
    internal interface ICachingProvider {
        void AddItem(string key, object value);
        object GetItem(string key);
        void RemoveItem(string key);
    }
}
