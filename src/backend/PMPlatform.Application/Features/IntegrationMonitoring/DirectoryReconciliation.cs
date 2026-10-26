using System.Globalization;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The directory reconciliation (TASK-075; sso-directory-integration.md F-6): reads the directory's three authoritative attributes for each
/// person the platform links to it and compares them with the platform's copy (<see cref="DirectoryComparison"/>). It applies nothing —
/// the attributes are copied only at sign-in, by IdentityAccess (D-6) — so a mismatch is counted and reported as an operational alert for
/// investigation, and the authoritative data stays as it was (BR-INT-006, DC-INT-09). A directory that cannot be read ends the attempt as a
/// technical failure, which the engine retries; a person the directory does not know is a mismatch.
/// </summary>
internal sealed class DirectoryReconciliation(IDirectoryReconciliationSource directory) : IIntegrationOperation
{
    public const string Code = "RECONCILE";

    /// <summary>How many people the alert's detail names before it only counts.</summary>
    public const int NamedPeople = 20;

    public string IntegrationCode => IntegrationCodes.Directory;

    public string OperationCode => Code;

    public SyncRunType? RunType => SyncRunType.Reconciliation;

    public bool IsAvailable => directory.IsConfigured;

    public async Task<IntegrationOperationResult> ExecuteAsync(IntegrationOperationContext context, CancellationToken cancellationToken)
    {
        if (!directory.IsConfigured)
        {
            return IntegrationOperationResult.Failure(InvocationFailureCategory.Technical, "NOT_CONFIGURED", new IntegrationRunOutcome(SyncRunStatus.Failed, 0, 0, 0, 0, null));
        }

        IReadOnlyList<DirectoryLinkedPerson> people = await directory.ListLinkedPeopleAsync(cancellationToken).ConfigureAwait(false);
        int read = 0;
        List<(Guid UserId, IReadOnlyList<string> Differences)> mismatches = [];
        foreach (DirectoryLinkedPerson person in people)
        {
            DirectoryReading reading = await directory.ReadAsync(person.SubjectId, cancellationToken).ConfigureAwait(false);
            if (reading.Outcome is DirectoryReadingOutcome.Unavailable or DirectoryReadingOutcome.NotConfigured)
            {
                // Nothing after this person was compared: a partial comparison is not a reconciliation.
                return IntegrationOperationResult.Failure(
                    InvocationFailureCategory.Technical, "UNREACHABLE",
                    new IntegrationRunOutcome(read > 0 ? SyncRunStatus.Partial : SyncRunStatus.Failed, read, 0, people.Count - read, mismatches.Count, Detail(mismatches, people.Count)));
            }

            read++;
            IReadOnlyList<string> differences = DirectoryComparison.Differences(person, reading);
            if (differences.Count > 0)
            {
                mismatches.Add((person.UserId, differences));
            }
        }

        return IntegrationOperationResult.Success(new IntegrationRunOutcome(SyncRunStatus.Succeeded, read, 0, 0, mismatches.Count, Detail(mismatches, people.Count)));
    }

    /// <summary>
    /// The counts per attribute and the first <see cref="NamedPeople"/> people by user id with the attributes that differ: enough to
    /// investigate, and no value (personal data stays where it is).
    /// </summary>
    public static string? Detail(IReadOnlyList<(Guid UserId, IReadOnlyList<string> Differences)> mismatches, int compared)
    {
        if (mismatches.Count == 0)
        {
            return null;
        }

        string counts = string.Join(", ", new[] { DirectoryComparison.JobTitle, DirectoryComparison.Department, DirectoryComparison.Manager, DirectoryComparison.NotInDirectory }
            .Select(a => $"{a} {mismatches.Count(m => m.Differences.Contains(a)).ToString(CultureInfo.InvariantCulture)}"));
        string named = string.Join("; ", mismatches.Take(NamedPeople).Select(m => $"{m.UserId}: {string.Join(",", m.Differences)}"));
        string more = mismatches.Count > NamedPeople ? $"; and {(mismatches.Count - NamedPeople).ToString(CultureInfo.InvariantCulture)} more" : string.Empty;
        return $"{mismatches.Count.ToString(CultureInfo.InvariantCulture)} of {compared.ToString(CultureInfo.InvariantCulture)} people differ from the directory ({counts}): {named}{more}";
    }
}
