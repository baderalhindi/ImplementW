using PMPlatform.Application.Features.Notifications;

namespace PMPlatform.Tests.Unit.Application.Notifications;

public sealed class TemplateTextTests
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal) { ["name"] = "ISS-1", ["deepLink"] = "/x" };

    [Fact]
    public void EveryPlaceholderIsReplacedByItsValueAsPlainText()
    {
        Dictionary<string, string> values = new(Values) { ["name"] = "{{deepLink}} <b>" };
        Assert.Equal("Concern {{deepLink}} <b> at /x.", TemplateText.Render("Concern {{name}} at {{deepLink}}.", values));
    }

    [Fact]
    public void APlaceholderWithoutAValueRendersNothing() =>
        Assert.Null(TemplateText.Render("Concern {{name}} {{severity}}", Values));

    [Theory]
    [InlineData("Plain text", true)]
    [InlineData("{{name}} and {{deepLink}}", true)]
    [InlineData("{{name", false)]
    [InlineData("name}}", false)]
    [InlineData("{{ name }}", false)]
    [InlineData("{{1name}}", false)]
    [InlineData("{{na-me}}", false)]
    public void OnlyNamePlaceholdersAreWellFormed(string text, bool wellFormed) =>
        Assert.Equal(wellFormed, TemplateText.IsWellFormed(text));

    [Fact]
    public void PlaceholdersAreListedOnceInOrder() =>
        Assert.Equal(["b", "a"], TemplateText.Placeholders("{{b}} {{a}} {{b}}"));
}
