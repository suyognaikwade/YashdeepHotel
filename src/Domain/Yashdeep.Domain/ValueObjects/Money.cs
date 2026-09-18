namespace Yashdeep.Domain.ValueObjects;

public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "INR")
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Money amount cannot be negative.");

        Amount = amount;
        Currency = currency ?? "INR";
    }

    public static Money Zero => new(0m);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot add money with different currencies: {a.Currency} vs {b.Currency}");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot subtract money with different currencies: {a.Currency} vs {b.Currency}");
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator *(Money a, decimal multiplier)
    {
        return new Money(a.Amount * multiplier, a.Currency);
    }

    public static Money operator *(decimal multiplier, Money a)
    {
        return a * multiplier;
    }

    public Money Round()
    {
        return new Money(Math.Round(Amount, 2, MidpointRounding.AwayFromZero), Currency);
    }

    public Money RoundToNearestRupee()
    {
        return new Money(Math.Round(Amount, 0, MidpointRounding.AwayFromZero), Currency);
    }

    public override string ToString() => $"{Currency} {Amount:F2}";
}
