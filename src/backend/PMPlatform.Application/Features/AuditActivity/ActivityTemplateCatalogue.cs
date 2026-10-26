namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>The bilingual line an audit event of one type is shown as on a project's Activity timeline (FG-06 §7.3, §12.1).</summary>
public sealed record ActivityTemplate(string SummaryEn, string SummaryAr);

/// <summary>
/// Which audit events become business Activity, and how each reads (FG-06 §7.1; BR-AUD-005, BR-AUD-029). Only the event types
/// listed here are projected: a template carries no value, name or note from the event, so an Activity line can reveal what
/// happened to a project's record but never a sensitive value, a restricted title or internal commentary (§7.4). Event types are
/// written as the language-neutral codes producers emit (EV-1), not as references to the producers' contracts: AuditActivity is a
/// foundation module and holds no type of another (ADR-003 §4.5). Every event type a producer declares is either templated here,
/// in <see cref="ExcludedEventTypes"/>, or of a module in <see cref="ExcludedModules"/> (<c>ActivityTemplateCatalogueTests</c>).
/// </summary>
public static class ActivityTemplateCatalogue
{
    /// <summary>
    /// Modules whose events are administration, security, configuration, integration operations or read-side records, never project
    /// activity. IntegrationMonitoring's are technical operations, kept apart from business state (TASK-075, BR-INT-001).
    /// </summary>
    public static IReadOnlySet<string> ExcludedModules { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "AuditActivity", "Dashboards", "IdentityAccess", "IntegrationMonitoring", "MasterDataConfig", "Notifications", "Reports",
    };

