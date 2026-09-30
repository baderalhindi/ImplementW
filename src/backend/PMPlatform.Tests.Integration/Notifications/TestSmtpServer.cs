using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// A minimal SMTP relay in the test process (RFC 5321: greeting, EHLO, MAIL, RCPT, DATA, QUIT), so the real e-mail adapter
/// is exercised over a real socket without another container. <see cref="StopAsync"/> is the workbook's "kill the email
/// provider connection": the port stops accepting, and every attempt fails to connect until <see cref="RestartAsync"/>.
/// </summary>
public sealed class TestSmtpServer : IAsyncDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _stopping;
    private Task? _accepting;

    public int Port { get; private set; }

    /// <summary>Every message accepted, in order: its envelope recipients and its raw content.</summary>
    public ConcurrentQueue<ReceivedEmail> Received { get; } = new();

    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["EXCHANGE_SMTP_HOST"] = "127.0.0.1",
        ["EXCHANGE_SMTP_PORT"] = Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Notifications:Email:FromAddress"] = "pmplatform-noreply@notifications.test",
        ["Notifications:Email:UseTls"] = "false",
        ["Notifications:Email:Timeout"] = "00:00:05",
    };

    public static async Task<TestSmtpServer> StartAsync()
    {
        TestSmtpServer server = new();
        await server.StartAsync(port: 0);
        return server;
    }

    /// <summary>Listens again, on the same port.</summary>
    public Task RestartAsync() => _listener is null ? StartAsync(Port) : Task.CompletedTask;

    public async Task StopAsync()
    {
        if (_listener is null)
        {
            return;
        }

        await _stopping!.CancelAsync();
        _listener.Stop();
        try
        {
            await _accepting!;
        }
        catch (OperationCanceledException)
        {
        }

        _listener = null;
    }

    public IReadOnlyList<ReceivedEmail> To(string address) => [.. Received.Where(m => m.Recipients.Contains(address, StringComparer.OrdinalIgnoreCase))];

    public async ValueTask DisposeAsync() => await StopAsync();

    private Task StartAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _stopping = new CancellationTokenSource();
        TcpListener listener = _listener;
        CancellationToken token = _stopping.Token;
        _accepting = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(token);
                }
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                _ = Task.Run(() => ServeAsync(client, token), token);
            }
        }, token);
        return Task.CompletedTask;
    }

    private async Task ServeAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            using StreamReader reader = new(stream, Encoding.UTF8);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 test.smtp ESMTP ready");
            List<string> recipients = [];
            while (!token.IsCancellationRequested && await reader.ReadLineAsync(token) is { } line)
            {
                string command = line.Length >= 4 ? line[..4].ToUpperInvariant() : line.ToUpperInvariant();
                switch (command)
                {
                    case "EHLO":
                        await writer.WriteLineAsync("250 test.smtp");
                        break;
                    case "HELO":
                    case "MAIL":
                    case "RSET":
                    case "NOOP":
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "RCPT":
                        recipients.Add(line[(line.IndexOf('<', StringComparison.Ordinal) + 1)..line.IndexOf('>', StringComparison.Ordinal)]);
                        await writer.WriteLineAsync("250 OK");
                        break;
                    case "DATA":
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                        StringBuilder data = new();
                        while (await reader.ReadLineAsync(token) is { } dataLine && dataLine != ".")
                        {
                            data.Append(dataLine.StartsWith("..", StringComparison.Ordinal) ? dataLine[1..] : dataLine).Append("\r\n");
                        }

                        Received.Enqueue(new ReceivedEmail([.. recipients], data.ToString()));
                        recipients.Clear();
                        await writer.WriteLineAsync("250 OK queued");
                        break;
                    case "QUIT":
                        await writer.WriteLineAsync("221 Bye");
                        return;
                    default:
                        await writer.WriteLineAsync("502 Command not implemented");
                        break;
                }
            }
        }
    }
}

/// <summary>One accepted message: its RCPT TO addresses and its content as sent. The relay offers no 8BITMIME, so the content is 7-bit MIME.</summary>
public sealed record ReceivedEmail(IReadOnlyList<string> Recipients, string Content);
