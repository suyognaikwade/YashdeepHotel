using System;
using Yashdeep.Domain.Enums;

namespace Yashdeep.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    TenantStatus? Status { get; }
    bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;
}

public interface ITenantContextSetter
{
    void SetTenantContext(Guid tenantId, TenantStatus status);
}
