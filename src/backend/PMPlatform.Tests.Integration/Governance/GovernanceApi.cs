using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;

namespace PMPlatform.Tests.Integration.Governance;

/// <summary>
/// What the TASK-065 suite shares: the three tags, the governance writes the document lists, and requests sent at the same moment. It
/// imports no module's driver, so each test file brings in the one driver of the host it runs on (their extension methods share names).
/// </summary>
internal static class GovernanceApi
{
    public const string ChangeRequestTag = "ChangeRequest";
    public const string SuspensionTag = "Suspension";
    public const string ClosureTag = "Closure";

    public static readonly string[] Tags = [ChangeRequestTag, SuspensionTag, ClosureTag];

    /// <summary>The write operations of <paramref name="tag"/> — every POST, PUT, PATCH and DELETE — as <c>METHOD path</c>.</summary>
    public static IEnumerable<string> Writes(this OpenApiDocument document, string tag) =>
        document.Operations(tag).Where(o => o.Name.Split(' ')[0] is "POST" or "PUT" or "PATCH" or "DELETE").Select(o => o.Name);

    /// <summary>
    /// Runs <paramref name="count"/> calls released together behind one gate — as retries racing on the network, or two workers — and
    /// returns their results in order.
    /// </summary>
    public static async Task<T[]> AtOnceAsync<T>(int count, Func<int, Task<T>> call)
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<T>[] calls = [.. Enumerable.Range(0, count).Select(async i =>
        {
            await gate.Task;
            return await call(i);
        })];
        gate.SetResult();
        return await Task.WhenAll(calls);
    }

    /// <summary>The answer of a request sent at the same moment as others: its status and R-27 code, the response disposed.</summary>
    public static async Task<Answer> AnswerAsync(Task<HttpResponseMessage> sending)
    {
        using HttpResponseMessage response = await sending;
        string text = await response.Content.ReadAsStringAsync();
        string? code = text.Length > 0 && JsonNode.Parse(text) is JsonObject body ? body["code"]?.GetValue<string>() : null;
        return new Answer(response.StatusCode, code, text);
    }

    /// <summary>
    /// Every project in the collection's database that breaks the single-ActiveSuspension rule: more than one open active suspension, or a
    /// SUSPENDED state without exactly one, or an open one of a project that is not SUSPENDED. Empty at every moment the rule holds.
    /// </summary>
    public static Task<IReadOnlyList<string>> SuspensionViolationsAsync(this IdentityDatabase database) =>
        database.QueryAsync("""
            SELECT p.id || ' ' || p.lifecycle_state || ' open=' || count(s.id)
            FROM project.project p LEFT JOIN suspension.active_suspension s ON s.project_id = p.id AND s.ended_at IS NULL
            GROUP BY p.id, p.lifecycle_state
            HAVING count(s.id) > 1 OR (p.lifecycle_state = 'SUSPENDED') <> (count(s.id) = 1)
            """);
}

/// <summary>A response's status, its R-27 code when it is a refusal, and its body.</summary>
internal sealed record Answer(HttpStatusCode Status, string? Code, string Body)
{
    public bool Succeeded => (int)Status is >= 200 and < 300;

    public override string ToString() => $"{(int)Status} {Code}";
}
