using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;

namespace PMPlatform.Tests.Unit.Application.Common.DataImport;

/// <summary>TASK-102: a workbook cell is held to its column's shape, and to the rules every text the platform receives obeys (TASK-077).</summary>
public sealed class ImportCellParserTests
{
    [Theory]
    [InlineData(ImportValueKind.Text, "  Riyadh ring road  ", "Riyadh ring road")]
    [InlineData(ImportValueKind.Code, "DEPT-01_A", "DEPT-01_A")]
    [InlineData(ImportValueKind.Email, "a.person@ahda.gov.sa", "a.person@ahda.gov.sa")]
    [InlineData(ImportValueKind.PhoneNumber, "+966501234567", "+966501234567")]
    public void TextKindsKeepTheTrimmedText(ImportValueKind kind, string cell, string expected)
    {
        Assert.Equal(expected, ImportCellParser.Parse(Column(kind), cell, out string? issue));
        Assert.Null(issue);
    }

    [Theory]
    [InlineData("2025-01-31", 2025, 1, 31)]
    [InlineData("45688", 2025, 1, 31)]
    [InlineData("61", 1900, 3, 1)]
    public void ADateIsIsoTextOrTheSerialADateCellHolds(string cell, int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), ImportCellParser.Parse(Column(ImportValueKind.Date), cell, out _));

    [Theory]
    [InlineData(ImportValueKind.Date, "31/01/2025")]
    [InlineData(ImportValueKind.Date, "45688.5")]
    [InlineData(ImportValueKind.Date, "60")]
    [InlineData(ImportValueKind.Code, "dept-01")]
    [InlineData(ImportValueKind.Email, "not an address")]
    [InlineData(ImportValueKind.PhoneNumber, "0501234567")]
    [InlineData(ImportValueKind.Number, "1,250")]
    [InlineData(ImportValueKind.WholeNumber, "2.5")]
    [InlineData(ImportValueKind.Boolean, "yes")]
    public void AValueOfTheWrongShapeIsMalformed(ImportValueKind kind, string cell)
    {
        Assert.Null(ImportCellParser.Parse(Column(kind), cell, out string? issue));
        Assert.Equal(ValidationCode.Malformed, issue);
    }

    [Fact]
    public void NumbersAreInvariantAndAWholeNumberMayCarryAZeroFraction()
    {
        Assert.Equal(1250000.75m, ImportCellParser.Parse(Column(ImportValueKind.Number), "1250000.75", out _));
        Assert.Equal(1.25e6m, ImportCellParser.Parse(Column(ImportValueKind.Number), "1.25E+6", out _));
        Assert.Equal(42, ImportCellParser.Parse(Column(ImportValueKind.WholeNumber), "42.0", out _));
        Assert.Equal(true, ImportCellParser.Parse(Column(ImportValueKind.Boolean), "true", out _));
        Assert.Equal(false, ImportCellParser.Parse(Column(ImportValueKind.Boolean), "FALSE", out _));
    }

    [Fact]
    public void AChoiceIsMatchedWithoutCaseAndReadAsTheListWritesIt()
    {
        ImportColumn status = Column(ImportValueKind.Choice) with { Choices = ["ACTIVE", "CLOSED"] };

        Assert.Equal("ACTIVE", ImportCellParser.Parse(status, "active", out _));
        Assert.Null(ImportCellParser.Parse(status, "OPEN", out string? issue));
        Assert.Equal(ValidationCode.EnumValue, issue);
    }

    [Fact]
    public void AnEmptyCellIsNullAndRequiredOnlyWhereTheColumnIs()
    {
        Assert.Null(ImportCellParser.Parse(Column(ImportValueKind.Text) with { Required = false }, "   ", out string? optional));
        Assert.Null(optional);
        Assert.Null(ImportCellParser.Parse(Column(ImportValueKind.Text), null, out string? required));
        Assert.Equal(ValidationCode.Required, required);
    }

    [Fact]
    public void TextIsHeldToItsLengthAndToTheCharactersEveryFieldRefuses()
    {
        Assert.Null(ImportCellParser.Parse(Column(ImportValueKind.Text) with { MaxLength = 5 }, "too long", out string? length));
        Assert.Equal(ValidationCode.MaxLength, length);
        Assert.Null(ImportCellParser.Parse(Column(ImportValueKind.Text), "bell\u0007", out string? control));
        Assert.Equal(ValidationCode.InvalidCharacter, control);
        Assert.Null(ImportCellParser.Parse(Column(ImportValueKind.Code), new string('A', 51), out string? code));
        Assert.Equal(ValidationCode.MaxLength, code);
    }

    private static ImportColumn Column(ImportValueKind kind) => new("column", kind, Required: true, "Column", "عمود");
}
