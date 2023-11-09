/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCoreMock
{
    public class DbAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public DbAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            if (expression is MethodCallExpression m)
            {
                var resultType = m.Method.ReturnType; // it should be IQueryable<T>
                var tElement = resultType.GetGenericArguments().First();
                var queryType = typeof(DbAsyncEnumerable<>).MakeGenericType(tElement);
                return (IQueryable)Activator.CreateInstance(queryType, expression);
            }

            return new DbAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<T> CreateQuery<T>(Expression expression)
        {
            return new DbAsyncEnumerable<T>(expression);
        }

        public object Execute(Expression expression)
        {
            return CompileExpressionItem<object>(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return CompileExpressionItem<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(
                    name: nameof(IQueryProvider.Execute),
                    genericParameterCount: 1,
                    types: new[] { typeof(Expression) })
                .MakeGenericMethod(expectedResultType)
                .Invoke(this, new[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult });
        }

        private static T CompileExpressionItem<T>(Expression expression)
            => Expression.Lambda<Func<T>>(
                    body: new Visitor().Visit(expression) ?? throw new InvalidOperationException("Visitor returns null"),
                    parameters: (IEnumerable<ParameterExpression>) null)
                .Compile()();

        private class Visitor : ExpressionVisitor
        {
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                var method = node.Method;
                if (method.DeclaringType?.FullName == "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions")
                {
                    var fakeMethod = GetType().GetMethod(method.Name);
                    if (fakeMethod != null)
                    {
                        var obj = Visit(node.Object);
                        var args = Visit(node.Arguments);
                        return Expression.Call(obj, fakeMethod.MakeGenericMethod(method.GetGenericArguments()), args);
                    }
                }
                return base.VisitMethodCall(node);
            }

            // ReSharper disable once UnusedMember.Local
            public static int ExecuteDelete<TSource>(IQueryable<TSource> source) =>
                source.Count();
            
            // ReSharper disable once UnusedMember.Local
            public static int ExecuteUpdate<TSource>(IQueryable<TSource> source) =>
                source.Count();
        }
    }
}
