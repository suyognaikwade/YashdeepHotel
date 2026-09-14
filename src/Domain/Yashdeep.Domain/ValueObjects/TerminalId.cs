using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class TerminalId : ValueObject
{
    public Guid Value { get; }

    public TerminalId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("TerminalId cannot be empty.");
        }

        Value = value;
    }

    public static TerminalId New() => new(Guid.NewGuid());

    public static TerminalId Create(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}
