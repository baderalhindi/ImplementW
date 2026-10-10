using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Reports.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// The audit events of FG-02 (FG-02 §24; TASK-033 <c>IAuditTrail</c>): configuration, exports, downloads and their refusals, saved views, and
/// sensitive runs. An event carries codes, counts, ids and the output's hash — never a report's values or a label.
/// </summary>
internal static class ReportAudit
{
    public const string SubjectModule = "Reports";
    public const string DefinitionType = "ReportDefinition";
    public const string JobType = "ReportJob";
    public const string SavedViewType = "SavedView";

    public static AuditEntry SensitiveReportExecuted(Guid actorId, ReportCode? code, Guid? definitionId, int rowCount, int projectCount) =>
        new(AuditEventClass.DataChange, ReportAuditEvents.SensitiveReportExecuted, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = definitionId is { } id ? new AuditSubject(SubjectModule, DefinitionType, id) : null,
            Attributes =
            [
                AuditAttribute.Of(ReportAuditAttributes.Code, code),
                AuditAttribute.Of(ReportAuditAttributes.RowCount, rowCount),
                AuditAttribute.Of(ReportAuditAttributes.ProjectCount, projectCount),
            ],
        };

    public static AuditEntry ExportRequested(ReportJob job, ReportCode? code, int columnCount) =>
        Job(ReportAuditEvents.ExportRequested, AuditEventClass.DataChange, AuditOutcome.Success, job.RequestedByUserId, AuditActorType.User, job,
        [
            AuditAttribute.Of(ReportAuditAttributes.Code, code),
            AuditAttribute.Of(ReportAuditAttributes.ExportFormat, job.ExportFormat),
            AuditAttribute.Of(ReportAuditAttributes.ReportLanguage, job.ReportLanguage),
            AuditAttribute.Of(ReportAuditAttributes.ColumnCount, columnCount),
        ]);

    public static AuditEntry ExportCompleted(ReportJob job, GeneratedOutput output, int projectCount, int neutralizedCells) =>
        Job(ReportAuditEvents.ExportCompleted, AuditEventClass.LifecycleTransition, AuditOutcome.Success, ReportServicePrincipal.Id, AuditActorType.Service, job,
        [
            AuditAttribute.Change(ReportAuditAttributes.Status, ReportJobStatus.Running, job.Status)!,
            AuditAttribute.Of(ReportAuditAttributes.RowCount, output.RowCount),
            AuditAttribute.Of(ReportAuditAttributes.ProjectCount, projectCount),
            AuditAttribute.Of(ReportAuditAttributes.Sensitivity, output.Sensitivity),
            AuditAttribute.Of(ReportAuditAttributes.SizeBytes, output.SizeBytes),
            AuditAttribute.Of(ReportAuditAttributes.ChecksumSha256, output.ChecksumSha256),
            AuditAttribute.Of(ReportAuditAttributes.NeutralizedCellCount, neutralizedCells),
        ]);

    public static AuditEntry ExportFailed(ReportJob job, ReportJobStatus from) =>
        Job(ReportAuditEvents.ExportFailed, AuditEventClass.LifecycleTransition, AuditOutcome.Failed, ReportServicePrincipal.Id, AuditActorType.Service, job,
        [
            AuditAttribute.Change(ReportAuditAttributes.Status, from, job.Status)!,
            AuditAttribute.Of(ReportAuditAttributes.FailureCode, job.FailureCode),
        ]);

    public static AuditEntry ExportCancelled(Guid actorId, ReportJob job, ReportJobStatus from) =>
        Job(ReportAuditEvents.ExportCancelled, AuditEventClass.LifecycleTransition, AuditOutcome.Success, actorId, AuditActorType.User, job,
            [AuditAttribute.Change(ReportAuditAttributes.Status, from, job.Status)!]);

    public static AuditEntry OutputExpired(ReportJob job) =>
        Job(ReportAuditEvents.OutputExpired, AuditEventClass.LifecycleTransition, AuditOutcome.Success, ReportServicePrincipal.Id, AuditActorType.Service, job,
            [AuditAttribute.Change(ReportAuditAttributes.Status, ReportJobStatus.Completed, job.Status)!]);

    public static AuditEntry OutputDownloaded(Guid actorId, ReportJob job, GeneratedOutput output) =>
        Job(ReportAuditEvents.OutputDownloaded, AuditEventClass.DataChange, AuditOutcome.Success, actorId, AuditActorType.User, job,
        [
            AuditAttribute.Of(ReportAuditAttributes.Sensitivity, output.Sensitivity),
            AuditAttribute.Of(ReportAuditAttributes.ChecksumSha256, output.ChecksumSha256),
        ]);

