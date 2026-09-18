namespace Yashdeep.Domain.ValueObjects;

public record TenantContext(Guid TenantId, string LegalName);
public record BranchContext(Guid BranchId, Guid LocationId, string BranchName);
