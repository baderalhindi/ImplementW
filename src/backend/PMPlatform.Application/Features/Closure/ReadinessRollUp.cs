using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// The backend-authoritative roll-up (WF-10 §6.3, CLO-CC-14) of one case's readiness records: its latest evaluation — the rows sharing the
/// newest evaluation time — with each failed, waivable criterion WAIVED by the case's latest waiver of it. A waiver stands for the case, so
/// a criterion accepted once stays accepted while it keeps failing, and is irrelevant once it passes.
/// </summary>
internal static class ReadinessRollUp
{
    public static readonly ReadinessDetail NotEvaluated = new(ReadinessStatus.Incomplete, null, []);

    public static ReadinessDetail Of(IEnumerable<ReadinessCheck> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        List<ReadinessCheck> rows = [.. records];
        List<ReadinessCheck> evaluations = [.. rows.Where(r => r.Result != ReadinessResult.Waived)];
        if (evaluations.Count == 0)
        {
            return NotEvaluated;
        }

        DateTimeOffset latest = evaluations.Max(r => r.EvaluatedAt);
        Dictionary<ReadinessCheckCode, ReadinessCheck> waivers = rows
            .Where(r => r.Result == ReadinessResult.Waived)
            .GroupBy(r => r.CheckCode)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.EvaluatedAt).ThenBy(r => r.Id).Last());

        List<ReadinessCheckDetail> checks = [.. evaluations
            .Where(r => r.EvaluatedAt == latest)
            .OrderBy(r => r.CheckCode)
            .Select(r => r.Result == ReadinessResult.Fail && ReadinessPolicy.IsWaivable(r.CheckCode) && waivers.TryGetValue(r.CheckCode, out ReadinessCheck? waiver)
                ? new ReadinessCheckDetail(r.CheckCode, ReadinessResult.Waived, r.BlockingCount, true, waiver.WaivedByUserId, waiver.Detail)
                : new ReadinessCheckDetail(r.CheckCode, r.Result, r.BlockingCount, ReadinessPolicy.IsWaivable(r.CheckCode), null, null))];

        ReadinessStatus status = checks.Any(c => c.Result == ReadinessResult.Fail) ? ReadinessStatus.NotReady
            : checks.Any(c => c.Result == ReadinessResult.Waived) ? ReadinessStatus.ReadyWithConditions
            : ReadinessStatus.Ready;
        return new ReadinessDetail(status, latest, checks);
    }
}
