using Yashdeep.Application.Common.Interfaces;

namespace Yashdeep.Persistence.Local.Security;

/// <summary>
/// Abstract base class for OS-native secure storage key providers
/// (e.g., Windows Credential Manager / Android KeyStore / MAUI SecureStorage).
/// </summary>
public abstract class SecureStorageKeyProviderBase : ISQLiteKeyProvider
{
    protected const string DefaultKeyAlias = "yashdeep_sqlite_passphrase";

    protected string KeyAlias { get; }

    protected SecureStorageKeyProviderBase(string keyAlias = DefaultKeyAlias)
    {
        KeyAlias = string.IsNullOrWhiteSpace(keyAlias) ? DefaultKeyAlias : keyAlias;
    }

    public async Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        var existingKey = await ReadFromSecureStorageAsync(KeyAlias, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingKey))
        {
            return existingKey;
        }

        var newKey = GenerateCryptographicKey();
        await SaveToSecureStorageAsync(KeyAlias, newKey, cancellationToken);
        return newKey;
    }

    protected abstract Task<string?> ReadFromSecureStorageAsync(string keyAlias, CancellationToken cancellationToken);
    protected abstract Task SaveToSecureStorageAsync(string keyAlias, string key, CancellationToken cancellationToken);

    protected virtual string GenerateCryptographicKey()
    {
        var buffer = new byte[32]; // 256 bits
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return Convert.ToBase64String(buffer);
    }
}
