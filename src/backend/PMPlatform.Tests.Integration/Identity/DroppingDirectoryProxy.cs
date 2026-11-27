using System.Net;
using System.Net.Sockets;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The test directory behind a loopback relay that resets the first <c>drops</c> connections it accepts, each once its first request is
/// in, and relays every later one unchanged: a network that loses a connection now and then, as CI's did (LdapException 91, Connect
/// Error, on a sign-in).
/// </summary>
internal sealed class DroppingDirectoryProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Uri _directory = new(TestDirectory.Url);
    private readonly Task _accepting;
    private int _dropsLeft;
    private int _accepted;

    public DroppingDirectoryProxy(int drops)
    {
        _dropsLeft = drops;
        _listener.Start();
        _accepting = AcceptAsync();
    }

    /// <summary>The directory URL that reaches the test directory through this relay.</summary>
    public string Url => $"ldap://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{_directory.AbsolutePath}";

    /// <summary>Connections accepted so far, dropped ones included.</summary>
    public int Accepted => Volatile.Read(ref _accepted);

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        await _accepting.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _stopping.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            Socket client = await _listener.AcceptSocketAsync(_stopping.Token);
            Interlocked.Increment(ref _accepted);
            _ = Interlocked.Decrement(ref _dropsLeft) >= 0 ? ResetAfterFirstRequestAsync(client) : RelayAsync(client);
        }
    }

    /// <summary>
    /// Takes the client's first request (the service account's bind), then resets the connection while the client waits for the answer.
    /// A connection reset before any request is sent is reconnected by the LDAP client itself; one lost while an answer is awaited is not.
    /// </summary>
    private async Task ResetAfterFirstRequestAsync(Socket client)
    {
        using Socket dropped = client;
        _ = await dropped.ReceiveAsync(new byte[1024], SocketFlags.None, _stopping.Token);

        // A zero linger makes Close send RST: the connection is reset, not ended.
        dropped.LingerState = new LingerOption(true, 0);
    }

    private async Task RelayAsync(Socket client)
    {
        using Socket inbound = client;
        using TcpClient outbound = new();
        await outbound.ConnectAsync(_directory.Host, _directory.Port, _stopping.Token);
        await using NetworkStream fromClient = new(inbound, ownsSocket: false);
        await using NetworkStream toDirectory = outbound.GetStream();
        using CancellationTokenSource either = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        await Task.WhenAny(fromClient.CopyToAsync(toDirectory, either.Token), toDirectory.CopyToAsync(fromClient, either.Token));
        await either.CancelAsync();
    }
}
