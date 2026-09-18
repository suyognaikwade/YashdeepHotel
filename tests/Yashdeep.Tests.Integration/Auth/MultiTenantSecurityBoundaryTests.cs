using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yashdeep.Application.DTOs;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Cloud;
using Xunit;
using FluentAssertions;

namespace Yashdeep.Tests.Integration.Auth;

public class MultiTenantSecurityBoundaryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MultiTenantSecurityBoundaryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private class TenantDataResultDto
    {
        public Guid CurrentTenantId { get; set; }
        public Guid CurrentUserId { get; set; }
        public List<UserDto> Users { get; set; } = new();
        public List<OrgDto> Organizations { get; set; } = new();
    }

    private class UserDto
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string Username { get; set; } = string.Empty;
    }

    private class OrgDto
    {
        public Guid OrganizationId { get; set; }
        public Guid TenantId { get; set; }
        public string LegalName { get; set; } = string.Empty;
    }

    [Fact]
    public async Task TenantDataEndpoint_ShouldNeverReturnDataFromAnotherTenant()
    {
        // Arrange
        var tenantA_Id = Guid.NewGuid();
        var orgA_Id = Guid.NewGuid();
        var userA_Id = Guid.NewGuid();
        var usernameA = "tenantA_admin_" + Guid.NewGuid().ToString("N")[..6];

        var tenantB_Id = Guid.NewGuid();
        var orgB_Id = Guid.NewGuid();
        var userB_Id = Guid.NewGuid();
        var usernameB = "tenantB_admin_" + Guid.NewGuid().ToString("N")[..6];

        var rawPassword = "Pass123!Secure";

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                // Seed Tenant A
                var tenantA = new Tenant(tenantA_Id, "Tenant A Legal", "Tenant A Trade", "27AAAAA0000A1Z5", "tA@example.com", "9999999999");
                var orgA = new Organization(orgA_Id, tenantA_Id, "Org A", "FL3-AAA");
                var userA = new User(userA_Id, tenantA_Id, orgA_Id, usernameA, "usera@example.com", "temp");
                userA.SetPasswordHash(passwordHasher.HashPassword(userA, rawPassword));
                userA.AddRole(new Role(Guid.NewGuid(), tenantA_Id, "Admin"));

                // Seed Tenant B
                var tenantB = new Tenant(tenantB_Id, "Tenant B Legal", "Tenant B Trade", "27BBBBB0000B1Z5", "tB@example.com", "8888888888");
                var orgB = new Organization(orgB_Id, tenantB_Id, "Org B", "FL3-BBB");
                var userB = new User(userB_Id, tenantB_Id, orgB_Id, usernameB, "userb@example.com", "temp");
                userB.SetPasswordHash(passwordHasher.HashPassword(userB, rawPassword));
                userB.AddRole(new Role(Guid.NewGuid(), tenantB_Id, "Admin"));

                db.Tenants.AddRange(tenantA, tenantB);
                db.Organizations.AddRange(orgA, orgB);
                db.Users.AddRange(userA, userB);
                db.SaveChanges();
            });
        }).CreateClient();

        // Act 1: Login as Tenant A user
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
        {
            Username = usernameA,
            Password = rawPassword
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResult.Should().NotBeNull();

        // Act 2: Request tenant data using Tenant A bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);
        var dataResponse = await client.GetAsync("/api/v1/tenant-data");

        // Assert Security Boundary
        dataResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenantData = await dataResponse.Content.ReadFromJsonAsync<TenantDataResultDto>();

        tenantData.Should().NotBeNull();
        tenantData!.CurrentTenantId.Should().Be(tenantA_Id);

        // Prove 100% tenant data boundary isolation: zero Tenant B records returned!
        tenantData.Users.Should().OnlyContain(u => u.TenantId == tenantA_Id);
        tenantData.Users.Should().NotContain(u => u.TenantId == tenantB_Id);
        tenantData.Users.Should().NotContain(u => u.Username == usernameB);

        tenantData.Organizations.Should().OnlyContain(o => o.TenantId == tenantA_Id);
        tenantData.Organizations.Should().NotContain(o => o.TenantId == tenantB_Id);
    }
}
