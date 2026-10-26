using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Domain.IntegrationMonitoring;
using PMPlatform.Tests.Integration.AuditActivity;
using PMPlatform.Tests.Integration.Identity;
using static PMPlatform.Tests.Integration.IntegrationMonitoring.IntegrationMonitoringApi;

namespace PMPlatform.Tests.Integration.IntegrationMonitoring;

/// <summary>
/// TASK-075 acceptance criterion 3: retries are idempotent and correlation-traceable end to end. A lineage is one operation — its key begins
/// it once, its attempts share its correlation id, a retry is a new attempt of it and never a second operation, and a lineage that succeeded
/// is never retried. Driven through the engine and the API with an operation whose outcome the test sets.
/// </summary>
[Collection(IntegrationMonitoringSuite.Name)]
public sealed class InvocationEngineTests(IntegrationMonitoringTestHost host)
{
    [Fact]
    public async Task ALineageIsBegunOnceByItsKey()
    {
        string key = $"once-{Guid.NewGuid():N}";
        host.Operation.Outcome = _ => IntegrationOperationResult.Success();

        Invocation first = await StartAsync(key);
        Invocation again = await StartAsync(key);

        Assert.Equal(first.Id, again.Id);
        Assert.Single(host.Operation.CallsFor(key));
        Assert.Equal(1, host.Operation.EffectsFor(key));
    }

    [Fact]
    public async Task ATechnicalFailureIsRetriedUnderTheLineagesKeyAndCorrelationIdThenDeadLetteredThenRetriedByAnOperator()
    {
        string key = $"retry-{Guid.NewGuid():N}";
        Guid correlationId = Guid.NewGuid();
        using HttpClient administrator = await host.ClientAsync(1, Guid.NewGuid());
        host.Operation.Outcome = _ => TestOperation.Unreachable();
        try
        {
            Invocation first = await StartAsync(key, correlationId);
            Assert.Equal(InvocationStatus.RetryScheduled, first.Status);
            Assert.Equal(first.CompletedAt!.Value.AddSeconds(60), first.NextAttemptAt);

            // Not before its back-off.
            await host.RunPassAsync();
            Assert.Single(host.Operation.CallsFor(key));

            host.Clock.Advance(TimeSpan.FromSeconds(61));
            await host.RunPassAsync();
            host.Clock.Advance(TimeSpan.FromSeconds(121));
            await host.RunPassAsync();

            Assert.Equal(
                ["1|FAILED|TECHNICAL", "2|FAILED|TECHNICAL", "3|DEAD_LETTER|TECHNICAL"],
                await host.Database.QueryAsync($"SELECT attempt_no || '|' || status || '|' || failure_category FROM integration_monitoring.invocation WHERE idempotency_key = '{key}' ORDER BY attempt_no"));
            Assert.All(host.Operation.CallsFor(key), call => Assert.Equal((correlationId, correlationId), (call.CorrelationId, call.AmbientCorrelationId)));
            Assert.Equal([1, 2, 3], host.Operation.CallsFor(key).Select(c => (int)c.AttemptNo));
            Assert.Contains(await host.LiveAlertsAsync(TestOperation.Code), a => a.StartsWith("DEAD_LETTER|", StringComparison.Ordinal));

            // An operator retries the dead letter; the provider is back. The retry is the lineage's fourth attempt, under its correlation id.
            host.Operation.Outcome = _ => IntegrationOperationResult.Success();
            string deadLetter = Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM integration_monitoring.invocation WHERE idempotency_key = '{key}' AND attempt_no = 3"));
            JsonObject retried = await (await administrator.SendSensitiveAsync($"{Invocations}/{deadLetter}/retry")).SuccessAsync();

            Assert.Equal(("SUCCEEDED", 4), (retried["invocation"]!["status"].Text(), retried["invocation"]!["attemptNo"]!.GetValue<int>()));
            Assert.Equal(correlationId.ToString(), retried["invocation"]!["correlationId"].Text());
            Assert.Equal(4, retried["lineage"]!.AsArray().Count);
            Assert.False(retried["retryPermitted"]!.GetValue<bool>());
            Assert.Equal(1, host.Operation.EffectsFor(key));

            // A lineage that succeeded is never retried; an older attempt of it is not its lineage's to retry.
            using HttpResponseMessage succeeded = await administrator.SendSensitiveAsync($"{Invocations}/{retried["invocation"]!["id"].Text()}/retry");
            using HttpResponseMessage older = await administrator.SendSensitiveAsync($"{Invocations}/{deadLetter}/retry");
            Assert.Equal((HttpStatusCode.Conflict, "INVOCATION_ALREADY_SUCCEEDED"), (succeeded.StatusCode, (await succeeded.ReadAsync<Problem>()).Code));
            Assert.Equal((HttpStatusCode.Conflict, "INVOCATION_ALREADY_SUCCEEDED"), (older.StatusCode, (await older.ReadAsync<Problem>()).Code));
            Assert.Equal(1, host.Operation.EffectsFor(key));

            // The operator's retry is integration audit, recorded in the operator's request with the lineage's correlation id named.
            Assert.Contains(
                await host.Database.QueryAsync($"""
                    SELECT e.event_class || '|' || a.new_value FROM audit_activity.audit_event e
                    JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id AND a.attribute_name = 'lineage_correlation_id'
                    WHERE e.event_type = 'IntegrationMonitoring.InvocationRetried' AND e.subject_id = '{retried["invocation"]!["id"].Text()}'
                    """),
                e => e == $"INTEGRATION|{correlationId}");
        }
        finally
        {
            host.Clock.Reset();
            host.Operation.Outcome = _ => IntegrationOperationResult.Success();
        }
    }

