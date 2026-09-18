using Yashdeep.Domain.Entities;

namespace Yashdeep.Application.Interfaces;

public class AuthTokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
}

public interface ITokenService
{
    AuthTokenResult GenerateTokens(User user);
    string GenerateRefreshTokenValue();
    string HashRefreshToken(string refreshTokenValue);
}
