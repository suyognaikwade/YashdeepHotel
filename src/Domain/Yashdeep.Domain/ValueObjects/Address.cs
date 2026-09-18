using Yashdeep.Domain.Common;
using Yashdeep.Domain.Common.Exceptions;

namespace Yashdeep.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string StreetLine1 { get; }
    public string? StreetLine2 { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(
        string streetLine1,
        string? streetLine2,
        string city,
        string state,
        string postalCode,
        string country = "India")
    {
        if (string.IsNullOrWhiteSpace(streetLine1))
        {
            throw new DomainException("Street line 1 is required.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainException("City is required.");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new DomainException("State is required.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new DomainException("Postal code is required.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new DomainException("Country is required.");
        }

        StreetLine1 = streetLine1.Trim();
        StreetLine2 = streetLine2?.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StreetLine1;
        yield return StreetLine2;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(StreetLine2)
            ? $"{StreetLine1}, {City}, {State} {PostalCode}, {Country}"
            : $"{StreetLine1}, {StreetLine2}, {City}, {State} {PostalCode}, {Country}";
}
