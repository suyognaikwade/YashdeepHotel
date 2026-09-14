namespace Yashdeep.Application.Entitlements;

public interface ITenantContext
{
    string? TenantId { get; }
}
