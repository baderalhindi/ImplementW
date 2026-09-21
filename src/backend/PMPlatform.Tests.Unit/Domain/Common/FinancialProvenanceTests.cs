using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Domain.Common;

public sealed class FinancialProvenanceTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateOnly AsOf = new(2026, 9, 21);

    [Fact]
    public void ManualEntryNeedsNoSourceReference()
    {
        var provenance = new FinancialProvenance(FinancialSourceType.Manual, null, AsOf, User);
        Assert.Null(provenance.SourceReference);
    }

    [Theory]
    [InlineData(FinancialSourceType.Etimad)]
    [InlineData(FinancialSourceType.Other)]
    public void ExternalSourceRequiresASourceReference(FinancialSourceType sourceType)
    {
        Assert.Throws<ArgumentException>(() => new FinancialProvenance(sourceType, " ", AsOf, User));
        Assert.Equal("ETM-2026-0042", new FinancialProvenance(sourceType, "ETM-2026-0042", AsOf, User).SourceReference);
    }

    [Fact]
    public void EnteringUserIsRequired()
    {
        Assert.Throws<ArgumentException>(() => new FinancialProvenance(FinancialSourceType.Manual, null, AsOf, Guid.Empty));
    }
}
