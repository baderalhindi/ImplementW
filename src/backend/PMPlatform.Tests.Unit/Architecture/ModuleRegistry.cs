namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>
/// The 21 modules (ADR-003 §4.3) and the code-level references ADR-003 §8 allows between them.
/// A new module or edge is a revision of ADR-003 first and of this file second — never the reverse.
/// </summary>
/// <remarks>
/// Edges are recorded as <c>(referrer, referenced)</c>, i.e. which module's code may reference which module's
/// <c>Contracts</c>. For a query or command edge that is the ADR's From → To. For an event edge it is the
/// reverse: the consumer references the producer's <c>Contracts/Events</c> (event-conventions EV-8), so
/// "Project → Schedule (event)" is the reference Schedule → Project.
/// </remarks>
internal static class ModuleRegistry
{
    public static readonly IReadOnlySet<string> Modules = new HashSet<string>(StringComparer.Ordinal)
    {
        "Project", "Progress", "Schedule", "ProjectTask", "Milestone", "Risk", "ManagementConcern",
        "ChangeRequest", "Suspension", "Closure", "Approval", "DocumentManagement", "ExternalParticipation",
        "FinancialKpi", "Notifications", "Dashboards", "Reports", "IdentityAccess", "MasterDataConfig",
        "IntegrationMonitoring", "AuditActivity",
    };

    /// <summary>E-U1 and E-U2: every module may query IdentityAccess and MasterDataConfig.</summary>
    /// <remarks>
    /// E-U3 (audit) and E-U4 (notification intent) cross via the outbox envelope kinds in Application/Common,
    /// not via a module reference, so they add no edge here.
    /// </remarks>
    public static readonly IReadOnlySet<string> UniversalTargets = new HashSet<string>(StringComparer.Ordinal)
    {
        "IdentityAccess", "MasterDataConfig",
    };

    /// <summary>ADR-003 §8.2, as code-level references. The number is the §8.2 row.</summary>
    public static readonly IReadOnlySet<(string Referrer, string Referenced)> SpecificEdges = new HashSet<(string, string)>
    {
        ("Progress", "Project"),                     // 1 query; 35 event consumer
        ("Schedule", "Project"),                     // 2 query; 35 event consumer
        ("Risk", "Project"),                         // 3
        ("ManagementConcern", "Project"),            // 4
        ("FinancialKpi", "Project"),                 // 5 query; 35 event consumer
        ("ChangeRequest", "Project"),                // 6
        ("Suspension", "Project"),                   // 7 command
        ("Closure", "Project"),                      // 8 command
        ("ProjectTask", "Schedule"),                 // 9
        ("Milestone", "Schedule"),                   // 10 split-authority contract (ICD-04), WF-05 → WF-03 only: query the
                                                     //    shared milestone, record it ACHIEVED on acceptance (TASK-050)
        ("Schedule", "ChangeRequest"),               // 11
        ("FinancialKpi", "ChangeRequest"),           // 12
        ("FinancialKpi", "Progress"),                // 13
        ("Closure", "FinancialKpi"),                 // 14
        ("Risk", "ManagementConcern"),               // 15 command
        ("Milestone", "DocumentManagement"),         // 16 command
        ("ExternalParticipation", "DocumentManagement"), // 17 command
        ("ExternalParticipation", "Project"),        // 18
        ("ExternalParticipation", "ProjectTask"),    // 19 command: an accepted external report of a task's percentage, applied
                                                     //    through WF-04's ITaskProgressContributions (TASK-066). The target set is
                                                     //    what TASK-066 built; another target is another row here (S-5).
        ("Project", "Approval"),                     // 20 command; 28 outcome-event consumer
        ("Schedule", "Approval"),                    // 21; 28
        ("ChangeRequest", "Approval"),               // 22; 28
        ("Suspension", "Approval"),                  // 23; 28
        ("ManagementConcern", "Approval"),           // 24; 28
        ("Milestone", "Approval"),                   // 25 (inferred, S-4); 28
        ("FinancialKpi", "Approval"),                // 26 (inferred, S-4); 28
        ("Closure", "Approval"),                     // 27 (inferred, S-4); 28
        ("Dashboards", "Progress"),                  // 29 read projection
        ("Dashboards", "Schedule"),                  // 29
        ("Dashboards", "Risk"),                      // 29
        ("Dashboards", "FinancialKpi"),              // 29
        ("Reports", "Dashboards"),                   // 30
        ("IntegrationMonitoring", "IdentityAccess"), // 31 event consumer
        ("IntegrationMonitoring", "Notifications"),  // 32 event consumer
        ("IntegrationMonitoring", "ExternalParticipation"), // 33 event consumer
        ("IntegrationMonitoring", "AuditActivity"),  // 34 query
        ("Milestone", "Project"),                    // 35 event consumer
        ("ProjectTask", "Project"),                  // 36 query
        ("FinancialKpi", "DocumentManagement"),      // 38 command: an Approved Budget version's referenced document (TASK-052)
        ("Closure", "ProjectTask"),                  // 39 query: WF-10's readiness, the tasks still to be dispositioned (TASK-063)
        ("Closure", "Schedule"),                     // 40 query: open baseline candidates, unachieved milestones (TASK-063)
        ("Closure", "Milestone"),                    // 41 query: achievement claims still on their way (TASK-063)
        ("Closure", "Risk"),                         // 42 query: risks neither closed nor accepted (TASK-063)
        ("Closure", "ManagementConcern"),            // 43 query: issues and challenges not resolved (TASK-063)
        ("Closure", "ChangeRequest"),                // 44 query: change requests not carried through (TASK-063)
        ("Closure", "Suspension"),                   // 45 query, command: open requests; the open suspension ended on terminal closure (TASK-063)
        ("Closure", "Progress"),                     // 46 query: unpublished progress, and whether any is published (TASK-063)
    };

    public static bool Allows(string referrer, string referenced) =>
        referrer != referenced && (UniversalTargets.Contains(referenced) || SpecificEdges.Contains((referrer, referenced)));

    /// <summary>The module a namespace belongs to: its first segment that names a registered module, else null.</summary>
    public static string? ModuleOf(string ns) => ns.Split('.').FirstOrDefault(Modules.Contains);
}
