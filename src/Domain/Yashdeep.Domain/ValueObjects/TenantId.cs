using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class TenantId : ValueObject
{
    public Guid Value { get; }

    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("TenantId cannot be empty.");
        }

        Value = value;
    }

    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId Create(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
