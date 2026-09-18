namespace Yashdeep.Domain.Connectivity;

/// <summary>
/// Configurable policy parameters for connectivity and entitlement evaluation.
/// Note: Mandatory weekly connectivity limit is 7 days (168 hours).
/// Additional soft grace period duration remains configurable per Product Owner decision and is not hardcoded to 14 days.
/// </summary>
public class ConnectivityPolicyOptions
{
    /// <summary>
    /// Mandatory check-in window duration (Default: 7 days / 168 hours).
    /// </summary>
    public TimeSpan MandatoryCheckInWindow { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Duration offline after which non-blocking UI warning is triggered (Default: 5 days / 120 hours).
    /// </summary>
    public TimeSpan WarningThreshold { get; set; } = TimeSpan.FromDays(5);

    /// <summary>
    /// Additional soft grace period duration beyond the mandatory 7 days.
    /// Configurable per Product Owner decision. Defaults to 7 days if unspecified.
    /// </summary>
    public TimeSpan SoftGracePeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Maximum allowed backward clock delta before flag as clock tampering.
    /// </summary>
    public TimeSpan MaxAllowedClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}
