using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class OrganizationId : ValueObject
{
    public Guid Value { get; }

    public OrganizationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("OrganizationId cannot be empty.");
        }

        Value = value;
    }

    public static OrganizationId New() => new(Guid.NewGuid());

    public static OrganizationId Create(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
