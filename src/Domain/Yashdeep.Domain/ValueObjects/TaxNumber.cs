using System.Text.RegularExpressions;
using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class TaxNumber : ValueObject
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public string Value { get; }

    public TaxNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Tax number (GSTIN) cannot be empty.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (!GstinRegex.IsMatch(normalized))
        {
            throw new DomainException($"Invalid GSTIN format: '{value}'. Expected format e.g. 27AAAAA0000A1Z5.");
        }

        Value = normalized;
    }

    public static TaxNumber Create(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
