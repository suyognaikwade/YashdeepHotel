using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Options;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Local.Security;
using Xunit;

namespace Yashdeep.Persistence.Local.Tests;

public class OrganizationalContextCrudTests : IDisposable
{
    private readonly string _testDbDir;
    private readonly LocalDatabaseOptions _options;
    private readonly InMemoryKeyProvider _keyProvider;
    private readonly LocalDbContextFactory _dbContextFactory;

    public OrganizationalContextCrudTests()
    {
        _testDbDir = Path.Combine(Path.GetTempPath(), "yashdeep_crud_tests_" + Guid.NewGuid().ToString("N"));
        _options = new LocalDatabaseOptions
        {
            DatabaseDirectory = _testDbDir,
            DatabaseFileName = "crud_edge.db",
            EnableWalMode = true,
            EnsureDirectoryExists = true
        };
        _keyProvider = new InMemoryKeyProvider("CrudPassphrase789!");
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
    public async Task OrganizationalHierarchy_FullLifecycle_SucceedsAtomically()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        using var uow = new LocalUnitOfWork(dbContext);

        var tenantRepo = uow.GetRepository<Tenant>();
        var branchRepo = uow.GetRepository<Branch>();
        var outletRepo = uow.GetRepository<Outlet>();
        var terminalRepo = uow.GetRepository<Terminal>();
        var deviceRepo = uow.GetRepository<Device>();
        var userRepo = uow.GetRepository<User>();
        var roleRepo = uow.GetRepository<Role>();
        var userRoleRepo = uow.GetRepository<UserRole>();

        // Act
        await uow.BeginTransactionAsync();

        await tenantRepo.AddAsync(new Tenant { Id = tenantId, Name = "Yashdeep Group", Code = "YHG" });
        await branchRepo.AddAsync(new Branch { Id = branchId, TenantId = tenantId, Name = "Bhenda Branch", Code = "BH01", Address = "Bhenda, MH" });
        await outletRepo.AddAsync(new Outlet { Id = outletId, TenantId = tenantId, BranchId = branchId, Name = "AC Bar", Code = "AC01", OutletType = "Bar" });
        await terminalRepo.AddAsync(new Terminal { Id = terminalId, TenantId = tenantId, BranchId = branchId, OutletId = outletId, DeviceName = "Counter-POS-1", MacAddress = "AA:BB:CC:DD:EE:FF", IPAddress = "192.168.1.10" });
        await deviceRepo.AddAsync(new Device { Id = deviceId, TenantId = tenantId, BranchId = branchId, HardwareId = "HW-TEST-998877", IsRegistered = true });
        await userRepo.AddAsync(new User { Id = userId, TenantId = tenantId, Username = "cashier1", FullName = "John Cashier", PasswordHash = "Argon2idHashPlaceholder" });
        await roleRepo.AddAsync(new Role { Id = roleId, TenantId = tenantId, Name = "Cashier", NormalizedName = "CASHIER" });
        await userRoleRepo.AddAsync(new UserRole { UserId = userId, RoleId = roleId, TenantId = tenantId });

        await uow.CommitTransactionAsync();

        // Assert
        await using var verifyContext = await _dbContextFactory.CreateDbContextAsync();
        var savedTenant = await verifyContext.Tenants.Include(t => t.Branches).FirstOrDefaultAsync(t => t.Id == tenantId);
        var savedOutlet = await verifyContext.Outlets.FirstOrDefaultAsync(o => o.Id == outletId);
        var savedTerminal = await verifyContext.Terminals.FirstOrDefaultAsync(t => t.Id == terminalId);
        var savedDevice = await verifyContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        var savedUserRole = await verifyContext.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        Assert.NotNull(savedTenant);
        Assert.Single(savedTenant.Branches);
        Assert.NotNull(savedOutlet);
        Assert.NotNull(savedTerminal);
        Assert.NotNull(savedDevice);
        Assert.NotNull(savedUserRole);
    }
}
