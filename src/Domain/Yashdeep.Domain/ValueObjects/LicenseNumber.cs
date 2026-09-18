using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class LicenseNumber : ValueObject
{
    public string Value { get; }

    public LicenseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("License number cannot be empty.");
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public static LicenseNumber Create(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
