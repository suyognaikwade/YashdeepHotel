using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Yashdeep.Domain.Entities;
using Yashdeep.Infrastructure.Security;
using Xunit;
using FluentAssertions;

namespace Yashdeep.Tests.Unit.Security;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        var configValues = new Dictionary<string, string?>
        {
            { "Jwt:SecretKey", "SUPER_SECRET_UNIT_TEST_KEY_32BYTES_LONG!" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" },
            { "Jwt:AccessTokenExpirationMinutes", "15" },
            { "Jwt:RefreshTokenExpirationDays", "30" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _tokenService = new JwtTokenService(_configuration);
    }

    private static User CreateTestUser()
    {
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var user = new User(Guid.NewGuid(), tenantId, orgId, "manager_user", "manager@example.com", "hash");

        var role = new Role(Guid.NewGuid(), tenantId, "Manager");
        role.AddPermission(new Permission(Guid.NewGuid(), "Order.Create"));
        role.AddPermission(new Permission(Guid.NewGuid(), "Bill.Void"));
        user.AddRole(role);

        return user;
    }

    [Fact]
    public void GenerateTokens_ShouldReturnValidJwtAndRefreshToken()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = _tokenService.GenerateTokens(user);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.AccessTokenExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        result.RefreshTokenExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);

        // Validate JWT claims
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.AccessToken);

        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().Contain("TestAudience");

        var tenantClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        var orgClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "org_id")?.Value;
        var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub" || c.Type == "nameid")?.Value;
        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;

        tenantClaim.Should().Be(user.TenantId.ToString());
        orgClaim.Should().Be(user.OrganizationId.ToString());
        subClaim.Should().Be(user.UserId.ToString());
        roleClaim.Should().Be("Manager");
    }

    [Fact]
    public void HashRefreshToken_ShouldProduceConsistentHash()
    {
        // Arrange
        var rawToken = _tokenService.GenerateRefreshTokenValue();

        // Act
        var hash1 = _tokenService.HashRefreshToken(rawToken);
        var hash2 = _tokenService.HashRefreshToken(rawToken);

        // Assert
        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().Be(hash2);
    }
}
