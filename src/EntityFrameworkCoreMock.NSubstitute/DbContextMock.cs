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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;

namespace EntityFrameworkCoreMock.NSubstitute
{
    public class DbContextMock<TDbContext> where TDbContext : DbContext
    {
        private readonly IKeyFactoryBuilder _keyFactoryBuilder;
        private readonly Dictionary<Type, IDbSetMock> _dbSetCache = new Dictionary<Type, IDbSetMock>();

        public TDbContext Object { get; set; }

        public DbContextMock(params object[] args)
            : this(new CompositeKeyFactoryBuilder(), args)
        {
        }

        private DbContextMock(IKeyFactoryBuilder keyFactoryBuilder, params object[] args)
        {
            Object = Substitute.For<TDbContext>(args);
            Object.Database.Returns(Substitute.For<DatabaseFacade>(Object));
            _keyFactoryBuilder = keyFactoryBuilder ?? throw new ArgumentNullException(nameof(keyFactoryBuilder));
            Reset();
        }

        public DbSetMock<TEntity> CreateDbSetMock<TEntity>(Expression<Func<TDbContext, DbSet<TEntity>>> dbSetSelector, IEnumerable<TEntity> initialEntities = null)
            where TEntity : class
            => CreateDbSetMock(dbSetSelector, _keyFactoryBuilder.BuildKeyFactory<TEntity>(), initialEntities);

        public DbSetMock<TEntity> CreateDbSetMock<TEntity>(IEnumerable<TEntity> initialEntities)
            where TEntity : class
        {
            var expParam = Expression.Parameter(typeof(TDbContext));
            var expBody = Expression.PropertyOrField(expParam, typeof(TEntity).Name);
            var exp = Expression.Lambda<Func<TDbContext, DbSet<TEntity>>>(expBody, expParam);
            return CreateDbSetMock(exp, initialEntities);
        }

        public DbSetMock<TEntity> CreateDbSetMock<TEntity>(
            Expression<Func<TDbContext, DbSet<TEntity>>> dbSetSelector,
            Func<TEntity, KeyContext, object> entityKeyFactory,
            IEnumerable<TEntity> initialEntities = null)
            where TEntity : class
        {
            if (dbSetSelector == null) throw new ArgumentNullException(nameof(dbSetSelector));
            if (entityKeyFactory == null) throw new ArgumentNullException(nameof(entityKeyFactory));

            var entityType = typeof(TEntity);
            if (_dbSetCache.ContainsKey(entityType)) throw new ArgumentException($"DbSetMock for entity {entityType.Name} already created", nameof(dbSetSelector));
            var mock = new DbSetMock<TEntity>(initialEntities, entityKeyFactory);
            Object.Set<TEntity>().Returns(mock.Object);

            dbSetSelector.Compile()(Object).Returns(mock.Object);

            _dbSetCache.Add(entityType, mock);
            return mock;
        }

        public void Reset()
        {
            _dbSetCache.Clear();
            Object.ClearReceivedCalls();
            Object.SaveChanges().Returns(_ => SaveChanges());
            Object.SaveChanges(Arg.Any<bool>()).Returns(_ => SaveChanges());
            Object.SaveChangesAsync().Returns(_ => SaveChanges());
            Object.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => SaveChanges());
        }

        // Facilitates unit-testing
        internal void RegisterDbSetMock<TEntity>(Expression<Func<TDbContext, DbSet<TEntity>>> dbSetSelector, IDbSetMock dbSet)
            where TEntity : class
        {
            var entityType = typeof(TEntity);
            _dbSetCache.Add(entityType, dbSet);
        }

        private int SaveChanges() => _dbSetCache.Values.Aggregate(0, (seed, dbSet) => seed + dbSet.SaveChanges());
    }
}
