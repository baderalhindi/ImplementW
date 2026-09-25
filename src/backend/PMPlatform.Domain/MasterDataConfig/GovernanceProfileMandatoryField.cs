using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A project registration field that a governance profile makes mandatory (TASK-105). Delete policy: CASCADE.</summary>
public sealed class GovernanceProfileMandatoryField : AuditedEntity
{
    public Guid GovernanceProfileSettingId { get; set; }

    public required string FieldCode { get; set; }
}
