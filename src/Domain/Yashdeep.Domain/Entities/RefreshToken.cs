namespace Yashdeep.Domain.Entities;

public class RefreshToken
{
    public Guid RefreshTokenId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string CreatedByIp { get; private set; } = string.Empty;

    private RefreshToken() { }

    public RefreshToken(Guid refreshTokenId, Guid userId, Guid tenantId, string tokenHash, DateTime expiresAtUtc, string createdByIp = "")
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("Token hash cannot be empty.", nameof(tokenHash));

        RefreshTokenId = refreshTokenId == Guid.Empty ? Guid.NewGuid() : refreshTokenId;
        UserId = userId;
        TenantId = tenantId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        IsRevoked = false;
        CreatedByIp = createdByIp ?? string.Empty;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(DateTime? revokedAtUtc = null)
    {
        IsRevoked = true;
        RevokedAtUtc = revokedAtUtc ?? DateTime.UtcNow;
    }
}
