namespace Yashdeep.Application.Common.Options;

/// <summary>
/// Configuration options for the local edge SQLite SQLCipher persistence layer.
/// </summary>
public class LocalDatabaseOptions
{
    /// <summary>
    /// Directory path where the local SQLite database file is stored.
    /// If null or empty, uses the execution directory or current working directory.
    /// </summary>
    public string DatabaseDirectory { get; set; } = string.Empty;

    /// <summary>
    /// File name for the local SQLite database.
    /// Defaults to 'yashdeep_edge.db'.
    /// </summary>
    public string DatabaseFileName { get; set; } = "yashdeep_edge.db";

    /// <summary>
    /// Whether to enforce Write-Ahead Logging (WAL) mode upon initialization.
    /// Defaults to true for high concurrency edge POS operations.
    /// </summary>
    public bool EnableWalMode { get; set; } = true;

    /// <summary>
    /// Whether to automatically create the database directory if it does not exist.
    /// </summary>
    public bool EnsureDirectoryExists { get; set; } = true;

    /// <summary>
    /// Computes the full absolute or relative path to the local database file.
    /// </summary>
    public string GetFullDatabasePath()
    {
        if (string.IsNullOrWhiteSpace(DatabaseDirectory))
        {
            return DatabaseFileName;
        }

        return Path.Combine(DatabaseDirectory, DatabaseFileName);
    }
}
