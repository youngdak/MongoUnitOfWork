using System;
namespace Mongo.Data.Entity {
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class Collection : Attribute {
        public Collection(string name = "", string connectionName = "") {
            this.name = name;
            this.connectionName = connectionName;
        }

        public string Name { get { return this.name; } }
        public string ConnectionName { get { return this.connectionName; } }

        private string name;
        private string connectionName;
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    internal class CRUD : Attribute {
        public CRUD(State state) {
            this.state = state;
        }

        public State State { get { return this.state; } }
        private State state;
    }

    internal enum State {
        Inserted,
        Updated,
        Deleted
    }
}
