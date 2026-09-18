using System;

namespace Yashdeep.Application.Common.Interfaces
{
    public interface ITenantContext
    {
        Guid TenantId { get; }
        Guid BranchId { get; }
    }

    public sealed class TenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }
        public Guid BranchId { get; set; }

        public TenantContext(Guid tenantId, Guid branchId = default)
        {
            TenantId = tenantId;
            BranchId = branchId;
        }
    }
}
