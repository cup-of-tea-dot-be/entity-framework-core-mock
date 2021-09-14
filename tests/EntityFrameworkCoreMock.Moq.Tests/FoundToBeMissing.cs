namespace SophiaUnitTests
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using EntityFrameworkCoreMock;
    using Microsoft.EntityFrameworkCore;
    using NUnit.Framework;

    public class SimpleDbSet
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public string Hallo { get; set; }
        public string Sophia { get; set; }
        public string dot { get; set; }
        public string com { get; set; }
    }
    
    public class DemoDb : DbContext
    {
        public DemoDb() { }
        public DemoDb(DbContextOptions options) : base(options) { }
        public virtual DbSet<SimpleDbSet> SimpleDbSet { get; set; }
    }
    
    
    /// <summary>
    /// THIS IS A DEMO FOR ISSUE 36 https://github.com/cup-of-tea-dot-be/entity-framework-core-mock/issues/36
    /// </summary>
    
    [TestFixture]
    public class LimitationsOfEntityFrameworkCoreMock
    {
        private List<SimpleDbSet> simpleData;
        private DbContextMock<DemoDb> dbContextMock;

        public DbContextOptions<DemoDb> DummyOptions { get; } = new DbContextOptionsBuilder<DemoDb>().Options;
        
        [SetUp]
        public void Init()
        {
            this.dbContextMock = new DbContextMock<DemoDb>(DummyOptions);
            this.simpleData = new List<SimpleDbSet>() { new SimpleDbSet() };
            this.dbContextMock = new DbContextMock<DemoDb>(DummyOptions);
            this.dbContextMock.Reset();
            this.dbContextMock.CreateDbSetMock(x => x.SimpleDbSet, simpleData);
        }

        [Test]
        public void AsQueryableNotWrongCountShouldNotFailButDoesIndeedFail()
        {
            var db = this.dbContextMock.Object;
            Assert.IsTrue(db.SimpleDbSet.Count() == simpleData.Count());
            Assert.IsTrue(db.SimpleDbSet.AsQueryable().Count() == simpleData.Count());
        }
        
        [Test]
        public void DbEntryReloadsDataFreshFromDbShouldNotFailButDoesIndeedFailLikeAChamp()
        {
            //force fresh db data https://stackoverflow.com/a/22177752/828184
            var db = this.dbContextMock.Object;
            var entry = db.SimpleDbSet.First();
            var entryCalled = db.Entry(entry);
            entryCalled.Reload();
            db.Entry(entry).Reference(x => x).Load(); //of course would select a table and not just x
        }
    }
}