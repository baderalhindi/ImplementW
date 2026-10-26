using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>A bilingual label as a request carries it (R-17), validated before it becomes a <see cref="BilingualLabel"/>.</summary>
public sealed record BilingualLabelRequest(string? Ar, string? En)
{
    internal BilingualLabel ToLabel() => new(Ar!, En!);
}
