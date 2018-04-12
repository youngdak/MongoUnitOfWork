using System;
using System.Runtime.Caching;
namespace Mongo.Data.Entity {
    internal class CachingProviderBase {
        private static readonly object padlock = new object();
        protected MemoryCache cache = new MemoryCache("CachingProvider");
        protected virtual void AddItem(string key, object value) {
            lock (padlock) {
                cache.Add(key, value, DateTimeOffset.MaxValue);
            }
        }

        protected virtual void RemoveItem(string key) {
            lock (padlock) {
                cache.Remove(key);
            }
        }

        protected virtual object GetItem(string key, bool remove) {
            lock (padlock) {
                var res = cache[key];

                if (res != null) {
                    if (remove == true)
                        cache.Remove(key);
                }

                return res;
            }
        }
    }
}
