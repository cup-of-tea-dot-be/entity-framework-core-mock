/*
 * Copyright 2017-2021 Wouter Huysentruit
 *
 * See LICENSE file.
 */

using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityFrameworkCoreMock.Shared.KeyFactoryBuilders
{
    public abstract class KeyFactoryBuilderBase : IKeyFactoryBuilder
    {
        public Func<T, KeyContext, object> BuildKeyFactory<T>()
        {
            var keyProperties = ResolveKeyProperties<T>();
            var keyFactory = BuildIdentityKeyFactory<T>(keyProperties);
            keyFactory = keyFactory ?? BuildDefaultKeyFactory<T>(keyProperties);
            return keyFactory;
        }

        protected abstract PropertyInfo[] ResolveKeyProperties<T>();

        private static Func<T, KeyContext, object> BuildIdentityKeyFactory<T>(PropertyInfo[] keyProperties)
        {
            if (keyProperties.Length != 1) return null;
            var keyProperty = keyProperties[0];
            if (keyProperty == null) return null;
            var databaseGeneratedAttribute = keyProperty.GetCustomAttribute(typeof(DatabaseGeneratedAttribute)) as DatabaseGeneratedAttribute;
            if (databaseGeneratedAttribute?.DatabaseGeneratedOption != DatabaseGeneratedOption.Identity) return null;

            var entityArgument = Expression.Parameter(typeof(T));
            var keyContextArgument = Expression.Parameter(typeof(KeyContext));

            if (keyProperty.PropertyType == typeof(int))
            {
                return BuildAutoincrementIdentityKeyFactory<T, int>(keyProperty, ctx => Expression.Property(ctx, nameof(KeyContext.NextIdentity)));
            }
            else if (keyProperty.PropertyType == typeof(long))
            {
                return BuildAutoincrementIdentityKeyFactory<T, long>(keyProperty, ctx => Expression.Property(ctx, nameof(KeyContext.NextIdentity)));
            }
            else if (keyProperty.PropertyType == typeof(Guid))
            {
                return BuildIdentityKeyFactory<T, Guid>(keyProperty, _ => Expression.Call(typeof(Guid), nameof(Guid.NewGuid), Array.Empty<Type>()));
            }

            return null;
        }

        private static Func<TEntity, KeyContext, object> BuildIdentityKeyFactory<TEntity, TKey>(
            PropertyInfo keyProperty,
            Func<ParameterExpression, Expression> nextIdentity)
        {
            var entityArgument = Expression.Parameter(typeof(TEntity));
            var keyContextArgument = Expression.Parameter(typeof(KeyContext));
            var keyValueVariable = Expression.Variable(typeof(TKey));
            var body = Expression.Block(typeof(object),
                new[] { keyValueVariable },
                Expression.Assign(keyValueVariable, Expression.Convert(Expression.Property(entityArgument, keyProperty), typeof(TKey))),
                Expression.IfThen(Expression.Equal(keyValueVariable, Expression.Default(typeof(TKey))),
                    Expression.Block(
                        Expression.Assign(keyValueVariable, Expression.Convert(nextIdentity(keyContextArgument), typeof(TKey))),
                        Expression.Assign(Expression.Property(entityArgument, keyProperty), keyValueVariable)
                    )
                ),
                Expression.Convert(keyValueVariable, typeof(object)));

            return Expression.Lambda<Func<TEntity, KeyContext, object>>(body, entityArgument, keyContextArgument).Compile();
        }
        
        private static Func<TEntity, KeyContext, object> BuildAutoincrementIdentityKeyFactory<TEntity, TKey>(
            PropertyInfo keyProperty,
            Func<ParameterExpression, Expression> nextIdentity)
        {
            var entityArgument = Expression.Parameter(typeof(TEntity));
            var keyContextArgument = Expression.Parameter(typeof(KeyContext));
            var keyValueVariable = Expression.Variable(typeof(TKey));
            var body = Expression.Block(typeof(object),
                new[] { keyValueVariable },
                Expression.Assign(keyValueVariable, Expression.Convert(Expression.Property(entityArgument, keyProperty), typeof(TKey))),
                Expression.IfThenElse(Expression.Equal(keyValueVariable, Expression.Default(typeof(TKey))),
                    Expression.Block(
                        Expression.Assign(keyValueVariable, Expression.Convert(nextIdentity(keyContextArgument), typeof(TKey))),
                        Expression.Assign(Expression.Property(entityArgument, keyProperty), keyValueVariable)
                    ),
                    Expression.Block(
                        Expression.Assign(
                            Expression.Property(keyContextArgument, nameof(KeyContext.CurrentIdentity)),
                            Expression.Call(
                                typeof(Math),
                                nameof(Math.Max),
                                Type.EmptyTypes,
                                Expression.Convert(keyValueVariable, typeof(long)),
                                Expression.Property(keyContextArgument, nameof(KeyContext.CurrentIdentity))
                            )
                        )
                    )
                ),
                Expression.Convert(keyValueVariable, typeof(object)));

            return Expression.Lambda<Func<TEntity, KeyContext, object>>(body, entityArgument, keyContextArgument).Compile();
        }

        private static Func<T, KeyContext, object> BuildDefaultKeyFactory<T>(PropertyInfo[] keyProperties)
        {
            var entityType = typeof(T);

            var tupleType = Type.GetType($"System.Tuple`{keyProperties.Length}");
            if (tupleType == null) throw new InvalidOperationException($"No tuple type found for {keyProperties.Length} generic arguments");

            var keyPropertyTypes = keyProperties.Select(x => x.PropertyType).ToArray();
            var constructor = tupleType.MakeGenericType(keyPropertyTypes).GetConstructor(keyPropertyTypes);
            if (constructor == null) throw new InvalidOperationException($"No tuple constructor found for key in {entityType.Name} entity");

            var entityArgument = Expression.Parameter(entityType);
            var keyContextArgument = Expression.Parameter(typeof(KeyContext));
            var newTupleExpression = Expression.New(constructor, keyProperties.Select(x => Expression.Property(entityArgument, x)));
            return Expression.Lambda<Func<T, KeyContext, object>>(newTupleExpression, entityArgument, keyContextArgument).Compile();
        }
    }
}
