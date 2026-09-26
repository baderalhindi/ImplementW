using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// What one audience may see of one entity's classified fields (ADR-010). It is decided once per audience and entity,
/// with no input naming the projection, so a screen, a dashboard, a report, an export and an API projection built from
/// the same mask restrict the same fields the same way (CTL-19). A field it does not reveal is omitted from the
/// representation and listed in <c>maskedFields</c> (api-conventions R-20).
/// </summary>
public sealed class FieldMask(IReadOnlyDictionary<string, MaskingRule> restricted)
{
    public static FieldMask None { get; } = new(new Dictionary<string, MaskingRule>());

    /// <summary><see cref="MaskingRule.Reveal"/> for an unclassified field or a cleared audience; else the field's configured rule.</summary>
    public MaskingRule RuleFor(string fieldCode) =>
        restricted.TryGetValue(fieldCode, out MaskingRule rule) ? rule : MaskingRule.Reveal;

    public bool Reveals(string fieldCode) => !restricted.ContainsKey(fieldCode);

    /// <summary>The field codes this audience does not see, in ordinal order: the R-20 <c>maskedFields</c> list.</summary>
    public IReadOnlyList<string> MaskedFields { get; } = [.. restricted.Keys.Order(StringComparer.Ordinal)];
}
