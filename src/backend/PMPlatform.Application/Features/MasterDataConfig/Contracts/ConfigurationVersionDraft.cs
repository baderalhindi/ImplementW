using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// A new DRAFT version of a family. With <see cref="BasedOnVersionId"/> it starts as a copy of that version's content,
/// which must be of the same family; the source is read, never changed.
/// </summary>
public sealed record ConfigurationVersionDraft(Guid FamilyId, Guid? BasedOnVersionId, NarrativeText? ChangeSummary);