    /// <summary>A security refusal is dead-lettered at once (it needs a person); two operators' retries of it make one new attempt.</summary>
    [Fact]
    public async Task TwoOperatorsRetryingOneDeadLetterMakeOneAttempt()
    {
        string key = $"race-{Guid.NewGuid():N}";
        host.Operation.Outcome = _ => IntegrationOperationResult.Failure(InvocationFailureCategory.Security, "REJECTED");
        try
        {
            Invocation first = await StartAsync(key);
            Assert.Equal(InvocationStatus.DeadLetter, first.Status);
            host.Operation.Outcome = _ => IntegrationOperationResult.Success();

            using HttpClient one = await host.ClientAsync(1);
            using HttpClient two = await host.ClientAsync(1);
            HttpResponseMessage[] answers = await Task.WhenAll(
                one.SendSensitiveAsync($"{Invocations}/{first.Id}/retry"), two.SendSensitiveAsync($"{Invocations}/{first.Id}/retry"));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], answers.Select(a => a.StatusCode).Order());
            Assert.Equal(["1|FAILED", "2|SUCCEEDED"], await host.Database.QueryAsync($"SELECT attempt_no || '|' || status FROM integration_monitoring.invocation WHERE idempotency_key = '{key}' ORDER BY attempt_no"));
            Assert.Equal(1, host.Operation.EffectsFor(key));
            foreach (HttpResponseMessage answer in answers)
            {
                answer.Dispose();
            }
        }
        finally
        {
            host.Operation.Outcome = _ => IntegrationOperationResult.Success();
        }
    }

    /// <summary>
    /// Two due retries of one instance end in one pass although the instance row changes between them, as telemetry another process records
    /// changes it: each retry reads the instance afresh under its lock, never the copy the pass's scope already tracks.
    /// </summary>
    [Fact]
    public async Task DueRetriesOfOneInstanceEndInOnePassWhileTheInstanceChangesUnderneath()
    {
        string first = $"under-{Guid.NewGuid():N}";
        string second = $"under-{Guid.NewGuid():N}";
        host.Operation.Outcome = _ => TestOperation.Unreachable();
        try
        {
            await StartAsync(first);
            await StartAsync(second);
            host.Operation.Outcome = _ =>
            {
                host.Database.ExecuteAsync($"UPDATE integration_monitoring.integration_instance SET updated_at = now() WHERE id = '{IntegrationMonitoringTestHost.TestSystemInstanceId}'")
                    .GetAwaiter().GetResult();
                return IntegrationOperationResult.Success();
            };

            host.Clock.Advance(TimeSpan.FromSeconds(61));
            await host.RunPassAsync();

            Assert.Equal(
                ["1|FAILED", "1|FAILED", "2|SUCCEEDED", "2|SUCCEEDED"],
                await host.Database.QueryAsync($"SELECT attempt_no || '|' || status FROM integration_monitoring.invocation WHERE idempotency_key IN ('{first}', '{second}') ORDER BY attempt_no, status"));
        }
        finally
        {
            host.Clock.Reset();
            host.Operation.Outcome = _ => IntegrationOperationResult.Success();
        }
    }

    [Fact]
    public async Task AnAttemptAnotherModuleMadeIsRetriedByThatModuleNotByFg05()
    {
        using HttpClient administrator = await host.ClientAsync(1);
        Guid delivery = Guid.NewGuid();
        await host.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<Application.Common.Events.IOutbox>()
                .RecordAsync(Telemetry.EmailAttempt(delivery, 1, DateTimeOffset.UtcNow, dead: true), CancellationToken.None);
            return true;
        });
        await host.DispatchAsync();
        string observed = Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM integration_monitoring.invocation WHERE idempotency_key = '{delivery}'"));

        using HttpResponseMessage retry = await administrator.SendSensitiveAsync($"{Invocations}/{observed}/retry");
        JsonObject detail = await (await administrator.GetAsync($"{Invocations}/{observed}")).SuccessAsync();

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "RETRY_NOT_PERMITTED"), (retry.StatusCode, (await retry.ReadAsync<Problem>()).Code));
        Assert.False(detail["retryPermitted"]!.GetValue<bool>());
        Assert.Equal("Notifications", detail["invocation"]!["sourceModule"].Text());
    }

    /// <summary>
    /// End to end: one request's correlation id is on the reconciliation's attempt, its run, its audit, every directory read the run made
    /// (published by IdentityAccess as telemetry) and the outbound MFA call of the sign-in — and one search by it returns the lineage.
    /// </summary>
    [Fact]
    public async Task OneCorrelationIdTracesARequestThroughEveryAttemptRunAuditEventAndProviderCall()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient administrator = host.Api.CreateClient().WithCorrelationId(correlationId);
        administrator.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", (await administrator.SignInOrFailAsync(1)).AccessToken);
        Assert.Contains(correlationId.ToString(), host.Identity.MultiFactorProvider.CorrelationIds);

        JsonObject run = await (await administrator.SendSensitiveAsync(Reconciliations, new { integrationId = await host.DefinitionIdAsync("DIRECTORY") })).SuccessAsync();
        await host.DispatchAsync();

        Assert.Equal(correlationId.ToString(), run["invocation"]!["correlationId"].Text());
        Assert.Equal([correlationId.ToString()], await host.Database.QueryAsync($"SELECT correlation_id::text FROM integration_monitoring.sync_run WHERE id = '{run["invocation"]!["syncRunId"].Text()}'"));
        JsonArray lineage = (await (await administrator.GetAsync($"{Invocations}?correlationId={correlationId}&pageSize=200")).SuccessAsync())["items"]!.AsArray();
        string[] operations = [.. lineage.Select(i => $"{i!["integrationCode"].Text()} {i["operationCode"].Text()}").Distinct().Order(StringComparer.Ordinal)];
        Assert.Equal(["DIRECTORY AUTHENTICATE", "DIRECTORY READ_PERSON", "DIRECTORY RECONCILE", "MFA START_CHALLENGE", "MFA VERIFY_CHALLENGE"], operations);
        Assert.All(lineage, i => Assert.Equal(correlationId.ToString(), i!["correlationId"].Text()));
        Assert.Contains(
            await host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE correlation_id = '{correlationId}'"),
            e => e == "IntegrationMonitoring.ReconciliationCompleted");
    }

    private Task<Invocation> StartAsync(string key, Guid? correlationId = null) =>
        host.WithScopeAsync(async services =>
        {
            using IDisposable scope = CorrelationScope.Begin(correlationId ?? Guid.NewGuid());
            IIntegrationMonitoringRepository repository = services.GetRequiredService<IIntegrationMonitoringRepository>();
            IntegrationDefinition definition = (await repository.FindDefinitionByCodeAsync(TestOperation.Code, CancellationToken.None))!;
            IntegrationInvocationEngine engine = services.GetRequiredService<IntegrationInvocationEngine>();
            return await engine.StartAsync(
                definition, engine.OperationFor(TestOperation.Code, TestOperation.Operation)!, key,
                attempt => new AuditEntry(Domain.Common.AuditEventClass.Integration, "IntegrationMonitoring.ReconciliationRequested", Domain.Common.AuditOutcome.Success)
                {
                    ActorUserId = IntegrationMonitoringServicePrincipal.Id,
                    ActorType = Domain.Common.AuditActorType.Service,
                    Subject = new AuditSubject("IntegrationMonitoring", nameof(Invocation), attempt.Id),
                },
                CancellationToken.None);
        });
}
