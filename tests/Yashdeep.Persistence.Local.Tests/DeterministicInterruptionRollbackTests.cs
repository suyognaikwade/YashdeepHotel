using Yashdeep.Application.Common.Options;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Local.Security;
using Xunit;

namespace Yashdeep.Persistence.Local.Tests;

public class DeterministicInterruptionRollbackTests : IDisposable
{
    private readonly string _testDbDir;
    private readonly LocalDatabaseOptions _options;
    private readonly InMemoryKeyProvider _keyProvider;
    private readonly LocalDbContextFactory _dbContextFactory;

    public DeterministicInterruptionRollbackTests()
    {
        _testDbDir = Path.Combine(Path.GetTempPath(), "yashdeep_interrupt_tests_" + Guid.NewGuid().ToString("N"));
        _options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "interrupt_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };
        _keyProvider = new InMemoryKeyProvider("InterruptPassphrase123!");
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
    public async Task SimulatedMidTransactionFailure_RollsBackCleanly_WithoutCorruptingDatabase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var outletId = Guid.NewGuid();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        using var uow = new LocalUnitOfWork(dbContext);

        var tenantRepo = uow.GetRepository<Tenant>();
        var branchRepo = uow.GetRepository<Branch>();
        var outletRepo = uow.GetRepository<Outlet>();

        // Act: Start transaction, insert Tenant & Branch, then simulate unexpected system exception before Outlet commit
        try
        {
            await uow.BeginTransactionAsync();

            await tenantRepo.AddAsync(new Tenant
            {
                Id = tenantId,
                Name = "Interrupted Hotel Group",
                Code = "IHG01"
            });
            await uow.SaveChangesAsync();

            await branchRepo.AddAsync(new Branch
            {
                Id = branchId,
                TenantId = tenantId,
                Name = "Interrupted Branch",
                Code = "IB01"
            });
            await uow.SaveChangesAsync();

            // Simulate business logic or system hardware failure prior to committing Outlet
            throw new InvalidOperationException("Deterministic Simulated System Power Loss / Interruption!");

            #pragma warning disable CS0162 // Unreachable code detected
            await outletRepo.AddAsync(new Outlet
            {
                Id = outletId,
                TenantId = tenantId,
                BranchId = branchId,
                Name = "Interrupted Outlet",
                Code = "IO01",
                OutletType = "Dining"
            });
            await uow.CommitTransactionAsync();
            #pragma warning restore CS0162
        }
        catch (InvalidOperationException)
        {
            // Cleanly handle transaction rollback in exception handler
            await uow.RollbackTransactionAsync();
        }

        // Assert: Verify database contains ZERO records from the interrupted transaction
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var persistedTenant = await verifyContext.Tenants.FindAsync(tenantId);
        var persistedBranch = await verifyContext.Branches.FindAsync(branchId);
        var persistedOutlet = await verifyContext.Outlets.FindAsync(outletId);

        Assert.Null(persistedTenant);
        Assert.Null(persistedBranch);
        Assert.Null(persistedOutlet);
    }

    [Fact]
    public async Task SavepointInterruption_RevertsOnlyInterruptedPhase()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var validBranchId = Guid.NewGuid();
        var invalidBranchId = Guid.NewGuid();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        using var uow = new LocalUnitOfWork(dbContext);

        var tenantRepo = uow.GetRepository<Tenant>();
        var branchRepo = uow.GetRepository<Branch>();

        // Act: Begin transaction, persist Tenant and valid Branch, savepoint, attempt invalid operation and rollback to savepoint
        await uow.BeginTransactionAsync();

        await tenantRepo.AddAsync(new Tenant
        {
            Id = tenantId,
            Name = "Savepoint Hotel Group",
            Code = "SHG01"
        });
        await branchRepo.AddAsync(new Branch
        {
            Id = validBranchId,
            TenantId = tenantId,
            Name = "Valid Branch",
            Code = "VB01"
        });
        await uow.SaveChangesAsync();

        await uow.CreateSavepointAsync("ValidBranchSavepoint");

        try
        {
            // Add second branch and simulate failure
            await branchRepo.AddAsync(new Branch
            {
                Id = invalidBranchId,
                TenantId = tenantId,
                Name = "Failed Branch",
                Code = "FB01"
            });
            await uow.SaveChangesAsync();

            throw new InvalidOperationException("Simulated failure in sub-workflow!");
        }
        catch (InvalidOperationException)
        {
            await uow.RollbackToSavepointAsync("ValidBranchSavepoint");
        }

        await uow.CommitTransactionAsync();

        // Assert: Valid branch and tenant are preserved, failed branch is discarded
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var savedTenant = await verifyContext.Tenants.FindAsync(tenantId);
        var savedValidBranch = await verifyContext.Branches.FindAsync(validBranchId);
        var savedInvalidBranch = await verifyContext.Branches.FindAsync(invalidBranchId);

        Assert.NotNull(savedTenant);
        Assert.NotNull(savedValidBranch);
        Assert.Null(savedInvalidBranch);
    }
}
