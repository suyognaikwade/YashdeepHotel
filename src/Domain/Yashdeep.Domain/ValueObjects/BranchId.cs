using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class BranchId : ValueObject
{
    public Guid Value { get; }

    public BranchId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("BranchId cannot be empty.");
        }

        Value = value;
    }

    public static BranchId New() => new(Guid.NewGuid());

    public static BranchId Create(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
