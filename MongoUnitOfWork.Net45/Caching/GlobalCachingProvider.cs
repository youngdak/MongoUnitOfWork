namespace Mongo.Data.Entity {
    internal class GlobalCachingProvider : CachingProviderBase, ICachingProvider {
        #region Singelton (inheriting enabled)

        protected GlobalCachingProvider() {

        }

        public static GlobalCachingProvider Instance {
            get {
                return Nested.instance;
            }
        }

        class Nested {
            static Nested() {
            }

            internal static readonly GlobalCachingProvider instance = new GlobalCachingProvider();
        }

        #endregion

        #region ICaching Provider
        public new void AddItem(string key, object value) {
            base.AddItem(key, value);
        }

        public object GetItem(string key) {
            return base.GetItem(key, true);
        }

        public new object GetItem(string key, bool remove) {
            return base.GetItem(key, remove);
        }

        public new void RemoveItem(string key) {
            base.RemoveItem(key);
        }
        #endregion
    }
}
