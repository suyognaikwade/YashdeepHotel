using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Yashdeep.Application.Common.Interfaces;

namespace Yashdeep.Persistence.Cloud.Interceptors;

public class NpgsqlTenantInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public NpgsqlTenantInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.HasTenant)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{_tenantContext.TenantId}';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (_tenantContext.HasTenant)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{_tenantContext.TenantId}';";
            cmd.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }
}
