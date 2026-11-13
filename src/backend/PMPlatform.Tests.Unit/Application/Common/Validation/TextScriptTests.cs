using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;

namespace PMPlatform.Tests.Unit.Application.Common.Validation;

/// <summary>The 13 Nov 2026 review: a bilingual name's Arabic is written in Arabic and its English in English.</summary>
public sealed class TextScriptTests
{
    [Theory]
    [InlineData("مجتمع الواحة")]
    [InlineData("خادم البريد Exchange")]
    [InlineData("المرحلة 2")]
    public void ArabicTextHasAnArabicLetter(string text) => Assert.True(TextShape.IsInScript(text, TextScript.Arabic));

    [Theory]
    [InlineData("Oasis community")]
    [InlineData("PF-01")]
    [InlineData("2026")]
    [InlineData("١٢٣")]
    public void TextWithNoArabicLetterIsNotArabic(string text) => Assert.False(TextShape.IsInScript(text, TextScript.Arabic));

    [Theory]
    [InlineData("Oasis community")]
    [InlineData("Phase 2 (pilot)")]
    public void EnglishTextHasAnEnglishLetter(string text) => Assert.True(TextShape.IsInScript(text, TextScript.English));

    [Theory]
    [InlineData("مجتمع الواحة")]
    [InlineData("Exchange خادم")]
    [InlineData("2026")]
    public void EnglishTextHasNoArabicLetterAndSomeEnglishOne(string text) => Assert.False(TextShape.IsInScript(text, TextScript.English));

    [Theory]
    [InlineData(TextScript.Arabic, "Oasis community", ValidationCode.NotArabic)]
    [InlineData(TextScript.English, "مجتمع الواحة", ValidationCode.NotEnglish)]
    public void AnImportedNameInTheOtherLanguageIsRefused(TextScript script, string cell, string code)
    {
        ImportColumn column = new("label", ImportValueKind.Text, Required: true, "label", "label") { Script = script };

        Assert.Null(ImportCellParser.Parse(column, cell, out string? issue));
        Assert.Equal(code, issue);
    }

    [Fact]
    public void AnImportedNameInItsLanguageIsKept()
    {
        ImportColumn column = new("label_ar", ImportValueKind.Text, Required: true, "label", "label") { Script = TextScript.Arabic };

        Assert.Equal("مجتمع الواحة", ImportCellParser.Parse(column, "مجتمع الواحة", out string? issue));
        Assert.Null(issue);
    }
}
