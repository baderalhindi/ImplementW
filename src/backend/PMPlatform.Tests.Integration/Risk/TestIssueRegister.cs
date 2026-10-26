using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>
/// WF-07, played by the tests until TASK-057 builds it: an issue raised from a risk is a row of <c>test_issue.issue</c>, written on
/// the request's context, so it joins the risk's unit of work as WF-07's will (ADR-003 M-11). It refuses
/// <see cref="RiskTestHost.RefusedConcernCategoryId"/> as WF-07 would refuse a rule of its own.
/// </summary>
internal sealed class TestIssueRegister(PMPlatformDbContext context) : IRiskIssueMaterialisation
{
    public const string RefusalCode = "CONCERN_CATEGORY_INVALID";

    public async Task<AdministrationResult<OriginatedIssue>> RaiseIssueAsync(RiskIssueCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CategoryItemId == RiskTestHost.RefusedConcernCategoryId)
        {
            return AdministrationError.Rule(RefusalCode, new FieldIssue("categoryItemId", FieldIssue.NotAllowed));
        }

        OriginatedIssue issue = new(Guid.CreateVersion7(command.RaisedAt), command.ProjectId, command.OriginatingRiskId, "OPEN", command.ActorUserId, command.RaisedAt);
        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO test_issue.issue (id, project_id, originating_risk_id, status, raised_by_user_id, raised_at) VALUES ({issue.Id}, {issue.ProjectId}, {issue.OriginatingRiskId}, {issue.Status}, {issue.RaisedByUserId}, {issue.RaisedAt})",
            cancellationToken);
        return issue;
    }

    public async Task<IReadOnlyList<OriginatedIssue>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
        [
            .. (await context.Database
                .SqlQuery<IssueRow>($"SELECT id, project_id, originating_risk_id, status, raised_by_user_id, raised_at FROM test_issue.issue WHERE originating_risk_id = ANY({riskIds.ToArray()}) ORDER BY id")
                .ToListAsync(cancellationToken))
            .Select(r => new OriginatedIssue(r.Id, r.ProjectId, r.OriginatingRiskId, r.Status, r.RaisedByUserId, r.RaisedAt)),
        ];

    /// <summary>A row as EF Core reads it; the context's snake-case naming maps its columns.</summary>
    private sealed class IssueRow
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public Guid OriginatingRiskId { get; set; }

        public string Status { get; set; } = string.Empty;

        public Guid RaisedByUserId { get; set; }

        public DateTimeOffset RaisedAt { get; set; }
    }
}
