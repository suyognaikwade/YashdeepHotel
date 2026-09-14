using Yashdeep.Application.Common.Interfaces;

namespace Yashdeep.Persistence.Local.Security;

/// <summary>
/// Retrieves the SQLite encryption key from an environment variable.
/// Does not store secrets in source code or configuration files.
/// </summary>
public class EnvironmentVariableKeyProvider : ISQLiteKeyProvider
{
    private readonly string _environmentVariableName;

    public EnvironmentVariableKeyProvider(string environmentVariableName = "YASHDEEP_SQLITE_KEY")
    {
        if (string.IsNullOrWhiteSpace(environmentVariableName))
        {
            throw new ArgumentException("Environment variable name cannot be null or empty.", nameof(environmentVariableName));
        }

        _environmentVariableName = environmentVariableName;
    }

    public Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        var key = Environment.GetEnvironmentVariable(_environmentVariableName);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"SQLite encryption key was not found in environment variable '{_environmentVariableName}'. " +
                "Ensure the key is supplied via secure environment configuration.");
        }

        return Task.FromResult(key);
    }
}
