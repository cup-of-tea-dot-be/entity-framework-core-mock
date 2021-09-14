using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCoreMock.Moq.Tests.Enums;
using EntityFrameworkCoreMock.Tests.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EntityFrameworkCoreMock.Tests
{
    [TestFixture]
    public class DbContextMockTests
    {
        [Test]
        public void DbContextMock_Constructor_PassDbContextOptions_ShouldPassDbContextOptionsToMockedClass()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            Assert.That(dbContextMock.Object.Options, Is.EqualTo(Options));
        }

        [Test]
        public async Task DbContextMock_Constructor_ShouldSetupSaveChanges()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());
            Assert.That(dbContextMock.Object.SaveChanges(), Is.EqualTo(55861));
            Assert.That(await dbContextMock.Object.SaveChangesAsync(), Is.EqualTo(55861));
            Assert.That(await dbContextMock.Object.SaveChangesAsync(CancellationToken.None), Is.EqualTo(55861));
        }

        [Test]
        public void DbContextMock_Reset_ShouldForgetMockedDbSets()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());
            Assert.That(dbContextMock.Object.SaveChanges(), Is.EqualTo(55861));
            dbContextMock.Reset();
            Assert.That(dbContextMock.Object.SaveChanges(), Is.EqualTo(0));
        }

        [Test]
        public void DbContextMock_Reset_ShouldResetupSaveChanges()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());
            Assert.That(dbContextMock.Object.SaveChanges(), Is.EqualTo(55861));
            dbContextMock.Reset();
            Assert.That(dbContextMock.Object.SaveChanges(), Is.EqualTo(0));
            dbContextMock.RegisterDbSetMock(x => x.Users, new TestDbSetMock());
            Assert.That(dbContextMock.Object.SaveChanges(), Is.EqualTo(55861));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_CreateIdenticalDbSetMockTwice_ShouldThrowExceptionSecondTime()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            dbContextMock.CreateDbSetMock(x => x.Users);
            var ex = Assert.Throws<ArgumentException>(() => dbContextMock.CreateDbSetMock(x => x.Users));
            Assert.That(ex.ParamName, Is.EqualTo("dbSetSelector"));
            Assert.That(ex.Message, Does.StartWith("DbSetMock for entity User already created"));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_ShouldSetupMockForDbSetSelector()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            Assert.That(dbContextMock.Object.Users, Is.Null);
            dbContextMock.CreateDbSetMock(x => x.Users);
            Assert.That(dbContextMock.Object.Users, Is.Not.Null);
        }

        [TestCase(DbSetMockingType.WithSetSelector)]
        [TestCase(DbSetMockingType.WithoutSetSelector)]        
        [Test]
        public async Task DbContextMock_CreateDbSetMock_PassInitialEntities_DbSetShouldContainInitialEntities(DbSetMockingType dbSetMockingType)
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var users = new []
            {
                new User { Id = Guid.NewGuid(), FullName = "Eric Cartoon" },
                new User { Id = Guid.NewGuid(), FullName = "Billy Jewel" },
            };
            if (dbSetMockingType == DbSetMockingType.WithSetSelector)
            {
                dbContextMock.CreateDbSetMock(x => x.Users, users);
            }            
            else if (dbSetMockingType == DbSetMockingType.WithoutSetSelector)
            {
                dbContextMock.CreateDbSetMock(users);
            }
            Assert.That(dbContextMock.Object.Users.Count(), Is.EqualTo(2));
            Assert.That(await dbContextMock.Object.Users.CountAsync(), Is.EqualTo(2));

            var result = await dbContextMock.Object.Users.FirstAsync(x => x.FullName.StartsWith("Eric"));
            Assert.That(result.FullName, Is.EqualTo("Eric Cartoon"));

            result = dbContextMock.Object.Users.First(x => x.FullName.Contains("Jewel"));
            Assert.That(result.FullName, Is.EqualTo("Billy Jewel"));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_NoKeyFactoryForModelWithoutId_ShouldThrowException()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>(Options);

            // Act & Assert
            var ex = Assert.Throws<AggregateException>(() => dbContextMock.CreateDbSetMock(x => x.NoKeyModels));
            Assert.That(ex.Message, Does.StartWith("No key factory could be created for entity type NoKeyModel, see inner exceptions"));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_PassCustomKeyFactoryForModelWithoutId_ShouldNotThrowException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            Assert.DoesNotThrow(() => dbContextMock.CreateDbSetMock(x => x.NoKeyModels, (x, _) => x.ModelId));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_KeylessModel_ShouldNotThrowException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            Assert.DoesNotThrow(() => dbContextMock.CreateDbSetMock(x => x.NoKeyModels, (x, _) => x, new NoKeyModel[] { new NoKeyModel() }));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_ModelWithProtectedProperties_ShouldNotThrowException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            Assert.DoesNotThrow(() => dbContextMock.CreateDbSetMock(x =>
                x.ProtectedSetterPropertyModels, (x, _) => x, new[] {new ProtectedSetterPropertyModel()}));
        }

        [Ignore("Not yet ported to EntityFrameworkCoreMock")]
        public void DbContextMock_CreateDbSetMock_AddModelWithSameKeyTwice_ShouldThrowDbUpdatedException()
        {
            var userId = Guid.NewGuid();
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Users);
            dbSetMock.Object.Add(new User {Id = userId, FullName = "SomeName"});
            dbSetMock.Object.Add(new User {Id = Guid.NewGuid(), FullName = "SomeName"});
            dbContextMock.Object.SaveChanges();
            dbSetMock.Object.Add(new User {Id = userId, FullName = "SomeName"});
            Assert.Throws<DbUpdateException>(() => dbContextMock.Object.SaveChanges());
        }

        [Ignore("Not yet ported to EntityFrameworkCoreMock")]
        public void DbContextMock_CreateDbSetMock_DeleteUnknownModel_ShouldThrowDbUpdateConcurrencyException()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Users);
            dbSetMock.Object.Remove(new User {Id = Guid.NewGuid()});
            Assert.Throws<DbUpdateConcurrencyException>(() => dbContextMock.Object.SaveChanges());
        }

        [TestCase(DbSetMockingType.WithSetSelector)]
        [TestCase(DbSetMockingType.WithoutSetSelector)]
        [Test]
        public void DbContextMock_CreateDbSetMock_AddMultipleModelsWithDatabaseGeneratedIdentityKey_ShouldGenerateSequentialKey(DbSetMockingType dbSetMockingType)
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var generatedKeyModels = new[]
            {
                new GeneratedKeyModel {Value = "first"},
                new GeneratedKeyModel {Value = "second"}
            };
            DbSetMock<GeneratedKeyModel> dbSetMock = null;
            if (dbSetMockingType == DbSetMockingType.WithSetSelector)
            {
                dbSetMock = dbContextMock.CreateDbSetMock(x => x.GeneratedKeyModels, generatedKeyModels);
            }
            else if (dbSetMockingType == DbSetMockingType.WithoutSetSelector)
            {
                dbSetMock = dbContextMock.CreateDbSetMock(generatedKeyModels);
            }
            dbSetMock.Object.Add(new GeneratedKeyModel {Value = "third" });
            dbContextMock.Object.SaveChanges();

            Assert.That(dbSetMock.Object.Min(x => x.Id), Is.EqualTo(1));
            Assert.That(dbSetMock.Object.Max(x => x.Id), Is.EqualTo(3));
            Assert.That(dbSetMock.Object.First(x => x.Id == 1).Value, Is.EqualTo("first"));
            Assert.That(dbSetMock.Object.First(x => x.Id == 2).Value, Is.EqualTo("second"));
            Assert.That(dbSetMock.Object.First(x => x.Id == 3).Value, Is.EqualTo("third"));
        }

        [TestCase(DbSetMockingType.WithSetSelector)]
        [TestCase(DbSetMockingType.WithoutSetSelector)]
        [Test]
        public void DbContextMock_CreateDbSetMock_AddMultipleModelsWithGuidAsDatabaseGeneratedIdentityKey_ShouldGenerateRandomGuidAsKey(DbSetMockingType dbSetMockingType)
        {
            var knownId = Guid.NewGuid();
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var generatedGuidKeyModels = new[]
            {
                new GeneratedGuidKeyModel {Id = knownId, Value = "first"},
                new GeneratedGuidKeyModel {Value = "second"}
            };
            DbSetMock<GeneratedGuidKeyModel> dbSetMock = null;
            if (dbSetMockingType == DbSetMockingType.WithSetSelector)
            {
                dbSetMock = dbContextMock.CreateDbSetMock(x => x.GeneratedGuidKeyModels, generatedGuidKeyModels);
            }
            else if (dbSetMockingType == DbSetMockingType.WithoutSetSelector)
            {
                dbSetMock = dbContextMock.CreateDbSetMock(generatedGuidKeyModels);
            }
            dbSetMock.Object.Add(new GeneratedGuidKeyModel { Value = "third" });
            dbContextMock.Object.SaveChanges();

            var modelWithKnownId = dbSetMock.Object.FirstOrDefault(x => x.Id == knownId);
            Assert.That(modelWithKnownId, Is.Not.Null);
            Assert.That(modelWithKnownId.Value, Is.EqualTo("first"));
        }

        [Test]
        public async Task DbContextMock_CreateDbSetMock_AsyncAddMultipleModelsWithLongAsDatabaseGeneratedIdentityKey_ShouldGenerateIncrementalKey()
        {
            // Arrange
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Issue20Models);

            // Act
            await dbContextMock.Object.Issue20Models.AddAsync(new Issue20Model { Url = "A" });
            await dbContextMock.Object.Issue20Models.AddAsync(new Issue20Model { Url = "B" });
            await dbContextMock.Object.Issue20Models.AddAsync(new Issue20Model { Url = "C" });
            await dbContextMock.Object.SaveChangesAsync();

            // Assert
            Assert.That(dbSetMock.Object.First(x => x.Url == "A").LoggingRepositoryId, Is.EqualTo(1));
            Assert.That(dbSetMock.Object.First(x => x.Url == "B").LoggingRepositoryId, Is.EqualTo(2));
            Assert.That(dbSetMock.Object.First(x => x.Url == "C").LoggingRepositoryId, Is.EqualTo(3));
        }

        [Test]
        public void DbContextMock_CreateDbSetMock_GenericDbSetSelector_ShouldReturnDbSetMock()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Set<User>());
            Assert.That(dbSetMock.Object, Is.Not.Null);
        }

        [Test]
        public void DbContextMock_GenericSet_ShouldReturnDbSetMock()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Users);
            var dbSet = dbContextMock.Object.Set<User>();
            Assert.That(dbSet, Is.Not.Null);
            Assert.That(dbSet, Is.EqualTo(dbSetMock.Object));
        }

        [Test]
        public void DbContextMock_GenericSet_AsQueryable_ShouldReturnQueryable()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            var dbSetMock = dbContextMock.CreateDbSetMock(x => x.Users);
            var dbSet = dbContextMock.Object.Set<User>();
            Assert.That(dbSet.AsQueryable(), Is.Not.Null);
        }

        [Test]
        public void DbContextMock_BeginTransaction_CommitTransaction_ShouldNotFail()
        {
            var dbContextMock = new DbContextMock<TestDbContext>(Options);
            Assert.DoesNotThrow(() =>
            {
                var transaction = dbContextMock.Object.Database.BeginTransaction();
                transaction.Commit();
                transaction.Rollback();
            });

            Assert.DoesNotThrowAsync(async () =>
            {
                var transaction = await dbContextMock.Object.Database.BeginTransactionAsync();
                await transaction.CommitAsync();
                await transaction.RollbackAsync();
            });
        }

        public class TestDbSetMock : IDbSetMock
        {
            public int SaveChanges() => 55861;
        }

        public class TestDbQueryMock : IDbQueryMock
        {
        }

        public DbContextOptions Options { get; } = new DbContextOptionsBuilder().Options;

        public class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions options)
                : base(options)
            {
                Options = options;
            }

            public DbContextOptions Options { get; }

            public virtual DbSet<User> Users { get; set; }

            public virtual DbSet<NoKeyModel> NoKeyModels { get; set; }

            public virtual DbSet<ProtectedSetterPropertyModel> ProtectedSetterPropertyModels { get; set; }

            public virtual DbSet<GeneratedKeyModel> GeneratedKeyModels { get; set; }

            public virtual DbSet<GeneratedGuidKeyModel> GeneratedGuidKeyModels { get; set; }

            public virtual DbSet<Issue20Model> Issue20Models { get; set; }
        }
    }
}
