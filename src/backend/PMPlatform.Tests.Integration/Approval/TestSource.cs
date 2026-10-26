using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Approval;

/// <summary>
/// A source module played by the tests: one table of approvable records and its approval outcome handler. The handler
/// is deliberately naive — it counts every call and has no idempotency of its own — so that a count of 1 proves the
/// framework delivered the outcome exactly once, not that the source forgave a duplicate.
/// </summary>
internal static class TestSource
{
    public const string Module = "TestSource";
    public const string Type = "Approvable";

    public const string Schema = """
        CREATE SCHEMA test_source;
        CREATE TABLE test_source.approvable (
            id uuid PRIMARY KEY,
            revision_no integer NOT NULL,
            status text NOT NULL,
            applied_count integer NOT NULL DEFAULT 0,
            last_outcome_key text);
        """;

    public static string Insert(Guid id) => $"INSERT INTO test_source.approvable (id, revision_no, status) VALUES ('{id}', 1, 'SUBMITTED');";

    /// <summary>The record's state and how many times an outcome was applied to it, as "status|count".</summary>
    public static string StateOf(Guid id) => $"SELECT status || '|' || applied_count FROM test_source.approvable WHERE id = '{id}'";
}

/// <summary>Records the handler is told to fail on, once each, after it has written: the write must be rolled back.</summary>
public sealed class TestSourceFailures
{
    private readonly ConcurrentDictionary<Guid, byte> _failOnce = new();

    public void FailOnce(Guid subjectId) => _failOnce[subjectId] = 0;

    public bool ShouldFail(Guid subjectId) => _failOnce.TryRemove(subjectId, out _);
}

internal sealed class TestSourceOutcomeHandler(PMPlatformDbContext context, TestSourceFailures failures) : IApprovalOutcomeHandler
{
    public string SubjectModule => TestSource.Module;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        string status = outcome.Data.Decision.ToString().ToUpperInvariant();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE test_source.approvable
            SET status = {status}, applied_count = applied_count + 1, last_outcome_key = {outcome.IdempotencyKey}
            WHERE id = {outcome.Subject.Id} AND revision_no = {outcome.Subject.RevisionNo}
            """,
            cancellationToken);

        if (failures.ShouldFail(outcome.Subject.Id))
        {
            throw new InvalidOperationException("Injected failure after the source's write.");
        }
    }
}
