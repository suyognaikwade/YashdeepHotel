using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public AuthTokenResult GenerateTokens(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwtSecret = _configuration["Jwt:SecretKey"] ?? "SUPER_SECRET_DEV_KEY_DO_NOT_USE_IN_PRODUCTION_32BYTES_MIN!";
        var issuer = _configuration["Jwt:Issuer"] ?? "YashdeepSaaS";
        var audience = _configuration["Jwt:Audience"] ?? "YashdeepClientTerminals";
        var accessExpiryMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 15;
        var refreshExpiryDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 30;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new("sub", user.UserId.ToString()),
            new("tenant_id", user.TenantId.ToString()),
            new("org_id", user.OrganizationId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email)
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name));
            foreach (var permission in role.Permissions)
            {
                claims.Add(new Claim("permission", permission.Code));
            }
        }

        var accessExpiresAtUtc = DateTime.UtcNow.AddMinutes(accessExpiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessExpiresAtUtc,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var refreshTokenValue = GenerateRefreshTokenValue();
        var refreshExpiresAtUtc = DateTime.UtcNow.AddDays(refreshExpiryDays);

        return new AuthTokenResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAtUtc = accessExpiresAtUtc,
            RefreshTokenExpiresAtUtc = refreshExpiresAtUtc
        };
    }

    public string GenerateRefreshTokenValue()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashRefreshToken(string refreshTokenValue)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
            throw new ArgumentException("Refresh token value cannot be empty.", nameof(refreshTokenValue));

        byte[] bytes = Encoding.UTF8.GetBytes(refreshTokenValue);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
