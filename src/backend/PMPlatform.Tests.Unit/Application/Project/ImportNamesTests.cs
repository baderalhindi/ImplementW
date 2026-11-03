using PMPlatform.Application.Features.Project.Import;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>How a workbook's names are compared with the platform's (R3-4): spelling variants a person does not see as different are one name.</summary>
public sealed class ImportNamesTests
{
    [Theory]
    [InlineData("مبسّط", "مبسط")]
    [InlineData("الأحساء", "الاحساء")]
    [InlineData("  إدارة   المشاريع ", "ادارة المشاريع")]
    [InlineData("مستشفى", "مستشفي")]
    [InlineData("Full", "full")]
    [InlineData("الـقرى", "القرى")]
    public void VariantsAreOneName(string written, string platform) => Assert.Equal(ImportNames.Key(platform), ImportNames.Key(written));

    [Theory]
    [InlineData("PF-02 — وجهة ثقافية", "PF-02", "وجهة ثقافية")]
    [InlineData("SI-04 - تطوير التجارب", "SI-04", "تطوير التجارب")]
    [InlineData("الصحة", null, "الصحة")]
    public void ACodedNameSplitsIntoCodeAndLabel(string text, string? code, string label) => Assert.Equal((code, label), ImportNames.Split(text));
}
