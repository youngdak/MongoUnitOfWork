using System;
using System.Collections.Generic;

namespace Mongo.Data.Entity {
    public class ActionResult : Exception {
        public bool HasError { get; set; }
        public int InsertedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }
        public IDictionary<string, ErrorResult> ErrorResults { get; set; }
        
        public ActionResult(string errorMessage):base(errorMessage) {
            this.ErrorResults = new Dictionary<string, ErrorResult>();
        }
    }

    public class ErrorResult {
        public int InsertedErrorCount { get; set; }
        public int UpdatedErrorCount { get; set; }
        public int DeletedErrorCount { get; set; }

        public IEnumerable<object> InsertedErrors { get; set; }
        public IEnumerable<object> UpdatedErrors { get; set; }
        public IEnumerable<object> DeletedErrors { get; set; }

        public ErrorResult() {
            this.InsertedErrors = new List<object>();
            this.UpdatedErrors = new List<object>();
            this.DeletedErrors = new List<object>();
        }
    }
}
