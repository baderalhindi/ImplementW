using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>
/// The test database behind a TCP relay a test can cut, to simulate a database outage (TASK-091). <see cref="Refuse"/> drops every
/// relayed connection and resets each new one, as a stopped database does; <see cref="Silence"/> drops them and accepts new ones that
/// are never answered, as a host that stops responding does; <see cref="Restore"/> relays again. <see cref="ConnectionString"/> turns
/// pooling off, so every probe opens its own connection through the relay and sees the outage at once.
/// </summary>
internal sealed class DatabaseRelay : IAsyncDisposable
{
    private enum Mode
    {
        Relay,
        Refuse,
        Silence,
    }

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentDictionary<TcpClient, byte> _connections = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly Task _accepting;
    private volatile Mode _mode = Mode.Relay;

    public DatabaseRelay(string connectionString)
    {
        NpgsqlConnectionStringBuilder target = new(connectionString);
        _targetHost = target.Host!;
        _targetPort = target.Port;
        _listener.Start();
        ConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Host = IPAddress.Loopback.ToString(),
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
            Pooling = false,
        }.ConnectionString;
        _accepting = AcceptAsync();
    }

    public string ConnectionString { get; }

    public void Refuse() => Cut(Mode.Refuse);

    public void Silence() => Cut(Mode.Silence);

    public void Restore() => _mode = Mode.Relay;

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        DropAll();
        await _accepting.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _stopping.Dispose();
    }

    private void Cut(Mode mode)
    {
        _mode = mode;
        DropAll();
    }

    private void DropAll()
    {
        foreach (TcpClient connection in _connections.Keys)
        {
            Drop(connection);
        }
    }

    /// <summary>Resets the connection rather than closing it gracefully, as a database that went away does. Once per connection.</summary>
    private void Drop(TcpClient connection)
    {
        if (!_connections.TryRemove(connection, out _))
        {
            return;
        }

        connection.Client.LingerState = new LingerOption(true, 0);
        connection.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _connections[client] = 0;
            switch (_mode)
            {
                case Mode.Refuse:
                    Drop(client);
                    break;
                case Mode.Silence:
                    break;
                case Mode.Relay:
                    _ = RelayAsync(client);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown relay mode {_mode}.");
            }
        }
    }

    private async Task RelayAsync(TcpClient client)
    {
        using TcpClient server = new();
        try
        {
            await server.ConnectAsync(_targetHost, _targetPort, _stopping.Token);
            _connections[server] = 0;
            NetworkStream fromClient = client.GetStream();
            NetworkStream fromServer = server.GetStream();
            await Task.WhenAny(fromClient.CopyToAsync(fromServer, _stopping.Token), fromServer.CopyToAsync(fromClient, _stopping.Token));
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The relay was cut or either end closed; both ends are dropped below.
        }
        finally
        {
            Drop(client);
            Drop(server);
        }
    }
}
