namespace Yashdeep.Domain.Entities;

public class User
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PinCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private readonly List<Role> _roles = new();
    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

    private readonly List<RefreshToken> _refreshTokens = new();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    private User() { }

    public User(
        Guid userId,
        Guid tenantId,
        Guid organizationId,
        string username,
        string email,
        string passwordHash,
        string fullName = "",
        string pinCode = "",
        bool isActive = true)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (organizationId == Guid.Empty) throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username cannot be empty.", nameof(username));

        UserId = userId == Guid.Empty ? Guid.NewGuid() : userId;
        TenantId = tenantId;
        OrganizationId = organizationId;
        Username = username.Trim();
        Email = email?.Trim() ?? string.Empty;
        PasswordHash = passwordHash ?? string.Empty;
        FullName = fullName ?? string.Empty;
        PinCode = pinCode ?? string.Empty;
        IsActive = isActive;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetPasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AddRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (role.TenantId != TenantId)
            throw new InvalidOperationException("Cannot assign a role belonging to a different tenant.");

        if (!_roles.Any(r => r.RoleId == role.RoleId))
        {
            _roles.Add(role);
        }
    }

    public void AddRefreshToken(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        if (refreshToken.TenantId != TenantId)
            throw new InvalidOperationException("Cannot assign refresh token belonging to a different tenant.");

        _refreshTokens.Add(refreshToken);
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
