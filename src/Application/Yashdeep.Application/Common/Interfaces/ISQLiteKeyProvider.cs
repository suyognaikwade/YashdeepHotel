namespace Yashdeep.Application.Common.Interfaces;

/// <summary>
/// Abstraction for acquiring the SQLite SQLCipher database encryption key securely.
/// Implementations acquire keys from OS secure storage, environment variables, or key vaults.
/// </summary>
public interface ISQLiteKeyProvider
{
    /// <summary>
    /// Asynchronously retrieves the SQLite encryption key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw encryption key string.</returns>
    Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default);
}
