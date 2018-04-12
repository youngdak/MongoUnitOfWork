using System;
using System.Collections.Generic;
using System.Linq.Expressions;
namespace Mongo.Data.Entity {
    public interface IExpression {
        Action<Expression, Func<List<object>, List<object>>> Action { get; set; }
        void Evaluate(Expression expression, Func<List<object>, List<object>> func, ref bool canGetValue, ref List<object> output, string input = "");
    }

    public class CustomCallExpression : IExpression {
        public Action<Expression, Func<List<object>, List<object>>> Action { get; set; }
        public void Evaluate(Expression expression, Func<List<object>, List<object>> func, ref bool canGetValue, ref List<object> output, string input = "") {
            var exp = (MethodCallExpression)expression;
            if (exp.Method.Name.Equals(Constants.CONTAINS)) {
                Action(exp.Object, func);
                output.Add(Constants.REGEX);
                canGetValue = true;
                Action(exp.Arguments[0], func);
                output = output == null || output.Count == 0 ? new List<object>() : func(output);
            }else {
                var value = Expression.Lambda(exp).Compile().DynamicInvoke();
                output.Add(value);
                canGetValue = false;
            }
        }
    }

    internal class CustomMemberExpression : IExpression {
        public Action<Expression, Func<List<object>, List<object>>> Action { get; set; }
        public void Evaluate(Expression expression, Func<List<object>, List<object>> func, ref bool canGetValue, ref List<object> output, string input = "") {
            var member = (MemberExpression)expression;
            if (canGetValue) {
                var value = Expression.Lambda(member).Compile().DynamicInvoke();
                output.Add(value);
                canGetValue = false;
            } else {
                output.Add(member.Member.Name);
            }
        }
    }

    internal class CustomConstantExpression : IExpression {
        public Action<Expression, Func<List<object>, List<object>>> Action { get; set; }
        public void Evaluate(Expression expression, Func<List<object>, List<object>> func, ref bool canGetValue, ref List<object> output, string input = "") {
            var constant = ((ConstantExpression)expression).Value;
            output.Add(constant);
            canGetValue = false;
        }
    }

    internal class CustomOtherExpression : IExpression {
        public Action<Expression, Func<List<object>, List<object>>> Action { get; set; }
        public void Evaluate(Expression expression, Func<List<object>, List<object>> func, ref bool canGetValue, ref List<object> output, string input = "") {
            var binaryExpression = (BinaryExpression)expression;
            Action(binaryExpression.Left, func);
            output.Add(input);
            canGetValue = true;
            Action(binaryExpression.Right, func);
            output = output == null || output.Count == 0 ? new List<object>() : func(output);
        }
    }

    internal class ExpressionUtil {
        private static IExpression _exp;
        private static bool _canGetValue;
        private static List<object> _output1 = new List<object>();

        public static void GenerateExpression(Expression expression, Func<List<object>, List<object>> func) {
            BinaryExpression binaryExpression;
            if (expression.NodeType == ExpressionType.MemberAccess) {
                _exp = new CustomMemberExpression();
                _exp.Evaluate(expression, func, ref _canGetValue, ref _output1);
            } else if (expression.NodeType == ExpressionType.Constant) {
                _exp = new CustomConstantExpression();
                _exp.Evaluate(expression, func, ref _canGetValue, ref _output1);
            } else if (expression.NodeType == ExpressionType.Call) {
                _exp = new CustomCallExpression { Action = GenerateExpression };
                _exp.Evaluate(expression, func, ref _canGetValue, ref _output1);
            } else {
                _canGetValue = false;

                binaryExpression = (BinaryExpression)expression;
                switch (binaryExpression.NodeType) {
                    case ExpressionType.And:
                    case ExpressionType.AndAlso:
                        Other(expression, func, Constants.AND);
                        break;
                    case ExpressionType.Equal:
                        Other(expression, func, Constants.EQUAL);
                        break;
                    case ExpressionType.GreaterThan:
                        Other(expression, func, Constants.GT);
                        break;
                    case ExpressionType.GreaterThanOrEqual:
                        Other(expression, func, Constants.GTE);
                        break;
                    case ExpressionType.LessThan:
                        Other(expression, func, Constants.LT);
                        break;
                    case ExpressionType.LessThanOrEqual:
                        Other(expression, func, Constants.LTE);
                        break;
                    case ExpressionType.NotEqual:
                        Other(expression, func, Constants.NOTEQUAL);
                        break;
                    case ExpressionType.Or:
                    case ExpressionType.OrElse:
                        Other(expression, func, Constants.OR);
                        break;
                    default:
                        break;
                }
            }
        }

        private static void Other(Expression expression, Func<List<object>, List<object>> func, string input) {
            _exp = new CustomOtherExpression() {
                Action = GenerateExpression
            };
            _exp.Evaluate(expression, func, ref _canGetValue, ref _output1, input);
        }
    }
}
