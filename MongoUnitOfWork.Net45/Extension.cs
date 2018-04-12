using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Mongo.Data.Entity {
    public static class ExtensionMethod {
        public static IFindFluent<T, T> Get<T>(this IMongoCollection<T> source) {
            return source.Find(new BsonDocument());
        }

        public static IFindFluent<T, T> Get<T>(this IMongoCollection<T> source, Expression<Func<T, bool>> expression) {
            var expressionBody = expression.Body;
            FilterDefinition<T> filter = null;

            ExpressionUtil.GenerateExpression(expressionBody, (p) => {
                filter = FindFilterDefinition<T>(p, filter);
                return new List<object>();
            });

            return source.Find(filter);
        }

        public static IFindFluent<T, T> Include<T>(this IFindFluent<T, T> source, params Expression<Func<T, object>>[] include) {
            var propertyNames = GetPropertyNames(include);
            var pro = Builders<T>.Projection.Include(propertyNames[0]);
            for (int i = 1; i < propertyNames.Length; i++) {
                pro = pro.Include(propertyNames[i]);
            }
            return source.Project<T>(pro);
        }

        public static IFindFluent<T, T> Exclude<T>(this IFindFluent<T, T> source, params Expression<Func<T, object>>[] exclude) {
            var propertyNames = GetPropertyNames(exclude);
            var pro = Builders<T>.Projection.Exclude(propertyNames[0]);
            for (int i = 1; i < propertyNames.Length; i++) {
                pro = pro.Exclude(propertyNames[i]);
            }

            return source.Project<T>(pro);
        }

        public static IFindFluent<T, T> ExcludeOneIncludeMany<T>(this IFindFluent<T, T> source, Expression<Func<T, object>> exclude, params Expression<Func<T, object>>[] include) {
            var includes = GetPropertyNames(include);
            var excludes = GetPropertyNames(exclude);
            
            var pro = Builders<T>.Projection.Include(includes[0]);
            for (int i = 1; i < includes.Length; i++) {
                pro = pro.Include(includes[i]);
            }

            for (int i = 0; i < excludes.Length; i++) {
                pro = pro.Exclude(excludes[i]);
            }

            return source.Project<T>(pro);
        }
        
        private static FilterDefinition<T> FindFilterDefinition<T>(List<object> expression) {
            FilterDefinition<T> filter = null;
            var index = expression.FindIndex(x => x.ToString() == Constants.EQUAL || x.ToString() == Constants.GTE || x.ToString() == Constants.LTE ||
                                                      x.ToString() == Constants.NOTEQUAL || x.ToString() == Constants.GT || x.ToString() == Constants.LT ||
                                                      x.ToString() == Constants.REGEX);
            var leftOperand = expression.Take(index + 1).First().ToString();
            var rightOperand = expression.Skip(index + 1).First();
            var op = expression[index].ToString();

            switch (op) {
                case Constants.EQUAL:
                    filter = Builders<T>.Filter.Eq(leftOperand, rightOperand);
                    break;
                case Constants.GT:
                    filter = Builders<T>.Filter.Gt(leftOperand, rightOperand);
                    break;
                case Constants.GTE:
                    filter = Builders<T>.Filter.Gte(leftOperand, rightOperand);
                    break;
                case Constants.LT:
                    filter = Builders<T>.Filter.Lt(leftOperand, rightOperand);
                    break;
                case Constants.LTE:
                    filter = Builders<T>.Filter.Lte(leftOperand, rightOperand);
                    break;
                case Constants.NOTEQUAL:
                    filter = Builders<T>.Filter.Ne(leftOperand, rightOperand);
                    break;
                case Constants.REGEX:
                    filter = Builders<T>.Filter.Regex(leftOperand, new BsonRegularExpression(rightOperand.ToString(), "i"));
                    break;
                default:
                    break;
            }

            return filter;
        }

        private static FilterDefinition<T> FindFilterDefinition<T>(List<object> expression, FilterDefinition<T> filter) {
            var index = expression.FindIndex(x => x.ToString() == Constants.OR || x.ToString() == Constants.AND);
            if (index >= 0) {
                var opp = expression[index].ToString();
                var operand = expression.Skip(index + 1).ToList();
                switch (opp) {
                    case Constants.OR:
                        filter |= FindFilterDefinition<T>(operand);
                        break;
                    case Constants.AND:
                        filter &= FindFilterDefinition<T>(operand);
                        break;
                    default:
                        break;
                }
            } else {
                filter = FindFilterDefinition<T>(expression);
            }

            return filter;
        }

        private static string[] GetPropertyNames<T>(params Expression<Func<T, object>>[] actions) {
            var result = new string[actions.Length];
            for (int i = 0; i < actions.Length; i++) {
                var expressionBody = actions[i].Body;
                if (expressionBody.NodeType == ExpressionType.Convert) {
                    var unaryExpression = (UnaryExpression)expressionBody;
                    result[i] = ((MemberExpression)unaryExpression.Operand).Member.Name;
                    continue;
                }

                var expression = (MemberExpression)expressionBody;
                result[i] = expression.Member.Name;
            }

            return result;
        }
    }
}
