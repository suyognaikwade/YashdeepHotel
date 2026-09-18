using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Yashdeep.Domain.Contexts;

namespace Yashdeep.Persistence.Cloud.Interceptors;

public class CloudTenantInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public CloudTenantInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.IsTenantResolved)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{_tenantContext.TenantId}';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (_tenantContext.IsTenantResolved)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET LOCAL app.current_tenant_id = '{_tenantContext.TenantId}';";
            cmd.ExecuteNonQuery();
        }

        base.ConnectionOpened(connection, eventData);
    }
}
