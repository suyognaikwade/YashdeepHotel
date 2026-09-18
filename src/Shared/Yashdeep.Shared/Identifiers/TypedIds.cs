using System;
using System.Text.Json.Serialization;
using Yashdeep.Shared.Serialization;

namespace Yashdeep.Shared.Identifiers;

/// <summary>
/// Strongly typed identifier representing a Tenant (Subscription / Business Entity).
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<TenantId>))]
public readonly record struct TenantId(Guid Value) : IStronglyTypedId, IComparable<TenantId>
{
    public static TenantId Empty => new(Guid.Empty);
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId FromGuid(Guid value) => new(value);
    public static TenantId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out TenantId tenantId)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            tenantId = new TenantId(parsed);
            return true;
        }

        tenantId = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is TenantId id && id.Value == Value;
    public int CompareTo(TenantId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(TenantId id) => id.Value;
    public static explicit operator TenantId(Guid value) => new(value);
}

/// <summary>
/// Strongly typed identifier representing an Organization (Legal Entity).
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<OrganizationId>))]
public readonly record struct OrganizationId(Guid Value) : IStronglyTypedId, IComparable<OrganizationId>
{
    public static OrganizationId Empty => new(Guid.Empty);
    public static OrganizationId New() => new(Guid.NewGuid());
    public static OrganizationId FromGuid(Guid value) => new(value);
    public static OrganizationId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out OrganizationId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new OrganizationId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is OrganizationId id && id.Value == Value;
    public int CompareTo(OrganizationId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(OrganizationId id) => id.Value;
    public static explicit operator OrganizationId(Guid value) => new(value);
}

/// <summary>
/// Strongly typed identifier representing a Branch (Physical Property Location).
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<BranchId>))]
public readonly record struct BranchId(Guid Value) : IStronglyTypedId, IComparable<BranchId>
{
    public static BranchId Empty => new(Guid.Empty);
    public static BranchId New() => new(Guid.NewGuid());
    public static BranchId FromGuid(Guid value) => new(value);
    public static BranchId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out BranchId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new BranchId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is BranchId id && id.Value == Value;
    public int CompareTo(BranchId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(BranchId id) => id.Value;
    public static explicit operator BranchId(Guid value) => new(value);
}

/// <summary>
/// Strongly typed identifier representing an Outlet (Cost Center, e.g. Main Dining, Bar, Permit Room).
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<OutletId>))]
public readonly record struct OutletId(Guid Value) : IStronglyTypedId, IComparable<OutletId>
{
    public static OutletId Empty => new(Guid.Empty);
    public static OutletId New() => new(Guid.NewGuid());
    public static OutletId FromGuid(Guid value) => new(value);
    public static OutletId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out OutletId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new OutletId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is OutletId id && id.Value == Value;
    public int CompareTo(OutletId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(OutletId id) => id.Value;
    public static explicit operator OutletId(Guid value) => new(value);
}

/// <summary>
/// Strongly typed identifier representing a Terminal / Workstation.
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<TerminalId>))]
public readonly record struct TerminalId(Guid Value) : IStronglyTypedId, IComparable<TerminalId>
{
    public static TerminalId Empty => new(Guid.Empty);
    public static TerminalId New() => new(Guid.NewGuid());
    public static TerminalId FromGuid(Guid value) => new(value);
    public static TerminalId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out TerminalId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new TerminalId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is TerminalId id && id.Value == Value;
    public int CompareTo(TerminalId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(TerminalId id) => id.Value;
    public static explicit operator TerminalId(Guid value) => new(value);
}

/// <summary>
/// Strongly typed identifier representing a Registered Edge Device Instance.
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<DeviceId>))]
public readonly record struct DeviceId(Guid Value) : IStronglyTypedId, IComparable<DeviceId>
{
    public static DeviceId Empty => new(Guid.Empty);
    public static DeviceId New() => new(Guid.NewGuid());
    public static DeviceId FromGuid(Guid value) => new(value);
    public static DeviceId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out DeviceId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new DeviceId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is DeviceId id && id.Value == Value;
    public int CompareTo(DeviceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(DeviceId id) => id.Value;
    public static explicit operator DeviceId(Guid value) => new(value);
}

/// <summary>
/// Strongly typed identifier representing a User / Staff Member.
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<UserId>))]
public readonly record struct UserId(Guid Value) : IStronglyTypedId, IComparable<UserId>
{
    public static UserId Empty => new(Guid.Empty);
    public static UserId New() => new(Guid.NewGuid());
    public static UserId FromGuid(Guid value) => new(value);
    public static UserId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out UserId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new UserId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is UserId id && id.Value == Value;
    public int CompareTo(UserId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(UserId id) => id.Value;
    public static explicit operator UserId(Guid value) => new(value);
}

/// <summary>
/// Generic strongly typed identifier representing any Aggregate Root.
/// </summary>
[JsonConverter(typeof(StronglyTypedIdJsonConverter<AggregateId>))]
public readonly record struct AggregateId(Guid Value) : IStronglyTypedId, IComparable<AggregateId>
{
    public static AggregateId Empty => new(Guid.Empty);
    public static AggregateId New() => new(Guid.NewGuid());
    public static AggregateId FromGuid(Guid value) => new(value);
    public static AggregateId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string? value, out AggregateId id)
    {
        if (Guid.TryParse(value, out var parsed))
        {
            id = new AggregateId(parsed);
            return true;
        }

        id = Empty;
        return false;
    }

    public bool IsEmpty => Value == Guid.Empty;
    public bool Equals(IStronglyTypedId? other) => other is AggregateId id && id.Value == Value;
    public int CompareTo(AggregateId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("D");

    public static implicit operator Guid(AggregateId id) => id.Value;
    public static explicit operator AggregateId(Guid value) => new(value);
}
