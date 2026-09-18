using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Common.Interfaces;
using Yashdeep.Application.Common.Options;

namespace Yashdeep.Persistence.Local;

/// <summary>
/// Factory for creating encrypted LocalDbContext instances configured with SQLCipher password and SQLite options.
/// </summary>
public class LocalDbContextFactory : IDbContextFactory<LocalDbContext>
{
    private readonly LocalDatabaseOptions _options;
    private readonly ISQLiteKeyProvider _keyProvider;
    private readonly Guid? _currentTenantId;

    public LocalDbContextFactory(
        LocalDatabaseOptions options,
        ISQLiteKeyProvider keyProvider,
        Guid? currentTenantId = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _currentTenantId = currentTenantId;
    }

    public LocalDbContext CreateDbContext()
    {
        return CreateDbContextAsync().GetAwaiter().GetResult();
    }

    public async Task<LocalDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var encryptionKey = await _keyProvider.GetEncryptionKeyAsync(cancellationToken);
        var dbPath = _options.GetFullDatabasePath();

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Password = encryptionKey,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };

        var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();
        optionsBuilder.UseSqlite(connectionStringBuilder.ConnectionString);

        return new LocalDbContext(optionsBuilder.Options, _currentTenantId);
    }
}
