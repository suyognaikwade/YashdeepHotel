using System.Net;
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

public class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private (Guid tenantId, Guid orgId, Guid userId, string username, string rawPassword) CreateSeededUserData()
    {
        return (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "cashier_user_" + Guid.NewGuid().ToString("N")[..6], "SecurePass123!");
    }

    private HttpClient CreateClientWithSeededUser(out (Guid tenantId, Guid orgId, Guid userId, string username, string rawPassword) userData)
    {
        var data = CreateSeededUserData();
        userData = data;

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();
                var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                var user = new User(data.userId, data.tenantId, data.orgId, data.username, $"{data.username}@example.com", "temp");
                var hash = passwordHasher.HashPassword(user, data.rawPassword);
                user.SetPasswordHash(hash);

                var role = new Role(Guid.NewGuid(), data.tenantId, "Cashier");
                user.AddRole(role);

                db.Users.Add(user);
                db.SaveChanges();
            });
        }).CreateClient();

        return client;
    }

    [Fact]
    public async Task Login_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var client = CreateClientWithSeededUser(out var user);
        var loginRequest = new LoginRequestDto
        {
            Username = user.username,
            Password = user.rawPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Server returned 500 with body: {body}");

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResponse.RefreshToken.Should().NotBeNullOrWhiteSpace();
        authResponse.User.Username.Should().Be(user.username);
        authResponse.User.TenantId.Should().Be(user.tenantId);
    }
}
