using System.Diagnostics.CodeAnalysis;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>How a <see cref="ConfigurationValue"/>'s text is read.</summary>
[SuppressMessage("Naming", "CA1720", Justification = "The members are the stored values the ERD names: INTEGER, DECIMAL, BOOLEAN, TEXT, DURATION_DAYS, PERCENT.")]
public enum ConfigurationValueType
{
    Integer = 1,
    Decimal = 2,
    Boolean = 3,
    Text = 4,
    DurationDays = 5,
    Percent = 6,
}
