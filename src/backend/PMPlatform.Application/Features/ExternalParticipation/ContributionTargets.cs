namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>The registered typed adapters, by the source record type a schema names.</summary>
internal sealed class ContributionTargets(IEnumerable<IExternalContributionTarget> targets)
{
    private readonly Dictionary<string, IExternalContributionTarget> _byType = targets.ToDictionary(t => t.TargetType, StringComparer.Ordinal);

    /// <summary>The adapter of a schema's source; null for a reference-only schema.</summary>
    public IExternalContributionTarget? Of(ContributionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return schema.TargetType is not { } type ? null
            : _byType.TryGetValue(type, out IExternalContributionTarget? target) ? target
            : throw new InvalidOperationException($"Schema {schema.Code} names the source {type}, which has no adapter.");
    }
}
