using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Options;
using Yashdeep.Persistence.Local.Security;
using Xunit;

namespace Yashdeep.Persistence.Local.Tests;

public class LocalDatabaseInitializerTests : IDisposable
{
    private readonly string _testDbDir;

    public LocalDatabaseInitializerTests()
    {
        _testDbDir = Path.Combine(Path.GetTempPath(), "yashdeep_tests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDbDir))
        {
            try
            {
                Directory.Delete(_testDbDir, recursive: true);
            }
            catch
            {
                // Ignore temp directory cleanup locks
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesDatabaseDirectory_AndInitializesSchema()
    {
        // Arrange
        var options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "test_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };

        var keyProvider = new InMemoryKeyProvider("TestSqlCipherKey_2026!");
        var dbContextFactory = new LocalDbContextFactory(options, keyProvider);
        var initializer = new LocalDatabaseInitializer(options, keyProvider, dbContextFactory);

        // Act
        await initializer.InitializeAsync();

        // Assert
        var dbPath = options.GetFullDatabasePath();
        Assert.True(Directory.Exists(_testDbDir));
        Assert.True(File.Exists(dbPath));
        Assert.False(string.IsNullOrWhiteSpace(initializer.VerificationNotes));

        // Verify tables were created by querying LocalSettings
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var settingsExists = dbContext.LocalSettings.Any();
        Assert.True(settingsExists);
    }

    [Fact]
    public async Task InitializeAsync_ExecutesWalMode_Successfully()
    {
        // Arrange
        var options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "test_wal_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };

        var keyProvider = new InMemoryKeyProvider("TestSqlCipherWalKey_2026!");
        var dbContextFactory = new LocalDbContextFactory(options, keyProvider);
        var initializer = new LocalDatabaseInitializer(options, keyProvider, dbContextFactory);

        // Act
        await initializer.InitializeAsync();

        // Assert
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            var mode = await command.ExecuteScalarAsync() as string;

            Assert.NotNull(mode);
            Assert.Equal("wal", mode, ignoreCase: true);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
