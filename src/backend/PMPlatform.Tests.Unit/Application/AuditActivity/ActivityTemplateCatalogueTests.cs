using System.Reflection;
using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Tests.Unit.Application.AuditActivity;

/// <summary>TASK-073: every audit event type a producer declares is decided — Activity, or deliberately not — and each line reads in both languages.</summary>
public sealed class ActivityTemplateCatalogueTests
{
    /// <summary>The <c>public const string</c> values of every <c>*AuditEvents</c> class in Application: the event types producers emit.</summary>
    private static readonly IReadOnlyList<string> DeclaredEventTypes = [.. typeof(AuditTrail).Assembly.GetTypes()
        .Where(t => t.Name.EndsWith("AuditEvents", StringComparison.Ordinal))
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Distinct(StringComparer.Ordinal)];

    [Fact]
    public void EveryDeclaredEventTypeIsTemplatedOrExcluded()
    {
        List<string> undecided = [.. DeclaredEventTypes.Where(type =>
            !ActivityTemplateCatalogue.Templates.ContainsKey(type)
            && !ActivityTemplateCatalogue.ExcludedEventTypes.Contains(type)
            && !ActivityTemplateCatalogue.ExcludedModules.Contains(type[..type.IndexOf('.', StringComparison.Ordinal)]))];

        Assert.True(DeclaredEventTypes.Count > 250, $"Only {DeclaredEventTypes.Count} event types were found.");
        Assert.Empty(undecided);
    }

    /// <summary>A template or an exclusion that names no declared event type is a typo, or outlived its event.</summary>
    [Fact]
    public void EveryTemplateAndExclusionNamesADeclaredEventType()
    {
        Assert.Empty(ActivityTemplateCatalogue.Templates.Keys.Except(DeclaredEventTypes, StringComparer.Ordinal));
        Assert.Empty(ActivityTemplateCatalogue.ExcludedEventTypes.Except(DeclaredEventTypes, StringComparer.Ordinal));
        Assert.Empty(ActivityTemplateCatalogue.Templates.Keys.Intersect(ActivityTemplateCatalogue.ExcludedEventTypes, StringComparer.Ordinal));
        Assert.DoesNotContain(ActivityTemplateCatalogue.Templates.Keys, type => ActivityTemplateCatalogue.ExcludedModules.Contains(type[..type.IndexOf('.', StringComparison.Ordinal)]));
    }

    /// <summary>ADR-012 / FG-06 §12.1: Arabic and English, within the column, never a value or placeholder.</summary>
    [Fact]
    public void EveryLineReadsInBothLanguagesWithinItsColumn() =>
        Assert.All(ActivityTemplateCatalogue.Templates, pair =>
        {
            Assert.InRange(pair.Value.SummaryEn.Length, 1, 500);
            Assert.InRange(pair.Value.SummaryAr.Length, 1, 500);
            Assert.Contains(pair.Value.SummaryAr, c => c is >= '؀' and <= 'ۿ');
            Assert.DoesNotContain(pair.Value.SummaryEn, c => c is >= '؀' and <= 'ۿ');
            Assert.DoesNotContain('{', pair.Value.SummaryEn + pair.Value.SummaryAr);
        });

    [Fact]
    public void EveryActivityKindFitsItsColumn() =>
        Assert.All(ActivityTemplateCatalogue.Templates.Keys, type => Assert.Matches("^[A-Z][A-Z0-9_]{0,49}$", ActivityProjection.KindOf(type)));
}
