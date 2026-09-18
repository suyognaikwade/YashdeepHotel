using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Yashdeep.Persistence.Cloud;

public class CloudDbContextFactory : IDesignTimeDbContextFactory<CloudDbContext>
{
    public CloudDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<CloudDbContext>();
        builder.UseNpgsql("Host=localhost;Database=yashdeep_cloud_db;Username=postgres;Password=postgres");

        return new CloudDbContext(builder.Options);
    }
}
