namespace Yashdeep.Domain.Contracts;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public interface IBranchEntity : ITenantEntity
{
    Guid BranchId { get; set; }
}

public interface IOutletEntity : IBranchEntity
{
    Guid OutletId { get; set; }
}
