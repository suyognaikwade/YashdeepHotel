using Yashdeep.Application.Common.Options;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Local.Security;
using Xunit;

namespace Yashdeep.Persistence.Local.Tests;

public class LocalTransactionTests : IDisposable
{
    private readonly string _testDbDir;
    private readonly LocalDatabaseOptions _options;
    private readonly InMemoryKeyProvider _keyProvider;
    private readonly LocalDbContextFactory _dbContextFactory;

    public LocalTransactionTests()
    {
        _testDbDir = Path.Combine(Path.GetTempPath(), "yashdeep_tx_tests_" + Guid.NewGuid().ToString("N"));
        _options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "tx_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };
        _keyProvider = new InMemoryKeyProvider("TxPassphrase999!");
        _dbContextFactory = new LocalDbContextFactory(_options, _keyProvider);

        var initializer = new LocalDatabaseInitializer(_options, _keyProvider, _dbContextFactory);
        initializer.InitializeAsync().GetAwaiter().GetResult();
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
    public async Task CommitTransactionAsync_PersistsChangesToDatabase()
    {
        // Arrange
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        using var uow = new LocalUnitOfWork(dbContext);
        var tenantRepo = uow.GetRepository<Tenant>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Committed Hotel Group",
            Code = "CHG01",
            IsActive = true
        };

        // Act
        await uow.BeginTransactionAsync();
        await tenantRepo.AddAsync(tenant);
        await uow.CommitTransactionAsync();

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var persisted = await verifyContext.Tenants.FindAsync(tenant.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Committed Hotel Group", persisted.Name);
    }

    [Fact]
    public async Task RollbackTransactionAsync_RevertsChangesInDatabase()
    {
        // Arrange
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        using var uow = new LocalUnitOfWork(dbContext);
        var tenantRepo = uow.GetRepository<Tenant>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Rollback Hotel Group",
            Code = "RHG01",
            IsActive = true
        };

        // Act
        await uow.BeginTransactionAsync();
        await tenantRepo.AddAsync(tenant);
        await uow.SaveChangesAsync(); // staged in context but uncommitted in transaction
        await uow.RollbackTransactionAsync();

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var persisted = await verifyContext.Tenants.FindAsync(tenant.Id);
        Assert.Null(persisted);
    }

    [Fact]
    public async Task Savepoint_RollsBackToSavepoint_PartialTransaction()
    {
        // Arrange
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        using var uow = new LocalUnitOfWork(dbContext);
        var tenantRepo = uow.GetRepository<Tenant>();

        var tenant1 = new Tenant { Id = Guid.NewGuid(), Name = "Tenant 1", Code = "T1" };
        var tenant2 = new Tenant { Id = Guid.NewGuid(), Name = "Tenant 2", Code = "T2" };

        // Act
        await uow.BeginTransactionAsync();
        await tenantRepo.AddAsync(tenant1);
        await uow.SaveChangesAsync();

        await uow.CreateSavepointAsync("AfterTenant1");

        await tenantRepo.AddAsync(tenant2);
        await uow.SaveChangesAsync();

        // Roll back to savepoint (reverts tenant2, keeps tenant1)
        await uow.RollbackToSavepointAsync("AfterTenant1");
        await uow.CommitTransactionAsync();

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var saved1 = await verifyContext.Tenants.FindAsync(tenant1.Id);
        var saved2 = await verifyContext.Tenants.FindAsync(tenant2.Id);

        Assert.NotNull(saved1);
        Assert.Null(saved2);
    }
}
