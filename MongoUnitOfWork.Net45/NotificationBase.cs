using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Mongo.Data.Entity {
    public abstract class NotificationBase : INotifyPropertyChanged {

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = "") {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        protected void OnPropertyChanged(Expression<Func<object>> expression) {
            var name = GetPropertyName(expression);
            this.OnPropertyChanged(name);
        }

        private string GetPropertyName(Expression<Func<object>> action) {
            var expressionBody = action.Body;
            if (expressionBody.NodeType == ExpressionType.Convert) {
                var unaryExpression = (UnaryExpression)expressionBody;
                return ((MemberExpression)unaryExpression.Operand).Member.Name;
            }
            var expression = (MemberExpression)expressionBody;
            return expression.Member.Name;
        }
    }
}
