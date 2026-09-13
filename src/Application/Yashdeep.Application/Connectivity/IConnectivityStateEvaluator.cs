namespace Yashdeep.Application.Connectivity;

using Yashdeep.Domain.Connectivity;

/// <summary>
/// Interface for evaluating the operational connectivity state of an installation/device.
/// Governed by Product Owner mandatory 7-day rule, configurable warning threshold, and configurable grace period.
/// </summary>
public interface IConnectivityStateEvaluator
{
    /// <summary>
    /// Evaluates current operational connectivity state based on last check-in record, current system/monotonic time, and evaluation context.
    /// </summary>
    ConnectivityStateResult EvaluateState(ConnectivityEvaluationContext context);
}
