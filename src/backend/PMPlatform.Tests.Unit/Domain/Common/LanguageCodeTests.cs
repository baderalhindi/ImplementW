using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Domain.Common;

public sealed class LanguageCodeTests
{
    [Theory]
    [InlineData(Language.Ar, "ar")]
    [InlineData(Language.En, "en")]
    public void RoundTripsTheTwoPlatformLanguages(Language language, string code)
    {
        Assert.Equal(code, LanguageCode.Of(language));
        Assert.Equal(language, LanguageCode.Parse(code));
    }

    [Fact]
    public void RejectsAnyOtherCode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LanguageCode.Parse("fr"));
    }

    [Fact]
    public void NarrativeTextKeepsItsEntryLanguage()
    {
        var narrative = new NarrativeText("تقرير التقدم", Language.Ar);
        Assert.Equal(Language.Ar, narrative.Language);
        Assert.Throws<ArgumentException>(() => new NarrativeText(" ", Language.En));
    }
}
