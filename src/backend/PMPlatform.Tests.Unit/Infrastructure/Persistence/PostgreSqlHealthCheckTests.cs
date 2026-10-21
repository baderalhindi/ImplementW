using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// TASK-091: the readiness probe behind the anonymous endpoints — one at a time, bounded by its own timeout, and not cancelled by a caller
/// that gives up. The database is a listener that counts connections and either never answers or closes them at once. GSS encryption is
/// disabled because under its default, Prefer, Npgsql retries any failed handshake — a timeout included — on a new socket, and whether that
/// socket connects before the expired timeout stops it is a race; without it, one probe is one connection.
/// </summary>
public sealed class PostgreSqlHealthCheckTests
{
    private static readonly HealthCheckContext Context = new();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(PostgreSqlHealthCheck.TimeoutSeconds);

    [Fact]
    public async Task ConcurrentChecksShareOneProbeAndADatabaseThatNeverAnswersIsUnhealthyWithinTheTimeout()
    {
        using FakeDatabase database = new(holdConnections: true);
        PostgreSqlHealthCheck check = new(database.Configuration);

        Stopwatch elapsed = Stopwatch.StartNew();
        HealthCheckResult[] results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => check.CheckHealthAsync(Context)));

        Assert.All(results, result => Assert.Equal(HealthStatus.Unhealthy, result.Status));
        Assert.All(results, result => Assert.Equal("PostgreSQL unreachable.", result.Description));
        Assert.InRange(elapsed.Elapsed, Timeout, Timeout + TimeSpan.FromSeconds(2));
        Assert.Equal(1, database.Connections);
    }

    /// <summary>A caller that disconnects stops waiting; the probe it shared runs on, and the others get its real result.</summary>
    [Fact]
    public async Task ACallerThatGivesUpDoesNotCancelTheProbeOthersAreWaitingFor()
    {
        using FakeDatabase database = new(holdConnections: true);
        PostgreSqlHealthCheck check = new(database.Configuration);
        using CancellationTokenSource leaving = new();

        Task<HealthCheckResult> left = check.CheckHealthAsync(Context, leaving.Token);
        Task<HealthCheckResult> stayed = check.CheckHealthAsync(Context);
        await leaving.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => left);
        Stopwatch elapsed = Stopwatch.StartNew();
        HealthCheckResult result = await stayed;
        Assert.Equal("PostgreSQL unreachable.", result.Description);
        Assert.True(elapsed.Elapsed > Timeout - TimeSpan.FromSeconds(1), $"The shared probe ended after {elapsed.Elapsed}, with the caller who left.");
        Assert.Equal(1, database.Connections);
    }

    [Fact]
    public async Task AResultIsSharedUntilTheNextProbeIsDue()
    {
        using FakeDatabase database = new(holdConnections: false);
        PostgreSqlHealthCheck check = new(database.Configuration);

        HealthCheckResult first = await check.CheckHealthAsync(Context);
        int afterFirst = database.Connections;
        HealthCheckResult shared = await check.CheckHealthAsync(Context);
        Assert.Equal(afterFirst, database.Connections);

        await Task.Delay(PostgreSqlHealthCheck.ReuseFor + TimeSpan.FromMilliseconds(100));
        HealthCheckResult next = await check.CheckHealthAsync(Context);

        Assert.Equal([HealthStatus.Unhealthy, HealthStatus.Unhealthy, HealthStatus.Unhealthy], new[] { first.Status, shared.Status, next.Status });
        Assert.Equal("PostgreSQL unreachable.", first.Description);
        Assert.True(database.Connections > afterFirst, "No new probe after ReuseFor.");
    }

    [Fact]
    public async Task WithoutAConnectionStringTheDatabaseIsUnhealthy()
    {
        PostgreSqlHealthCheck check = new(new ConfigurationBuilder().Build());

        HealthCheckResult result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("DB_CONNECTION_STRING is not configured.", result.Description);
    }

    /// <summary>A listener in place of PostgreSQL: it holds each connection open without a word, or closes it at once.</summary>
    private sealed class FakeDatabase : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly List<TcpClient> _held = [];
        private int _connections;

        public FakeDatabase(bool holdConnections)
        {
            _listener.Start();
            int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [PostgreSqlHealthCheck.ConnectionStringKey] = $"Host=127.0.0.1;Port={port};Database=probe;Username=probe;Password=probe;Pooling=false;SSL Mode=Disable;GSS Encryption Mode=Disable",
                })
                .Build();
            _ = AcceptAsync(closeAtOnce: !holdConnections);
        }

        public IConfiguration Configuration { get; }

        public int Connections => Volatile.Read(ref _connections);

        public void Dispose()
        {
            _listener.Stop();
            lock (_held)
            {
                _held.ForEach(client => client.Dispose());
            }
        }

        private async Task AcceptAsync(bool closeAtOnce)
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
                {
                    return;
                }

                Interlocked.Increment(ref _connections);
                if (closeAtOnce)
                {
                    client.Dispose();
                    continue;
                }

                lock (_held)
                {
                    _held.Add(client);
                }
            }
        }
    }
}
