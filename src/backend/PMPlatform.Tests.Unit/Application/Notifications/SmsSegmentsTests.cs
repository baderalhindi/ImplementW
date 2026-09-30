using PMPlatform.Application.Features.Notifications;

namespace PMPlatform.Tests.Unit.Application.Notifications;

/// <summary>ADR-004's Arabic segment budget rests on this count: GSM-7 160/153 septets, UCS-2 70/67 characters.</summary>
public sealed class SmsSegmentsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(160, 1)]
    [InlineData(161, 2)]
    [InlineData(306, 2)]
    [InlineData(307, 3)]
    public void GsmTextIs160SeptetsInOneSegmentAnd153WhenConcatenated(int length, int segments) =>
        Assert.Equal(segments, SmsSegments.Count(new string('a', length)));

    [Theory]
    [InlineData(70, 1)]
    [InlineData(71, 2)]
    [InlineData(134, 2)]
    [InlineData(135, 3)]
    [InlineData(201, 3)]
    [InlineData(202, 4)]
    public void ArabicTextIsUcs2At70CharactersInOneSegmentAnd67WhenConcatenated(int length, int segments) =>
        Assert.Equal(segments, SmsSegments.Count(new string('ت', length)));

    [Fact]
    public void OneArabicLetterPutsTheWholeTextInUcs2() =>
        Assert.Equal(2, SmsSegments.Count(new string('a', 70) + "ت"));

    [Fact]
    public void AnExtensionCharacterCostsTwoSeptets()
    {
        Assert.Equal(1, SmsSegments.Count(new string('a', 158) + "{"));
        Assert.Equal(2, SmsSegments.Count(new string('a', 159) + "{"));
    }
}
