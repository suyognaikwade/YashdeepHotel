using Yashdeep.Persistence.Local.Security;
using Xunit;

namespace Yashdeep.Persistence.Local.Tests;

public class EncryptionKeyProviderTests
{
    [Fact]
    public async Task InMemoryKeyProvider_ReturnsConfiguredKey()
    {
        // Arrange
        const string expectedKey = "SecretTestPassphrase123!";
        var provider = new InMemoryKeyProvider(expectedKey);

        // Act
        var key = await provider.GetEncryptionKeyAsync();

        // Assert
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void InMemoryKeyProvider_ThrowsArgumentException_WhenKeyIsNullOrEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new InMemoryKeyProvider(""));
        Assert.Throws<ArgumentException>(() => new InMemoryKeyProvider("   "));
    }

    [Fact]
    public async Task EnvironmentVariableKeyProvider_ReturnsKeyFromEnvironmentVariable()
    {
        // Arrange
        const string envVarName = "TEST_YASHDEEP_SQLITE_KEY";
        const string expectedKey = "EnvSecretPassphrase456!";
        Environment.SetEnvironmentVariable(envVarName, expectedKey);

        try
        {
            var provider = new EnvironmentVariableKeyProvider(envVarName);

            // Act
            var key = await provider.GetEncryptionKeyAsync();

            // Assert
            Assert.Equal(expectedKey, key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public async Task EnvironmentVariableKeyProvider_ThrowsInvalidOperationException_WhenEnvVarMissing()
    {
        // Arrange
        const string envVarName = "NON_EXISTENT_ENV_KEY_VAR_99";
        Environment.SetEnvironmentVariable(envVarName, null);

        var provider = new EnvironmentVariableKeyProvider(envVarName);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetEncryptionKeyAsync());
    }

    [Fact]
    public async Task MockSecureStorageKeyProvider_GeneratesAndStoresNewKey()
    {
        // Arrange
        var mockStorage = new TestSecureStorageKeyProvider();

        // Act
        var key1 = await mockStorage.GetEncryptionKeyAsync();
        var key2 = await mockStorage.GetEncryptionKeyAsync();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(key1));
        Assert.Equal(key1, key2); // Subsequent call returns stored key
    }

    private class TestSecureStorageKeyProvider : SecureStorageKeyProviderBase
    {
        private string? _storedKey;

        protected override Task<string?> ReadFromSecureStorageAsync(string keyAlias, CancellationToken cancellationToken)
        {
            return Task.FromResult(_storedKey);
        }

        protected override Task SaveToSecureStorageAsync(string keyAlias, string key, CancellationToken cancellationToken)
        {
            _storedKey = key;
            return Task.CompletedTask;
        }
    }
}
