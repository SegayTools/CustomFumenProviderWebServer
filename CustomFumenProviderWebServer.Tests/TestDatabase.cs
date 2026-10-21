using CustomFumenProviderWebServer.Databases;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Tests;

/// <summary>
/// 每个测试独占的 SQLite 内存数据库，避免依赖本机 MySQL。
/// </summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public DbContextOptions<FumenDataDB> Options { get; }
    public IDbContextFactory<FumenDataDB> Factory { get; }

    public TestDatabase()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Options = new DbContextOptionsBuilder<FumenDataDB>()
            .UseSqlite(connection)
            .Options;

        using (var db = new FumenDataDB(Options))
            db.Database.EnsureCreated();

        Factory = new TestDbContextFactory(Options);
    }

    public void Dispose()
    {
        connection.Dispose();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<FumenDataDB>
    {
        private readonly DbContextOptions<FumenDataDB> options;

        public TestDbContextFactory(DbContextOptions<FumenDataDB> options)
        {
            this.options = options;
        }

        public FumenDataDB CreateDbContext()
        {
            return new FumenDataDB(options);
        }
    }
}
