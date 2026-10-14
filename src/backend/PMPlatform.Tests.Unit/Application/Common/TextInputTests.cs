using PMPlatform.Application.Common.Validation;

namespace PMPlatform.Tests.Unit.Application.Common;

/// <summary>TASK-077: the text rules every field obeys — what they refuse, and the text people actually write, which they accept.</summary>
public sealed class TextInputTests
{
    /// <summary>Text is stored as entered (ADR-012): markup, quotes, SQL, Arabic, bidirectional marks and emoji are all just text.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("Contractor mobilisation delay")]
    [InlineData("تأخر تعبئة المقاول")]
    [InlineData("Line one\nLine two\r\n\tindented")]
    [InlineData("<script>alert(1)</script><img src=x onerror=alert(1)>")]
    [InlineData("Robert'); DROP TABLE risk.risk;--")]
    [InlineData("‏‎⁨isolated⁩")]
    [InlineData("site 🚧 closed")]
    public void TextPeopleWriteIsAccepted(string value) => Assert.Null(TextInput.Violation(value));

    [Theory]
    [InlineData("nul \u0000 here")]
    [InlineData("bell \u0007")]
    [InlineData("escape \u001B[31m")]
    [InlineData("delete \u007F")]
    [InlineData("next line \u0085")]
    [InlineData("c1 \u009F")]
    public void ControlCharactersOtherThanTabAndLineBreaksAreRefused(string value) =>
        Assert.Equal(ValidationCode.InvalidCharacter, TextInput.Violation(value));

    /// <summary>Built here, not as theory data: xUnit serializes theory data and turns a lone surrogate into U+FFFD on the way.</summary>
    [Fact]
    public void ASurrogateWithoutItsPairIsRefused()
    {
        string[] values = ["lone high " + '\uD800', '\uDC00' + " lone low", "reversed " + '\uDC00' + '\uD800', "cut " + "\uD83D\uDEA7"[..1]];
        Assert.All(values, value => Assert.Equal(ValidationCode.InvalidCharacter, TextInput.Violation(value)));
    }

    [Fact]
    public void TextUpToTheCeilingIsAcceptedAndBeyondItIsTooLong()
    {
        Assert.Null(TextInput.Violation(new string('a', TextInput.MaxLength)));
        Assert.Equal(ValidationCode.MaxLength, TextInput.Violation(new string('a', TextInput.MaxLength + 1)));
    }
}
