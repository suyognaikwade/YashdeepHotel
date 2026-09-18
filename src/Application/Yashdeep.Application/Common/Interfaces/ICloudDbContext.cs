using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Application.Common.Interfaces;

public interface ICloudDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Outlet> Outlets { get; }
    DbSet<Terminal> Terminals { get; }
    DbSet<Device> Devices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