    /// <summary>RPT-EVT-013: a security event — the requester may no longer see what the output holds.</summary>
    public static AuditEntry OutputAccessDenied(Guid actorId, ReportJob job) =>
        Job(ReportAuditEvents.OutputAccessDenied, AuditEventClass.AuthorizationDenial, AuditOutcome.Denied, actorId, AuditActorType.User, job,
            [AuditAttribute.Of(ReportAuditAttributes.ReasonCode, Contracts.ReportErrorCodes.DownloadAccessDenied)]);

    public static AuditEntry SavedViewCreated(Guid actorId, SavedView view) => View(ReportAuditEvents.SavedViewCreated, actorId, view);

    public static AuditEntry SavedViewChanged(Guid actorId, SavedView view) => View(ReportAuditEvents.SavedViewChanged, actorId, view);

    public static AuditEntry DefinitionCreated(Guid actorId, ReportDefinition definition, int columnCount) =>
        Definition(AuditEventClass.ConfigurationChange, ReportAuditEvents.DefinitionCreated, actorId, definition,
            [AuditAttribute.Of(ReportAuditAttributes.ColumnCount, columnCount)]);

    public static AuditEntry DefinitionChanged(Guid actorId, ReportDefinition definition, int columnCount, int parameterCount, IReadOnlyList<string> audienceRoleCodes) =>
        Definition(AuditEventClass.ConfigurationChange, ReportAuditEvents.DefinitionChanged, actorId, definition,
        [
            AuditAttribute.Of(ReportAuditAttributes.ColumnCount, columnCount),
            AuditAttribute.Of(ReportAuditAttributes.ParameterCount, parameterCount),
            AuditAttribute.Of(ReportAuditAttributes.AudienceRoleCodes, string.Join(',', audienceRoleCodes.Order(StringComparer.Ordinal))),
        ]);

    public static AuditEntry DefinitionValidated(Guid actorId, ReportDefinition definition) =>
        Transition(ReportAuditEvents.DefinitionValidated, actorId, definition, GovernedLifecycleState.Draft, []);

    public static AuditEntry DefinitionPublished(Guid actorId, ReportDefinition definition, Guid? supersededId) =>
        Transition(ReportAuditEvents.DefinitionPublished, actorId, definition, GovernedLifecycleState.Validated,
            [AuditAttribute.Of(ReportAuditAttributes.SupersededDefinitionId, supersededId)]);

    public static AuditEntry DefinitionRetired(Guid actorId, ReportDefinition definition, GovernedLifecycleState from, Guid? supersededById) =>
        Transition(ReportAuditEvents.DefinitionRetired, actorId, definition, from, [AuditAttribute.Of(ReportAuditAttributes.SupersededByDefinitionId, supersededById)]);

    private static AuditEntry Job(
        string eventType, AuditEventClass eventClass, AuditOutcome outcome, Guid actorId, AuditActorType actorType, ReportJob job, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, outcome)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = new AuditSubject(SubjectModule, JobType, job.Id),
            Attributes = [AuditAttribute.Of(ReportAuditAttributes.Kind, job.Kind), .. attributes],
        };

    private static AuditEntry View(string eventType, Guid actorId, SavedView view) =>
        new(AuditEventClass.DataChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(SubjectModule, SavedViewType, view.Id),
            Attributes = [AuditAttribute.Of(ReportAuditAttributes.ViewType, view.ViewType)],
        };

    private static AuditEntry Transition(string eventType, Guid actorId, ReportDefinition definition, GovernedLifecycleState from, IEnumerable<AuditAttribute> attributes) =>
        Definition(AuditEventClass.LifecycleTransition, eventType, actorId, definition,
            [AuditAttribute.Change(ReportAuditAttributes.LifecycleState, from, definition.LifecycleState)!, .. attributes]);

    private static AuditEntry Definition(AuditEventClass eventClass, string eventType, Guid actorId, ReportDefinition definition, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(SubjectModule, DefinitionType, definition.Id),
            Attributes =
            [
                AuditAttribute.Of(ReportAuditAttributes.Code, definition.Code),
                AuditAttribute.Of(ReportAuditAttributes.VersionNo, definition.VersionNo),
                .. attributes,
            ],
        };
}
