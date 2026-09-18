using System;

namespace Yashdeep.Shared.Contracts;

/// <summary>
/// Correlation context for cross-boundary request tracing and log diagnostics.
/// </summary>
public sealed class CorrelationContext
{
    public string CorrelationId { get; }
    public string? CausationId { get; set; }
    public string? TraceParent { get; set; }

    public CorrelationContext(string? correlationId = null, string? causationId = null, string? traceParent = null)
    {
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("D") : correlationId;
        CausationId = causationId;
        TraceParent = traceParent;
    }

    public static CorrelationContext CreateNew() => new();
}
