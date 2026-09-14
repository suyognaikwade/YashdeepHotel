namespace Yashdeep.Domain.Connectivity;

/// <summary>
/// Operational connectivity states required by the system.
/// </summary>
public enum ConnectivityState
{
    /// <summary>
    /// Operating offline within the valid mandatory weekly window (0 to 168 hours).
    /// </summary>
    NormalOfflineOperation = 1,

    /// <summary>
    /// Approaching the mandatory weekly check-in boundary (e.g. 120 to 168 hours offline).
    /// </summary>
    ConnectivityWarning = 2,

    /// <summary>
    /// Continuous offline duration exceeds mandatory weekly boundary (168 hours).
    /// </summary>
    ConnectivityExpiry = 3,

    /// <summary>
    /// Offline duration has exceeded mandatory window and configured grace period without check-in.
    /// Operational lockout enabled; read-only operations permitted.
    /// </summary>
    RestrictedOperation = 4,

    /// <summary>
    /// Offline batch upload or sync connection encountered failures.
    /// </summary>
    SynchronizationFailure = 5,

    /// <summary>
    /// Cloud Web API mandates client software version update before continuing transactional operations.
    /// </summary>
    UpdateRequirement = 6,

    /// <summary>
    /// Cloud administrator has suspended or revoked device authorization.
    /// </summary>
    DeviceSuspension = 7,

    /// <summary>
    /// Transition state upon successful online check-in after expiry, warning, or failure.
    /// </summary>
    Recovery = 8
}
