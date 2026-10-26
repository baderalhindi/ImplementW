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

    private static readonly string[] InternalBusinessRoles = ["R02", "R03", "R04", "R05", "R06", "R07"];

    /// <summary>
    /// BR-RPT-011 and ADR-019: the System Administrator is offered no report, and neither is the External Entity User; an external entity's
    /// person runs none, whatever an audience lists, while an internal caller may run any report it is in the audience of.
    /// </summary>
    [Fact]
    public void TheAdministratorAndEntityUsersAreOfferedNoReport()
    {
        Assert.False(ReportCatalogue.MayBeOfferedTo("R01"));
        Assert.False(ReportCatalogue.MayBeOfferedTo("R08"));
        Assert.All(InternalBusinessRoles, role => Assert.True(ReportCatalogue.MayBeOfferedTo(role)));
        Assert.False(ReportCatalogue.MayRun(isExternal: true));
        Assert.True(ReportCatalogue.MayRun(isExternal: false));
    }

    [Fact]
    public void OnlyTheOneProjectReportsRequireAProject() =>
        Assert.Equal([ReportCode.ProjectReport, ReportCode.ProgressHistory], ReportCatalogue.Codes.Where(ReportCatalogue.RequiresProject).Order());
}
