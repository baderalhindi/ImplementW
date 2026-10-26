using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.DocumentManagement.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>The audit events of WF-12 (TASK-033 <c>IAuditTrail</c>). Titles and descriptions are free text and are not copied.</summary>
internal static class DocumentAudit
{
    public const string Module = "DocumentManagement";

    public static AuditEntry Uploaded(Guid actorId, Document document, DocumentAnchors anchors, DocumentVersion version) =>
        DocumentEntry(AuditEventClass.DataChange, DocumentAuditEvents.DocumentUploaded, actorId, AuditActorType.User, document, anchors,
        [
            AuditAttribute.Of(DocumentAuditAttributes.DocumentTypeItemId, document.DocumentTypeItemId),
            AuditAttribute.Of(DocumentAuditAttributes.DataClassificationItemId, document.DataClassificationItemId),
            .. VersionAttributes(version),
        ]);

    public static AuditEntry VersionAdded(Guid actorId, Document document, DocumentAnchors anchors, DocumentVersion version) =>
        DocumentEntry(AuditEventClass.DataChange, DocumentAuditEvents.VersionAdded, actorId, AuditActorType.User, document, anchors, VersionAttributes(version));

    public static AuditEntry MetadataChanged(Guid actorId, Document document, DocumentAnchors anchors, IEnumerable<AuditAttribute> changes) =>
        DocumentEntry(AuditEventClass.DataChange, DocumentAuditEvents.MetadataChanged, actorId, AuditActorType.User, document, anchors, changes);

    public static AuditEntry Archived(Guid actorId, Document document, DocumentAnchors anchors) =>
        DocumentEntry(AuditEventClass.LifecycleTransition, DocumentAuditEvents.DocumentArchived, actorId, AuditActorType.User, document, anchors,
            [AuditAttribute.Change(DocumentAuditAttributes.Status, DocumentStatus.Active, DocumentStatus.Archived)!]);

    public static AuditEntry ScanCompleted(Document document, DocumentAnchors anchors, DocumentVersion version) =>
        DocumentEntry(AuditEventClass.LifecycleTransition, DocumentAuditEvents.ScanCompleted, DocumentServicePrincipal.Id, AuditActorType.Service, document, anchors,
        [
            AuditAttribute.Of(DocumentAuditAttributes.DocumentVersionId, version.Id),
            AuditAttribute.Of(DocumentAuditAttributes.VersionNo, version.VersionNo),
            AuditAttribute.Change(DocumentAuditAttributes.ScanState, ScanState.ScanPending, version.ScanState)!,
            AuditAttribute.Of(DocumentAuditAttributes.ScanReference, version.ScanReference),
        ]);

    public static AuditEntry ScanRequeued(Guid actorId, Document document, DocumentAnchors anchors, DocumentVersion version) =>
        DocumentEntry(AuditEventClass.LifecycleTransition, DocumentAuditEvents.ScanRequeued, actorId, AuditActorType.User, document, anchors,
        [
            AuditAttribute.Of(DocumentAuditAttributes.DocumentVersionId, version.Id),
            AuditAttribute.Change(DocumentAuditAttributes.ScanState, ScanState.ScanFailed, ScanState.ScanPending)!,
        ]);

    /// <summary>Someone asked for malware's bytes; it was withheld (CTL-20, CTL-25).</summary>
    public static AuditEntry QuarantinedContentWithheld(Guid actorId, Document document, DocumentAnchors anchors, DocumentVersion version) =>
        new(AuditEventClass.AuthorizationDenial, DocumentAuditEvents.QuarantinedContentWithheld, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(DocumentVersion), version.Id),
            ScopeProjectId = anchors.ProjectId,
            ScopeExternalEntityId = anchors.ExternalEntityId,
            DataClassificationItemId = document.DataClassificationItemId,
            Attributes =
            [
                AuditAttribute.Of(DocumentAuditAttributes.DocumentId, document.Id),
                AuditAttribute.Of(DocumentAuditAttributes.VersionNo, version.VersionNo),
                AuditAttribute.Of(DocumentAuditAttributes.ScanState, version.ScanState),
            ],
        };

    public static AuditEntry Link(string eventType, Guid actorId, Document document, DocumentAnchors anchors, BusinessLink link) =>
        new(AuditEventClass.DataChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(BusinessLink), link.Id),
            ScopeProjectId = anchors.ProjectId,
            ScopeExternalEntityId = anchors.ExternalEntityId,
            DataClassificationItemId = document.DataClassificationItemId,
            Attributes = LinkAttributes(document, link),
        };

    public static AuditEntry Evidence(string eventType, Guid actorId, Document document, DocumentAnchors anchors, BusinessLink link, EvidenceReference evidence) =>
        new(AuditEventClass.DataChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, nameof(EvidenceReference), evidence.Id),
            ScopeProjectId = anchors.ProjectId,
            ScopeExternalEntityId = anchors.ExternalEntityId,
            DataClassificationItemId = document.DataClassificationItemId,
            Attributes =
            [
                .. LinkAttributes(document, link),
                AuditAttribute.Of(DocumentAuditAttributes.DocumentVersionId, evidence.DocumentVersionId),
                AuditAttribute.Of(DocumentAuditAttributes.EvidenceTypeItemId, evidence.EvidenceTypeItemId),
                AuditAttribute.Of(DocumentAuditAttributes.Status, evidence.Status),
            ],
        };

    private static AuditEntry DocumentEntry(
        AuditEventClass eventClass, string eventType, Guid actorId, AuditActorType actorType, Document document, DocumentAnchors anchors, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = new AuditSubject(Module, nameof(Document), document.Id),
            ScopeProjectId = anchors.ProjectId,
            ScopeExternalEntityId = anchors.ExternalEntityId,
            DataClassificationItemId = document.DataClassificationItemId,
            Attributes = [.. attributes],
        };

    private static IEnumerable<AuditAttribute> VersionAttributes(DocumentVersion version) =>
    [
        AuditAttribute.Of(DocumentAuditAttributes.DocumentVersionId, version.Id),
        AuditAttribute.Of(DocumentAuditAttributes.VersionNo, version.VersionNo),
        AuditAttribute.Of(DocumentAuditAttributes.FileName, version.FileName),
        AuditAttribute.Of(DocumentAuditAttributes.ContentType, version.ContentType),
        AuditAttribute.Of(DocumentAuditAttributes.SizeBytes, version.SizeBytes),
        AuditAttribute.Of(DocumentAuditAttributes.ChecksumSha256, version.ChecksumSha256),
    ];

    private static AuditAttribute[] LinkAttributes(Document document, BusinessLink link) =>
    [
        AuditAttribute.Of(DocumentAuditAttributes.DocumentId, document.Id),
        AuditAttribute.Of(DocumentAuditAttributes.BusinessLinkId, link.Id),
        AuditAttribute.Of(DocumentAuditAttributes.LinkRole, link.LinkRole),
        AuditAttribute.Of(DocumentAuditAttributes.TargetModule, link.TargetModule),
        AuditAttribute.Of(DocumentAuditAttributes.TargetType, link.TargetType),
        AuditAttribute.Of(DocumentAuditAttributes.TargetId, link.TargetId),
    ];
}
