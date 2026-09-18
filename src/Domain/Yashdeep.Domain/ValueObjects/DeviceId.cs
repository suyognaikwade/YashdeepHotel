using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class DeviceId : ValueObject
{
    public Guid Value { get; }

    public DeviceId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("DeviceId cannot be empty.");
        }

        Value = value;
    }

    public static DeviceId New() => new(Guid.NewGuid());

    public static DeviceId Create(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
