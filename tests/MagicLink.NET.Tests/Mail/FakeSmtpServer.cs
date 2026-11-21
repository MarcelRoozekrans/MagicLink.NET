using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MagicLink.Tests.Mail;

/// <summary>
/// An SMTP server on the loopback address that speaks just enough SMTP for SmtpClient, without TLS, and keeps what
/// it receives.
/// </summary>
internal sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;
    private readonly Lock _lock = new();
    private readonly List<Received> _received = [];

    /// <param name="advertiseLogin">Whether the server asks clients to sign in with AUTH LOGIN.</param>
    /// <param name="refuseRecipients">Whether the server refuses every recipient, as for an address it does not know.</param>
    public FakeSmtpServer(bool advertiseLogin = false, bool refuseRecipients = false)
    {
        AdvertiseLogin = advertiseLogin;
        RefuseRecipients = refuseRecipients;
        _listener.Start();
        _serving = ServeAsync();
    }

    public bool AdvertiseLogin { get; }

    public bool RefuseRecipients { get; }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public IReadOnlyList<Received> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _received];
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _serving;
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }

        _stop.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            _ = Task.Run(() => TalkAsync(client));
        }
    }

    private async Task TalkAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            var current = new Received();
            await writer.WriteLineAsync("220 fake ESMTP");
            while (await reader.ReadLineAsync() is { } line)
            {
                var verb = line.Split(' ', 2)[0].ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                        await writer.WriteLineAsync(AdvertiseLogin ? "250-fake\r\n250-AUTH LOGIN\r\n250 OK" : "250-fake\r\n250 OK");
                        break;

                    case "AUTH":
                        await SignInAsync(line, reader, writer, current);
                        break;

                    case "MAIL":
                        current.From = line;
                        await writer.WriteLineAsync("250 OK");
                        break;

                    case "RCPT":
                        current.To = line;
                        await writer.WriteLineAsync(RefuseRecipients ? "550 5.1.1 No such user" : "250 OK");
                        break;

                    case "DATA":
                        await ReceiveAsync(reader, writer, current);
                        current = new Received();
                        break;

                    case "QUIT":
                        await writer.WriteLineAsync("221 Bye");
                        return;

                    default:
                        await writer.WriteLineAsync("250 OK");
                        break;
                }
            }
        }
    }

    /// <summary>AUTH LOGIN, with the user name on the AUTH line or after the server asks for it.</summary>
    private static async Task SignInAsync(string line, StreamReader reader, StreamWriter writer, Received current)
    {
        var parts = line.Split(' ');
        if (parts.Length > 2)
        {
            current.UserName = Decode(parts[2]);
        }
        else
        {
            await writer.WriteLineAsync("334 VXNlcm5hbWU6");
            current.UserName = Decode(await reader.ReadLineAsync() ?? string.Empty);
        }

        await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
        current.Password = Decode(await reader.ReadLineAsync() ?? string.Empty);
        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
    }

    /// <summary>DATA: reads the email up to the line with only a dot, and keeps it.</summary>
    private async Task ReceiveAsync(StreamReader reader, StreamWriter writer, Received current)
    {
        await writer.WriteLineAsync("354 Go ahead");
        var data = new StringBuilder();
        while (await reader.ReadLineAsync() is { } dataLine && !string.Equals(dataLine, ".", StringComparison.Ordinal))
        {
            data.Append(dataLine).Append("\r\n");
        }

        current.Data = data.ToString();
        lock (_lock)
        {
            _received.Add(current);
        }

        await writer.WriteLineAsync("250 OK");
    }

    private static string Decode(string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));

    internal sealed class Received
    {
        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        public string Data { get; set; } = string.Empty;
    }
}
