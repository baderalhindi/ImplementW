namespace PMPlatform.Domain.Common;

/// <summary>
/// A SAR amount (ADR-008; ERD D-5). The currency is this type's constant: there is no currency column,
/// no conversion and no selector. Maps to one <c>numeric(18,2)</c> column whose name ends in <c>_sar</c>.
/// </summary>
public readonly record struct Money
{
    public const string CurrencyCode = "SAR";
    private const int Scale = 2;
    private const decimal MaxAbsolute = 9_999_999_999_999_999.99m;

    public static readonly Money Zero = new(0m);

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (decimal.Round(amount, Scale) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, $"{CurrencyCode} amounts carry at most {Scale} decimal places.");
        }

        if (Math.Abs(amount) > MaxAbsolute)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount exceeds numeric(18,2).");
        }

        Amount = amount;
    }

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount);

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public override string ToString() => $"{Amount:0.00} {CurrencyCode}";
}
