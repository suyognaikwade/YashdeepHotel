namespace Yashdeep.Application.Common.Interfaces;

/// <summary>
/// Abstraction for initializing the local edge database, setting up encryption keys,
/// enabling Write-Ahead Logging (WAL) mode, and applying schema migrations.
/// </summary>
public interface ILocalDatabaseInitializer
{
    /// <summary>
    /// Initializes the local database, ensures directories exist, applies encryption key,
    /// configures WAL mode, and ensures initial schema creation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether SQLCipher native encryption was verified at runtime.
    /// </summary>
    bool IsEncryptionVerified { get; }

    /// <summary>
    /// Detailed runtime verification status and limitations regarding SQLCipher execution environment.
    /// </summary>
    string VerificationNotes { get; }
}
