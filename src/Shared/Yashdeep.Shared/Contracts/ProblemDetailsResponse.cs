using System;
using System.Collections.Generic;

namespace Yashdeep.Shared.Contracts;

/// <summary>
/// Standard RFC 7807 Problem Details representation for HTTP API error responses.
/// </summary>
public class ProblemDetailsResponse
{
    public string Type { get; set; } = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
    public string Title { get; set; } = "An error occurred while processing your request.";
    public int Status { get; set; } = 400;
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public Dictionary<string, object?> Extensions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string[]>? Errors { get; set; }
}
