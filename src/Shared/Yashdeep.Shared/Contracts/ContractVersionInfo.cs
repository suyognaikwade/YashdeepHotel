using System;

namespace Yashdeep.Shared.Contracts;

/// <summary>
/// Explicit contract versioning attribute to tag contract classes with semver expectations.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class ContractVersionAttribute : Attribute
{
    public string Version { get; }
    public string Area { get; }

    public ContractVersionAttribute(string version, string area = "General")
    {
        Version = version;
        Area = area;
    }
}

/// <summary>
/// Platform contract versioning information constants.
/// </summary>
public static class ApiVersionInfo
{
    public const string CurrentVersion = "v1.0";
    public const string OutboxProtocolVersion = "v1.0";
    public const string EntitlementProtocolVersion = "v1.0";
}
