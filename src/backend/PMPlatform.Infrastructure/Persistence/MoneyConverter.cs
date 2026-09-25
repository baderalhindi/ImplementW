using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>Stores a <see cref="Money"/> as its amount in one <c>numeric(18,2)</c> column; the currency is the type's constant (ADR-008, ERD D-5).</summary>
internal sealed class MoneyConverter() : ValueConverter<Money, decimal>(
    money => money.Amount,
    amount => new Money(amount));
