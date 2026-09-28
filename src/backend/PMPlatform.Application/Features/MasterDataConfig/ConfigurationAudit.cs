using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>The CONFIGURATION_CHANGE audit event every FG-04 change is saved with (TASK-033).</summary>
internal static class ConfigurationAudit
{
    public const string Module = "MasterDataConfig";

    public static AuditEntry Entry(string eventType, Guid actorId, string subjectType, Guid subjectId, IEnumerable<AuditAttribute?> attributes) =>
        new(AuditEventClass.ConfigurationChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, subjectType, subjectId),
            Attributes = [.. attributes.OfType<AuditAttribute>()],
        };
}
