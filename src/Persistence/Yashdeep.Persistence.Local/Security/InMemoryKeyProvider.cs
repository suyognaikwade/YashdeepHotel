using Yashdeep.Application.Common.Interfaces;

namespace Yashdeep.Persistence.Local.Security;

/// <summary>
/// In-memory key provider used primarily for automated testing and isolated edge runtime scenarios.
/// </summary>
public class InMemoryKeyProvider : ISQLiteKeyProvider
{
    private readonly string _key;

    public InMemoryKeyProvider(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Encryption key cannot be null or empty.", nameof(key));
        }

        _key = key;
    }

    public Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_key);
    }
}
