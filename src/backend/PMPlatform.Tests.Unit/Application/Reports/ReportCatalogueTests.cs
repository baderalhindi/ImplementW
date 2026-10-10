using PMPlatform.Application.Features.Reports;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Tests.Unit.Application.Reports;

/// <summary>ADR-006 MAPPED and the participation amendment: ten reports absorb FG-02's catalogue; ADR-005's three formats; ADR-013's entity set.</summary>
public sealed class ReportCatalogueTests
{
    [Fact]
    public void TheCatalogueIsTenReportsInThreeFormats()
    {
        Assert.Equal(10, ReportCatalogue.Codes.Count);
        Assert.Equal([ReportExportFormat.Pdf, ReportExportFormat.Xlsx, ReportExportFormat.Csv], ReportCatalogue.ExportFormats.Order());
    }

    /// <summary>The twenty-six entries of FG-02 §5.1: twenty-three absorbed once each, three not delivered by any report.</summary>
    [Fact]
    public void EveryCatalogueEntryIsAbsorbedOnceOrIsNamedAsNotDelivered()
    {
        string[] absorbed = [.. ReportCatalogue.CatalogueEntries.Values.SelectMany(e => e)];

        Assert.Equal(ReportCatalogue.Codes.Order(), ReportCatalogue.CatalogueEntries.Keys.Order());
        Assert.Equal(23, absorbed.Length);
        Assert.Equal(absorbed.Length, absorbed.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(absorbed.Intersect(ReportCatalogue.NotDelivered, StringComparer.Ordinal));
        Assert.Equal(26, absorbed.Concat(ReportCatalogue.NotDelivered).Distinct(StringComparer.Ordinal).Count());
        Assert.All(absorbed.Concat(ReportCatalogue.NotDelivered), e => Assert.Matches("^RPT-[A-Z]{3}-[0-9]{3}$", e));
    }

    /// <summary>BR-RPT-011 and ADR-013: the System Administrator is offered no report; an entity's person only the entity set, whatever an audience lists.</summary>
    [Fact]
    public void TheAdministratorIsOfferedNoReportAndAnEntityOnlyItsSet()
    {
        Assert.All(ReportCatalogue.Codes, code => Assert.False(ReportCatalogue.MayBeOfferedTo(code, "R01")));
        Assert.All(ReportCatalogue.Codes, code => Assert.True(ReportCatalogue.MayBeOfferedTo(code, "R02")));
        Assert.Equal(ReportCatalogue.EntityReports.Order(), ReportCatalogue.Codes.Where(c => ReportCatalogue.MayBeOfferedTo(c, "R08")).Order());
        Assert.Equal(ReportCatalogue.EntityReports.Order(), ReportCatalogue.Codes.Where(c => ReportCatalogue.MayRun(c, isExternal: true)).Order());
        Assert.All(ReportCatalogue.Codes, code => Assert.True(ReportCatalogue.MayRun(code, isExternal: false)));
        Assert.DoesNotContain(ReportCode.PortfolioSummary, ReportCatalogue.EntityReports);
        Assert.DoesNotContain(ReportCode.GovernanceChange, ReportCatalogue.EntityReports);
    }

    [Fact]
    public void OnlyTheOneProjectReportsRequireAProject() =>
        Assert.Equal([ReportCode.ProjectReport, ReportCode.ProgressHistory], ReportCatalogue.Codes.Where(ReportCatalogue.RequiresProject).Order());
}
