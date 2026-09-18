using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yashdeep.Application.Services;
using Yashdeep.Domain.Entities;
using Yashdeep.Domain.Exceptions;
using Yashdeep.Persistence.Cloud;
using Yashdeep.Server.Api.Controllers;
using Xunit;

namespace Yashdeep.Security.Tests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();

        if (Request.Headers.TryGetValue("X-Test-UserId", out var userId))
        {
            claims.Add(new Claim("user_id", userId.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-TenantId", out var tenantId))
        {
            claims.Add(new Claim("tenant_id", tenantId.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-BranchId", out var branchId))
        {
            claims.Add(new Claim("branch_id", branchId.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
        {
            foreach (var role in roles.ToString().Split(','))
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class SecurityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly Guid TenantAId = Guid.NewGuid();
    private static readonly Guid TenantBId = Guid.NewGuid();

    private static readonly Guid BranchAId = Guid.NewGuid();
    private static readonly Guid BranchBId = Guid.NewGuid();

    private static readonly Guid UserAId = Guid.NewGuid();
    private static readonly Guid UserOrgAdminId = Guid.NewGuid();
    private static readonly Guid SystemAdminId = Guid.NewGuid();

    public SecurityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                var validationService = new InMemoryTenantValidationService();

                // Tenant A setup
                var tenantA = new Tenant { Id = TenantAId, Name = "Tenant A", IsActive = true };
                var branchA = new Branch { Id = BranchAId, TenantId = TenantAId, Name = "Branch A" };
                var branchB = new Branch { Id = BranchBId, TenantId = TenantAId, Name = "Branch B" };
                tenantA.Branches.Add(branchA);
                tenantA.Branches.Add(branchB);
                validationService.AddTenant(tenantA);

                // Tenant B setup
                var tenantB = new Tenant { Id = TenantBId, Name = "Tenant B", IsActive = true };
                validationService.AddTenant(tenantB);

                // Users setup
                var userA = new User { Id = UserAId, TenantId = TenantAId, Username = "userA", IsActive = true };
                userA.BranchAssignments.Add(new UserBranchAssignment { UserId = UserAId, BranchId = BranchAId });
                validationService.AddUser(userA);

                var userOrgAdmin = new User { Id = UserOrgAdminId, TenantId = TenantAId, Username = "orgAdmin", IsActive = true, HasOrganizationWideAccess = true };
                validationService.AddUser(userOrgAdmin);

                var sysAdmin = new User { Id = SystemAdminId, TenantId = TenantAId, Username = "sysAdmin", IsActive = true, IsSystemAdmin = true };
                validationService.AddUser(sysAdmin);

                services.AddSingleton<ITenantValidationService>(validationService);
            });
        });
    }

    private HttpClient CreateAuthenticatedClient(Guid tenantId, Guid userId, Guid? branchId = null, string? roles = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer mock-token");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        if (branchId.HasValue)
        {
            client.DefaultRequestHeaders.Add("X-Test-BranchId", branchId.Value.ToString());
        }
        if (!string.IsNullOrEmpty(roles))
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        }
        return client;
    }

    [Fact]
    public async Task Scenario1_UserFromTenantA_AttemptingToAccessTenantB_IsRejected()
    {
        // User from Tenant A attempts to access Tenant B by supplying Tenant B's ID in query
        var client = CreateAuthenticatedClient(TenantAId, UserAId);

        var response = await client.GetAsync($"/api/TestSecurity/tenant-data?tenantId={TenantBId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Scenario2_UserFromBranchA_AttemptingToAccessBranchB_IsForbidden()
    {
        // User A assigned only to Branch A tries to request Branch B header/route
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer mock-token");
        client.DefaultRequestHeaders.Add("X-Test-TenantId", TenantAId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-UserId", UserAId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-BranchId", BranchBId.ToString());

        var response = await client.GetAsync($"/api/TestSecurity/branch-data/{BranchBId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Scenario3_UserWithOrganizationLevelPermission_CanAccessAuthorizedBranches()
    {
        var client = CreateAuthenticatedClient(TenantAId, UserOrgAdminId, BranchBId);

        var response = await client.GetAsync($"/api/TestSecurity/branch-data/{BranchBId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scenario4_UserWithoutBranchPermission_AttemptingBranchAccess_IsForbidden()
    {
        // User A (assigned to Branch A) requesting Branch B endpoint
        var client = CreateAuthenticatedClient(TenantAId, UserAId, BranchAId);

        var response = await client.GetAsync($"/api/TestSecurity/branch-data/{BranchBId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Scenario5_ManipulatedRouteQueryBodyTenantIdentifiers_AreRejected()
    {
        var client = CreateAuthenticatedClient(TenantAId, UserAId, BranchAId);

        // 1. Query manipulation
        var queryResp = await client.GetAsync($"/api/TestSecurity/tenant-data?tenantId={TenantBId}");
        queryResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Body manipulation
        var bodyPayload = new TestOrderRequest
        {
            TenantId = TenantBId, // Attempted tenant override in body
            BranchId = BranchAId,
            Description = "Tampered order",
            Amount = 100
        };
        var bodyResp = await client.PostAsJsonAsync("/api/TestSecurity/orders", bodyPayload);
        bodyResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Scenario6_BackgroundServiceExecution_WithNoTenantContext_IsIsolated()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CloudDbContext>();

        var uncontextualEntity = new TestOrderEntity
        {
            TenantId = Guid.Empty,
            BranchId = BranchAId,
            Description = "Background service order",
            Amount = 50
        };

        dbContext.TestOrders.Add(uncontextualEntity);

        // Assert that persisting entities without tenant context throws TenantAuthorizationException
        Func<Task> act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<TenantAuthorizationException>();
    }

    [Fact]
    public async Task Scenario7_UnauthorizedAdministrativeAccess_IsForbidden()
    {
        // Regular User A attempting admin portal endpoint
        var client = CreateAuthenticatedClient(TenantAId, UserAId);

        var response = await client.GetAsync("/api/TestSecurity/admin-portal");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // System Admin succeeds
        var adminClient = CreateAuthenticatedClient(TenantAId, SystemAdminId, roles: "SystemAdmin");
        var adminResponse = await adminClient.GetAsync("/api/TestSecurity/admin-portal");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
