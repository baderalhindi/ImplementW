using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A field the SCR-138 controlled explorer may expose (ADR-019); masking applies at execution (ADR-010).</summary>
public sealed record ReportFieldEntry(
    string SourceEntityCode, string FieldCode, BilingualLabel Label, bool IsFilterable, bool IsSortable, Guid? DataClassificationItemId);
