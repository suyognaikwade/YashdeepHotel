using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Options;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Local.Security;
using Xunit;

namespace Yashdeep.Persistence.Local.Tests;

public class SqlCipherEdgePersistenceVerificationTests : IDisposable
{
    private readonly string _testDbDir;

    public SqlCipherEdgePersistenceVerificationTests()
    {
        _testDbDir = Path.Combine(Path.GetTempPath(), "yashdeep_sqlcipher_verify_" + Guid.NewGuid().ToString("N"));
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
                // Ignore cleanup locks
            }
        }
    }

    [Fact]
    public async Task PhysicalFileEncryption_IsEncryptedOnDisk_AndUnreadableWithoutKey()
    {
        // 1. Arrange & Create Encrypted DB
        var options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "encrypted_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };

        const string validKey = "SecurePassphrase2026!EncryptedSecret";
        var keyProvider = new InMemoryKeyProvider(validKey);
        var dbContextFactory = new LocalDbContextFactory(options, keyProvider);
        var initializer = new LocalDatabaseInitializer(options, keyProvider, dbContextFactory);

        await initializer.InitializeAsync();

        var tenantId = Guid.NewGuid();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Encrypted Test Hotel",
                Code = "ETH01"
            });
            await dbContext.SaveChangesAsync();
        }

        // Close all connection pools to ensure file flushes to disk
        SqliteConnection.ClearAllPools();

        var dbPath = options.GetFullDatabasePath();
        Assert.True(File.Exists(dbPath));

        // 2. Read raw file header bytes directly
        var headerBytes = new byte[16];
        using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            await fs.ReadExactlyAsync(headerBytes, 0, headerBytes.Length);
        }

        var headerString = System.Text.Encoding.ASCII.GetString(headerBytes);
        // Standard unencrypted SQLite header starts with "SQLite format 3\0"
        Assert.False(headerString.StartsWith("SQLite format 3", StringComparison.Ordinal));

        // 3. Attempt unencrypted connection without password - MUST FAIL
        var unencryptedCsb = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWrite
        };

        var unencryptedEx = await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await using var unencryptedConn = new SqliteConnection(unencryptedCsb.ConnectionString);
            await unencryptedConn.OpenAsync();
            await using var cmd = unencryptedConn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            await cmd.ExecuteScalarAsync();
        });
        Assert.True(unencryptedEx.SqliteErrorCode == 26 || unencryptedEx.Message.Contains("file is not a database", StringComparison.OrdinalIgnoreCase) || unencryptedEx.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase));

        // 4. Attempt connection with wrong password - MUST FAIL
        var wrongKeyCsb = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Password = "WrongPassphrase123!",
            Mode = SqliteOpenMode.ReadWrite
        };

        var wrongKeyEx = await Assert.ThrowsAsync<SqliteException>(async () =>
        {
            await using var wrongKeyConn = new SqliteConnection(wrongKeyCsb.ConnectionString);
            await wrongKeyConn.OpenAsync();
            await using var cmd = wrongKeyConn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            await cmd.ExecuteScalarAsync();
        });
        Assert.True(wrongKeyEx.SqliteErrorCode == 26 || wrongKeyEx.Message.Contains("file is not a database", StringComparison.OrdinalIgnoreCase) || wrongKeyEx.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase));

        // 5. Connect with correct key via factory - MUST SUCCEED
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var tenant = await dbContext.Tenants.FindAsync(tenantId);
            Assert.NotNull(tenant);
            Assert.Equal("Encrypted Test Hotel", tenant.Name);
        }
    }

    [Fact]
    public async Task ProcessRestartSimulation_PreservesDataAcrossConnectionLifecycle()
    {
        var options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "restart_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };

        const string secretKey = "ProcessRestartKey_998877!";
        var keyProvider = new InMemoryKeyProvider(secretKey);

        // Session 1: Initialize and write
        var factory1 = new LocalDbContextFactory(options, keyProvider);
        var initializer1 = new LocalDatabaseInitializer(options, keyProvider, factory1);
        await initializer1.InitializeAsync();

        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        await using (var context1 = await factory1.CreateDbContextAsync())
        {
            context1.Tenants.Add(new Tenant { Id = tenantId, Name = "Restart Hotel", Code = "RH" });
            context1.Branches.Add(new Branch { Id = branchId, TenantId = tenantId, Name = "Main Branch", Code = "MB" });
            await context1.SaveChangesAsync();
        }

        // Simulate Process Termination by clearing connection pools
        SqliteConnection.ClearAllPools();

        // Session 2: Fresh start with new factory instance reading same file
        var factory2 = new LocalDbContextFactory(options, keyProvider);
        await using (var context2 = await factory2.CreateDbContextAsync())
        {
            var savedTenant = await context2.Tenants.Include(t => t.Branches).FirstOrDefaultAsync(t => t.Id == tenantId);
            Assert.NotNull(savedTenant);
            Assert.Equal("Restart Hotel", savedTenant.Name);
            Assert.Single(savedTenant.Branches);
            Assert.Equal("Main Branch", savedTenant.Branches.First().Name);
        }
    }

    [Fact]
    public async Task TenantIsolation_ContextSwitching_PreventsCrossTenantDataLeak()
    {
        var options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "multi_tenant_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };

        var keyProvider = new InMemoryKeyProvider("MultiTenantKey_112233!");
        var factory = new LocalDbContextFactory(options, keyProvider);
        var initializer = new LocalDatabaseInitializer(options, keyProvider, factory);
        await initializer.InitializeAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed data for both tenants using unpartitioned context
        await using (var systemContext = new LocalDbContext(new DbContextOptionsBuilder<LocalDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = options.GetFullDatabasePath(),
                Password = "MultiTenantKey_112233!"
            }.ConnectionString).Options, currentTenantId: Guid.Empty))
        {
            systemContext.Tenants.Add(new Tenant { Id = tenantA, Name = "Tenant A", Code = "TA" });
            systemContext.Tenants.Add(new Tenant { Id = tenantB, Name = "Tenant B", Code = "TB" });

            systemContext.Branches.Add(new Branch { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Branch A1", Code = "BA1" });
            systemContext.Branches.Add(new Branch { Id = Guid.NewGuid(), TenantId = tenantA, Name = "Branch A2", Code = "BA2" });
            systemContext.Branches.Add(new Branch { Id = Guid.NewGuid(), TenantId = tenantB, Name = "Branch B1", Code = "BB1" });

            await systemContext.SaveChangesAsync();
        }

        // Step 1: Create Context for Tenant A
        var factoryA = new LocalDbContextFactory(options, keyProvider, currentTenantId: tenantA);
        await using (var contextA = await factoryA.CreateDbContextAsync())
        {
            var branchesA = await contextA.Branches.ToListAsync();
            Assert.Equal(2, branchesA.Count);
            Assert.All(branchesA, b => Assert.Equal(tenantA, b.TenantId));
        }

        // Step 2: Create Context for Tenant B
        var factoryB = new LocalDbContextFactory(options, keyProvider, currentTenantId: tenantB);
        await using (var contextB = await factoryB.CreateDbContextAsync())
        {
            var branchesB = await contextB.Branches.ToListAsync();
            Assert.Single(branchesB);
            Assert.Equal(tenantB, branchesB[0].TenantId);
            Assert.Equal("Branch B1", branchesB[0].Name);
        }

        // Step 3: Verify Context switching back to Tenant A
        await using (var contextA2 = await factoryA.CreateDbContextAsync())
        {
            var branchesA2 = await contextA2.Branches.ToListAsync();
            Assert.Equal(2, branchesA2.Count);
            Assert.All(branchesA2, b => Assert.Equal(tenantA, b.TenantId));
        }
    }
}
