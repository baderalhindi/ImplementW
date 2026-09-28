using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// The editable representation of an item. While DRAFT its author may change all of it; once PUBLISHED a label,
/// description or order correction is an audited edit, and the parent is fixed (ERD §5.3).
/// </summary>
public sealed record MasterDataItemChanges(BilingualLabel Label, BilingualLabel? Description, Guid? ParentItemId, int SortOrder);
