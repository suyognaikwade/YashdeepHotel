using System.Collections.Generic;

namespace Yashdeep.Shared.Results;

/// <summary>
/// Represents standard error classification types across API and domain boundaries.
/// </summary>
public enum ErrorType
{
    None = 0,
    Failure = 1,
    Unexpected = 2,
    Validation = 3,
    NotFound = 4,
    Conflict = 5,
    Unauthorized = 6,
    Forbidden = 7,
    Concurrency = 8
}

/// <summary>
/// Standard immutable Error record for cross-boundary result and API error messaging.
/// </summary>
public readonly record struct Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Failure,
    int NumericCode = 400,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    /// <summary>
    /// Represents no error (Empty/None).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None, 0);

    /// <summary>
    /// Represents a generic null value error.
    /// </summary>
    public static readonly Error NullValue = new("Error.NullValue", "The requested value was null.", ErrorType.Failure, 400);

    public static Error Failure(string code, string message, int numericCode = 400) =>
        new(code, message, ErrorType.Failure, numericCode);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation, 400, details);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound, 404);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict, 409);

    public static Error Unauthorized(string code, string message) =>
        new(code, message, ErrorType.Unauthorized, 401);

    public static Error Forbidden(string code, string message) =>
        new(code, message, ErrorType.Forbidden, 403);

    public static Error Concurrency(string code, string message) =>
        new(code, message, ErrorType.Concurrency, 412);

    public static Error Unexpected(string code, string message) =>
        new(code, message, ErrorType.Unexpected, 500);
}