    /// <summary>
    /// Event types of projected modules that are not activity: refusals (AUTHORIZATION_DENIAL), ignored or failed outcomes,
    /// recomputed indicators, scanner and source-application mechanics, and approval delegation (PERMISSION_CHANGE).
    /// </summary>
    public static IReadOnlySet<string> ExcludedEventTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Project.ApprovalOutcomeIgnored", "Project.LifecycleGateRefused",
        "Progress.ProjectHealthRecomputed", "Progress.ReviewRefused",
        "Schedule.ApprovalOutcomeIgnored", "Schedule.ScheduleHealthRecomputed",
        "ProjectTask.ActivityProgressRecomputed",
        "Milestone.ApprovalOutcomeIgnored",
        "Risk.AuthorityRefused",
        "ManagementConcern.AuthorityRefused", "ManagementConcern.OutcomeIgnored",
        "ChangeRequest.AuthorityRefused", "ChangeRequest.OutcomeIgnored",
        "Suspension.AuthorityRefused", "Suspension.OutcomeIgnored",
        "Closure.AuthorityRefused", "Closure.OutcomeIgnored",
        "FinancialKpi.ApprovalOutcomeIgnored", "FinancialKpi.ReviewRefused",
        "ExternalParticipation.AuthorityRefused", "ExternalParticipation.SourceApplicationConflict",
        "ExternalParticipation.SourceApplicationFailed", "ExternalParticipation.SourceApplicationRevalidated",
        "DocumentManagement.ScanCompleted", "DocumentManagement.ScanRequeued", "DocumentManagement.QuarantinedContentWithheld",
        "Approval.OutcomeDelivered", "Approval.DecisionRefused",
        "Approval.DelegationCreated", "Approval.DelegationRevoked", "Approval.DelegationExpired",
    };

    public static IReadOnlyDictionary<string, ActivityTemplate> Templates { get; } = new Dictionary<string, ActivityTemplate>(StringComparer.Ordinal)
    {
        // WF-01 Project registration and lifecycle
        ["Project.ProjectCreated"] = new("Project draft created", "تم إنشاء مسودة المشروع"),
        ["Project.ProjectChanged"] = new("Project details updated", "تم تحديث بيانات المشروع"),
        ["Project.ProjectDeleted"] = new("Project draft deleted", "تم حذف مسودة المشروع"),
        ["Project.ProjectSubmitted"] = new("Project submitted for registration", "تم تقديم المشروع للتسجيل"),
        ["Project.SubmissionWithdrawn"] = new("Project submission withdrawn", "تم سحب تقديم المشروع"),
        ["Project.ReviewStarted"] = new("Registration review started", "بدأت مراجعة تسجيل المشروع"),
        ["Project.RegistrationReturned"] = new("Registration returned for changes", "تمت إعادة تسجيل المشروع لإجراء تعديلات"),
        ["Project.RegistrationApproved"] = new("Registration approved", "تمت الموافقة على تسجيل المشروع"),
        ["Project.ProjectActivated"] = new("Project activated", "تم تفعيل المشروع"),
        ["Project.ProjectTakenIn"] = new("Project taken in from the legacy system", "تم إدخال المشروع من النظام السابق"),
        ["Project.ProjectSuspended"] = new("Project suspended", "تم تعليق المشروع"),
        ["Project.ProjectResumed"] = new("Project resumed", "تم استئناف المشروع"),
        ["Project.ProjectCompleted"] = new("Project completed", "تم إكمال المشروع"),
        ["Project.ProjectClosed"] = new("Project closed", "تم إغلاق المشروع"),

        // WF-02 Progress update
        ["Progress.ReportingCycleCreated"] = new("Reporting period created", "تم إنشاء فترة التقرير"),
        ["Progress.ProgressUpdateStarted"] = new("Progress update started", "بدأ تحديث التقدم"),
        ["Progress.ProgressUpdateChanged"] = new("Progress update edited", "تم تعديل تحديث التقدم"),
        ["Progress.OpeningPositionRecorded"] = new("Opening progress position recorded", "تم تسجيل موقف التقدم الافتتاحي"),
        ["Progress.ProgressSubmitted"] = new("Progress update submitted for review", "تم تقديم تحديث التقدم للمراجعة"),
        ["Progress.ReviewStarted"] = new("Progress review started", "بدأت مراجعة تحديث التقدم"),
        ["Progress.ProgressReturned"] = new("Progress update returned", "تمت إعادة تحديث التقدم"),
        ["Progress.ProgressPublished"] = new("Progress update published", "تم نشر تحديث التقدم"),

        // WF-03 Schedule and baseline
        ["Schedule.ScheduleInitialized"] = new("Schedule created", "تم إنشاء الجدول الزمني"),
        ["Schedule.ActivityCreated"] = new("Schedule activity added", "تمت إضافة نشاط إلى الجدول الزمني"),
        ["Schedule.ActivityChanged"] = new("Schedule activity updated", "تم تحديث نشاط في الجدول الزمني"),
        ["Schedule.ActivityCancelled"] = new("Schedule activity cancelled", "تم إلغاء نشاط في الجدول الزمني"),
        ["Schedule.ActivityReforecast"] = new("Activity forecast updated", "تم تحديث توقعات النشاط"),
        ["Schedule.DependencyCreated"] = new("Activity dependency added", "تمت إضافة اعتمادية بين الأنشطة"),
        ["Schedule.DependencyDeleted"] = new("Activity dependency removed", "تمت إزالة اعتمادية بين الأنشطة"),
        ["Schedule.BaselineCreated"] = new("Baseline candidate prepared", "تم إعداد خط أساس مقترح"),
        ["Schedule.BaselineDeleted"] = new("Baseline candidate deleted", "تم حذف خط الأساس المقترح"),
        ["Schedule.BaselineSubmitted"] = new("Baseline submitted for approval", "تم تقديم خط الأساس للاعتماد"),
        ["Schedule.BaselineActivated"] = new("Baseline activated", "تم تفعيل خط الأساس"),
        ["Schedule.BaselineSuperseded"] = new("Baseline superseded", "تم استبدال خط الأساس"),
        ["Schedule.BaselineReturned"] = new("Baseline returned", "تمت إعادة خط الأساس"),
        ["Schedule.BaselineRejected"] = new("Baseline rejected", "تم رفض خط الأساس"),
        ["Schedule.BaselineWithdrawn"] = new("Baseline submission withdrawn", "تم سحب تقديم خط الأساس"),
        ["Schedule.MilestoneCreated"] = new("Milestone added", "تمت إضافة معلم رئيسي"),
        ["Schedule.MilestoneChanged"] = new("Milestone updated", "تم تحديث معلم رئيسي"),
        ["Schedule.MilestoneCancelled"] = new("Milestone cancelled", "تم إلغاء معلم رئيسي"),
        ["Schedule.MilestoneAchieved"] = new("Milestone recorded as achieved", "تم تسجيل تحقيق معلم رئيسي"),
        ["Schedule.DeclaredBaselineRecorded"] = new("Declared baseline recorded", "تم تسجيل خط الأساس المُعلَن"),

        // WF-04 Tasks
        ["ProjectTask.TaskCreated"] = new("Task created", "تم إنشاء مهمة"),
        ["ProjectTask.TaskChanged"] = new("Task updated", "تم تحديث مهمة"),
        ["ProjectTask.TaskProgressReported"] = new("Task progress reported", "تم الإبلاغ عن تقدم مهمة"),
        ["ProjectTask.TaskStarted"] = new("Task started", "بدأ تنفيذ مهمة"),
        ["ProjectTask.TaskBlocked"] = new("Task marked as blocked", "تم تعليم مهمة كمتعثرة"),
        ["ProjectTask.TaskUnblocked"] = new("Task unblocked", "تمت إزالة تعثر مهمة"),
        ["ProjectTask.TaskCompleted"] = new("Task completed", "تم إكمال مهمة"),
        ["ProjectTask.TaskReopened"] = new("Task reopened", "تمت إعادة فتح مهمة"),
        ["ProjectTask.TaskCancelled"] = new("Task cancelled", "تم إلغاء مهمة"),
        ["ProjectTask.DependencyCreated"] = new("Task dependency added", "تمت إضافة اعتمادية بين المهام"),
        ["ProjectTask.DependencyDeleted"] = new("Task dependency removed", "تمت إزالة اعتمادية بين المهام"),

        // WF-05 Milestone achievement
        ["Milestone.AchievementStarted"] = new("Milestone achievement claim started", "بدأ إعداد مطالبة بتحقيق معلم"),
        ["Milestone.AchievementChanged"] = new("Milestone achievement claim edited", "تم تعديل مطالبة بتحقيق معلم"),
        ["Milestone.AchievementDeleted"] = new("Milestone achievement claim deleted", "تم حذف مطالبة بتحقيق معلم"),
        ["Milestone.EvidenceAttached"] = new("Achievement evidence attached", "تم إرفاق دليل على التحقيق"),
        ["Milestone.EvidenceWithdrawn"] = new("Achievement evidence withdrawn", "تم سحب دليل على التحقيق"),
        ["Milestone.AchievementSubmitted"] = new("Milestone achievement submitted for acceptance", "تم تقديم تحقيق المعلم للقبول"),
        ["Milestone.AchievementAccepted"] = new("Milestone achievement accepted", "تم قبول تحقيق المعلم"),
        ["Milestone.AchievementReturned"] = new("Milestone achievement returned", "تمت إعادة مطالبة تحقيق المعلم"),
        ["Milestone.AchievementSuperseded"] = new("Milestone achievement superseded", "تم استبدال مطالبة تحقيق المعلم"),

        // WF-06 Risk management
        ["Risk.RiskRegistered"] = new("Risk registered", "تم تسجيل خطر"),
        ["Risk.RiskChanged"] = new("Risk details updated", "تم تحديث بيانات خطر"),
        ["Risk.RiskAssessed"] = new("Risk assessed", "تم تقييم خطر"),
        ["Risk.TreatmentStarted"] = new("Risk treatment started", "بدأت معالجة خطر"),
        ["Risk.MonitoringStarted"] = new("Risk monitoring started", "بدأت مراقبة خطر"),
        ["Risk.RiskAccepted"] = new("Risk accepted", "تم قبول خطر"),
        ["Risk.AcceptanceRevoked"] = new("Risk acceptance revoked", "تم إلغاء قبول خطر"),
        ["Risk.AcceptanceExpired"] = new("Risk acceptance expired", "انتهت صلاحية قبول خطر"),
        ["Risk.RiskClosed"] = new("Risk closed", "تم إغلاق خطر"),
        ["Risk.RiskReopened"] = new("Risk reopened", "تمت إعادة فتح خطر"),
        ["Risk.RiskMaterialised"] = new("Risk materialised into an issue", "تحوّل خطر إلى مشكلة"),
        ["Risk.TreatmentActionCreated"] = new("Treatment action added", "تمت إضافة إجراء معالجة"),
        ["Risk.TreatmentActionChanged"] = new("Treatment action updated", "تم تحديث إجراء معالجة"),
        ["Risk.TreatmentActionStarted"] = new("Treatment action started", "بدأ تنفيذ إجراء معالجة"),
        ["Risk.TreatmentActionCompleted"] = new("Treatment action completed", "تم إكمال إجراء معالجة"),
        ["Risk.TreatmentActionCancelled"] = new("Treatment action cancelled", "تم إلغاء إجراء معالجة"),

        // WF-07 Issues and challenges
        ["ManagementConcern.ConcernRaised"] = new("Issue or challenge raised", "تم رفع مشكلة أو تحدٍّ"),
        ["ManagementConcern.ConcernChanged"] = new("Issue or challenge updated", "تم تحديث مشكلة أو تحدٍّ"),
        ["ManagementConcern.ConcernAssessed"] = new("Issue or challenge assessed", "تم تقييم مشكلة أو تحدٍّ"),
        ["ManagementConcern.ConcernAssigned"] = new("Issue or challenge assigned", "تم إسناد مشكلة أو تحدٍّ"),
        ["ManagementConcern.WorkStarted"] = new("Resolution work started", "بدأ العمل على المعالجة"),
        ["ManagementConcern.ResolutionSubmitted"] = new("Resolution submitted for validation", "تم تقديم المعالجة للتحقق"),
        ["ManagementConcern.ResolutionValidated"] = new("Resolution validated", "تم التحقق من المعالجة"),
        ["ManagementConcern.ResolutionReturned"] = new("Resolution returned", "تمت إعادة المعالجة"),
        ["ManagementConcern.ConcernClosed"] = new("Issue or challenge closed", "تم إغلاق مشكلة أو تحدٍّ"),
        ["ManagementConcern.ConcernReviewed"] = new("Issue or challenge reviewed", "تمت مراجعة مشكلة أو تحدٍّ"),
        ["ManagementConcern.ConcernEscalated"] = new("Issue or challenge escalated", "تم تصعيد مشكلة أو تحدٍّ"),
        ["ManagementConcern.EscalationResolved"] = new("Escalation resolved", "تمت معالجة التصعيد"),
        ["ManagementConcern.EscalationWithdrawn"] = new("Escalation withdrawn", "تم سحب التصعيد"),

        // WF-08 Change requests
        ["ChangeRequest.ChangeRequestCreated"] = new("Change request created", "تم إنشاء طلب تغيير"),
        ["ChangeRequest.ChangeRequestChanged"] = new("Change request updated", "تم تحديث طلب تغيير"),
        ["ChangeRequest.ChangeRequestDeleted"] = new("Change request draft deleted", "تم حذف مسودة طلب تغيير"),
        ["ChangeRequest.ChangeRequestSubmitted"] = new("Change request submitted", "تم تقديم طلب تغيير"),
        ["ChangeRequest.MaterialityEvaluated"] = new("Change materiality evaluated", "تم تقييم جوهرية التغيير"),
        ["ChangeRequest.ReviewStarted"] = new("Change request review started", "بدأت مراجعة طلب التغيير"),
        ["ChangeRequest.ChangeRequestApproved"] = new("Change request approved", "تمت الموافقة على طلب التغيير"),
        ["ChangeRequest.ChangeRequestReturned"] = new("Change request returned", "تمت إعادة طلب التغيير"),
        ["ChangeRequest.ChangeRequestRejected"] = new("Change request rejected", "تم رفض طلب التغيير"),
        ["ChangeRequest.ChangeRequestWithdrawn"] = new("Change request withdrawn", "تم سحب طلب التغيير"),
        ["ChangeRequest.ChangeAuthorizationIssued"] = new("Change authorization issued", "تم إصدار تفويض التغيير"),
        ["ChangeRequest.ImplementationStarted"] = new("Change implementation started", "بدأ تنفيذ التغيير"),
        ["ChangeRequest.ChangeAuthorizationApplied"] = new("Change authorization applied", "تم تطبيق تفويض التغيير"),
        ["ChangeRequest.ChangeRequestImplemented"] = new("Change request implemented", "تم تنفيذ طلب التغيير"),
        ["ChangeRequest.ChangeRequestClosed"] = new("Change request closed", "تم إغلاق طلب التغيير"),

        // WF-09 Suspension and resumption
        ["Suspension.RequestCreated"] = new("Suspension or resumption request created", "تم إنشاء طلب تعليق أو استئناف"),
        ["Suspension.RequestChanged"] = new("Suspension or resumption request updated", "تم تحديث طلب تعليق أو استئناف"),
        ["Suspension.RequestDeleted"] = new("Suspension or resumption request deleted", "تم حذف طلب تعليق أو استئناف"),
        ["Suspension.RequestSubmitted"] = new("Suspension or resumption request submitted", "تم تقديم طلب تعليق أو استئناف"),
        ["Suspension.ReviewStarted"] = new("Suspension or resumption review started", "بدأت مراجعة طلب التعليق أو الاستئناف"),
        ["Suspension.RequestApproved"] = new("Suspension or resumption request approved", "تمت الموافقة على طلب التعليق أو الاستئناف"),
        ["Suspension.RequestReturned"] = new("Suspension or resumption request returned", "تمت إعادة طلب التعليق أو الاستئناف"),
        ["Suspension.RequestRejected"] = new("Suspension or resumption request rejected", "تم رفض طلب التعليق أو الاستئناف"),
        ["Suspension.RequestWithdrawn"] = new("Suspension or resumption request withdrawn", "تم سحب طلب التعليق أو الاستئناف"),
        ["Suspension.RequestEffected"] = new("Suspension or resumption put into effect", "تم تنفيذ التعليق أو الاستئناف"),
        ["Suspension.SuspensionEnded"] = new("Suspension ended with project closure", "انتهى التعليق بإغلاق المشروع"),

        // WF-10 Completion and closure
        ["Closure.CaseCreated"] = new("Completion or closure case created", "تم إنشاء ملف إكمال أو إغلاق"),
        ["Closure.CaseChanged"] = new("Completion or closure case updated", "تم تحديث ملف الإكمال أو الإغلاق"),
        ["Closure.CaseDeleted"] = new("Completion or closure case deleted", "تم حذف ملف الإكمال أو الإغلاق"),
        ["Closure.ReadinessEvaluated"] = new("Closeout readiness evaluated", "تم تقييم جاهزية الإكمال أو الإغلاق"),
        ["Closure.CheckWaived"] = new("Readiness check waived", "تم التنازل عن أحد فحوص الجاهزية"),
        ["Closure.CaseSubmitted"] = new("Completion or closure case submitted", "تم تقديم ملف الإكمال أو الإغلاق"),
        ["Closure.ReviewStarted"] = new("Completion or closure review started", "بدأت مراجعة ملف الإكمال أو الإغلاق"),
        ["Closure.CaseApproved"] = new("Completion or closure case approved", "تمت الموافقة على ملف الإكمال أو الإغلاق"),
        ["Closure.CaseReturned"] = new("Completion or closure case returned", "تمت إعادة ملف الإكمال أو الإغلاق"),
        ["Closure.CaseRejected"] = new("Completion or closure case rejected", "تم رفض ملف الإكمال أو الإغلاق"),
        ["Closure.CaseWithdrawn"] = new("Completion or closure case withdrawn", "تم سحب ملف الإكمال أو الإغلاق"),
        ["Closure.CaseEffected"] = new("Completion or closure put into effect", "تم تنفيذ الإكمال أو الإغلاق"),
        ["Closure.ObligationCreated"] = new("Post-project obligation added", "تمت إضافة التزام لما بعد المشروع"),
        ["Closure.ObligationChanged"] = new("Post-project obligation updated", "تم تحديث التزام لما بعد المشروع"),
        ["Closure.ObligationTransitioned"] = new("Post-project obligation status changed", "تغيّرت حالة التزام لما بعد المشروع"),

        // WF-14 Financial and KPI
        ["FinancialKpi.SourceModeChanged"] = new("Financial source mode changed", "تم تغيير مصدر البيانات المالية"),
        ["FinancialKpi.VersionCreated"] = new("Financial version prepared", "تم إعداد نسخة مالية"),
        ["FinancialKpi.VersionChanged"] = new("Financial version updated", "تم تحديث نسخة مالية"),
        ["FinancialKpi.VersionDeleted"] = new("Financial version draft deleted", "تم حذف مسودة نسخة مالية"),
        ["FinancialKpi.DocumentAttached"] = new("Financial document attached", "تم إرفاق مستند مالي"),
        ["FinancialKpi.DocumentWithdrawn"] = new("Financial document withdrawn", "تم سحب مستند مالي"),
        ["FinancialKpi.VersionSubmitted"] = new("Financial version submitted for approval", "تم تقديم نسخة مالية للاعتماد"),
        ["FinancialKpi.VersionActivated"] = new("Financial version activated", "تم تفعيل نسخة مالية"),
        ["FinancialKpi.VersionSuperseded"] = new("Financial version superseded", "تم استبدال نسخة مالية"),
        ["FinancialKpi.VersionReturned"] = new("Financial version returned", "تمت إعادة نسخة مالية"),
        ["FinancialKpi.VersionRejected"] = new("Financial version rejected", "تم رفض نسخة مالية"),
        ["FinancialKpi.VersionWithdrawn"] = new("Financial version submission withdrawn", "تم سحب تقديم نسخة مالية"),
        ["FinancialKpi.DeclaredBudgetRecorded"] = new("Declared budget recorded", "تم تسجيل الميزانية المُعلَنة"),
        ["FinancialKpi.OpeningSpendRecorded"] = new("Opening spend recorded", "تم تسجيل الصرف الافتتاحي"),
        ["FinancialKpi.UpdateStarted"] = new("Financial update started", "بدأ تحديث البيانات المالية"),
        ["FinancialKpi.UpdateChanged"] = new("Financial update edited", "تم تعديل تحديث البيانات المالية"),
        ["FinancialKpi.UpdateDeleted"] = new("Financial update deleted", "تم حذف تحديث البيانات المالية"),
        ["FinancialKpi.UpdateSubmitted"] = new("Financial update submitted for review", "تم تقديم تحديث البيانات المالية للمراجعة"),
        ["FinancialKpi.UpdateReviewStarted"] = new("Financial update review started", "بدأت مراجعة تحديث البيانات المالية"),
        ["FinancialKpi.UpdateReturned"] = new("Financial update returned", "تمت إعادة تحديث البيانات المالية"),
        ["FinancialKpi.UpdatePublished"] = new("Financial update published", "تم نشر تحديث البيانات المالية"),
        ["FinancialKpi.KpiAssigned"] = new("KPI assigned to the project", "تم إسناد مؤشر أداء إلى المشروع"),
        ["FinancialKpi.AssignmentChanged"] = new("KPI assignment updated", "تم تحديث إسناد مؤشر الأداء"),
        ["FinancialKpi.AssignmentTransitioned"] = new("KPI assignment status changed", "تغيّرت حالة إسناد مؤشر الأداء"),
        ["FinancialKpi.MeasurementRecorded"] = new("KPI measurement recorded", "تم تسجيل قياس لمؤشر أداء"),
        ["FinancialKpi.MeasurementChanged"] = new("KPI measurement edited", "تم تعديل قياس لمؤشر أداء"),
        ["FinancialKpi.MeasurementDeleted"] = new("KPI measurement deleted", "تم حذف قياس لمؤشر أداء"),
        ["FinancialKpi.MeasurementSubmitted"] = new("KPI measurement submitted", "تم تقديم قياس لمؤشر أداء"),
        ["FinancialKpi.MeasurementPublished"] = new("KPI measurement published", "تم نشر قياس لمؤشر أداء"),

        // WF-13 External participation
        ["ExternalParticipation.RequestCreated"] = new("External update request drafted", "تمت صياغة طلب تحديث خارجي"),
        ["ExternalParticipation.RequestChanged"] = new("External update request edited", "تم تعديل طلب تحديث خارجي"),
        ["ExternalParticipation.RequestDeleted"] = new("External update request draft deleted", "تم حذف مسودة طلب تحديث خارجي"),
        ["ExternalParticipation.RequestIssued"] = new("External update request issued", "تم إصدار طلب تحديث خارجي"),
        ["ExternalParticipation.RequestCancelled"] = new("External update request cancelled", "تم إلغاء طلب تحديث خارجي"),
        ["ExternalParticipation.ResponderAssigned"] = new("Responder assigned", "تم تعيين المستجيب"),
        ["ExternalParticipation.ReviewerAssigned"] = new("Reviewer assigned", "تم تعيين المراجع"),
        ["ExternalParticipation.ContributionStarted"] = new("Contribution started", "بدأ إعداد المساهمة"),
        ["ExternalParticipation.ContributionChanged"] = new("Contribution edited", "تم تعديل المساهمة"),
        ["ExternalParticipation.ContributionSubmitted"] = new("Contribution submitted", "تم تقديم المساهمة"),
        ["ExternalParticipation.ReviewStarted"] = new("Contribution review started", "بدأت مراجعة المساهمة"),
        ["ExternalParticipation.ContributionAccepted"] = new("Contribution accepted", "تم قبول المساهمة"),
        ["ExternalParticipation.ContributionReturned"] = new("Contribution returned", "تمت إعادة المساهمة"),
        ["ExternalParticipation.ContributionRejected"] = new("Contribution rejected", "تم رفض المساهمة"),
        ["ExternalParticipation.SourceApplied"] = new("Contribution applied to the source record", "تم تطبيق المساهمة على السجل المصدر"),

        // WF-12 Documents
        ["DocumentManagement.DocumentUploaded"] = new("Document uploaded", "تم رفع مستند"),
        ["DocumentManagement.VersionAdded"] = new("Document version added", "تمت إضافة نسخة من مستند"),
        ["DocumentManagement.MetadataChanged"] = new("Document details updated", "تم تحديث بيانات مستند"),
        ["DocumentManagement.DocumentArchived"] = new("Document archived", "تمت أرشفة مستند"),
        ["DocumentManagement.DocumentLinked"] = new("Document linked", "تم ربط مستند"),
        ["DocumentManagement.DocumentUnlinked"] = new("Document unlinked", "تم إلغاء ربط مستند"),
        ["DocumentManagement.EvidenceDesignated"] = new("Document designated as evidence", "تم اعتماد مستند كدليل"),
        ["DocumentManagement.EvidenceWithdrawn"] = new("Evidence designation withdrawn", "تم سحب اعتماد مستند كدليل"),

        // WF-11 Shared approval
        ["Approval.ApprovalRequested"] = new("Approval requested", "تم طلب الاعتماد"),
        ["Approval.TaskApproved"] = new("Approval step approved", "تمت الموافقة على خطوة الاعتماد"),
        ["Approval.TaskRejected"] = new("Approval step rejected", "تم رفض خطوة الاعتماد"),
        ["Approval.TaskReturned"] = new("Approval step returned", "تمت إعادة خطوة الاعتماد"),
        ["Approval.TaskEscalated"] = new("Approval step escalated", "تم تصعيد خطوة الاعتماد"),
        ["Approval.ApprovalCompleted"] = new("Approval completed", "اكتمل الاعتماد"),
    };
}
