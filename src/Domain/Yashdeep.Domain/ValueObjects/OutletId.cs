using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class OutletId : ValueObject
{
    public Guid Value { get; }

    public OutletId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("OutletId cannot be empty.");
        }

        Value = value;
    }

    public static OutletId New() => new(Guid.NewGuid());

    public static OutletId Create(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
