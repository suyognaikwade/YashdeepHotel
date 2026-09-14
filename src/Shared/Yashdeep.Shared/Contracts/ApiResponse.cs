using System;
using Yashdeep.Shared.Results;

namespace Yashdeep.Shared.Contracts;

/// <summary>
/// Standard API response envelope contract for all REST API endpoints.
/// </summary>
/// <typeparam name="TData">The response data payload type.</typeparam>
public class ApiResponse<TData>
{
    public bool Success { get; set; }
    public TData? Data { get; set; }
    public ApiErrorResponse? Error { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public static ApiResponse<TData> Ok(TData data, string? correlationId = null) => new()
    {
        Success = true,
        Data = data,
        Error = null,
        CorrelationId = correlationId,
        TimestampUtc = DateTime.UtcNow
    };

    public static ApiResponse<TData> Fail(Error error, string? correlationId = null) => new()
    {
        Success = false,
        Data = default,
        Error = ApiErrorResponse.FromError(error),
        CorrelationId = correlationId,
        TimestampUtc = DateTime.UtcNow
    };
}

/// <summary>
/// Serializable API error contract representation.
/// </summary>
public class ApiErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = nameof(ErrorType.Failure);
    public int NumericCode { get; set; } = 400;
    public System.Collections.Generic.Dictionary<string, string[]>? Details { get; set; }

    public static ApiErrorResponse FromError(Error error) => new()
    {
        Code = error.Code,
        Message = error.Message,
        Type = error.Type.ToString(),
        NumericCode = error.NumericCode,
        Details = error.Details != null ? new System.Collections.Generic.Dictionary<string, string[]>(error.Details) : null
    };

    public Error ToError() => new(
        Code,
        Message,
        Enum.TryParse<ErrorType>(Type, true, out var parsedType) ? parsedType : ErrorType.Failure,
        NumericCode,
        Details);
}
