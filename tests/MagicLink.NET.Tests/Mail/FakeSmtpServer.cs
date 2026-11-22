using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MagicLink.Tests.Mail;

/// <summary>
/// An SMTP server on the loopback address that speaks just enough SMTP for MailKit, without TLS, with STARTTLS or with
/// TLS from the first byte, and keeps what it receives.
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
    /// <param name="withholdDataReply">Whether the server never answers the end of an email, as a stalled server would.</param>
    /// <param name="tls">Whether and how the server offers TLS.</param>
    public FakeSmtpServer(
        bool advertiseLogin = false, bool refuseRecipients = false, bool withholdDataReply = false, FakeSmtpTls tls = FakeSmtpTls.None)
    {
        Tls = tls;
        Certificate = tls == FakeSmtpTls.None ? null : CreateCertificate();
        AdvertiseLogin = advertiseLogin;
        RefuseRecipients = refuseRecipients;
        WithholdDataReply = withholdDataReply;
        _listener.Start();
        _serving = ServeAsync();
    }

    public FakeSmtpTls Tls { get; }

    /// <summary>The server's own certificate, for "localhost", which no system trusts; null without TLS.</summary>
    public X509Certificate2? Certificate { get; }

    public bool AdvertiseLogin { get; }

    public bool RefuseRecipients { get; }

    public bool WithholdDataReply { get; }

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
        Certificate?.Dispose();
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
            Stream stream = client.GetStream();
            var secure = false;
            if (Tls == FakeSmtpTls.Implicit)
            {
                stream = await SecureAsync(stream);
                secure = true;
            }

            var (reader, writer) = Talk(stream);
            var current = new Received { Secure = secure };
            await writer.WriteLineAsync("220 fake ESMTP");
            while (await reader.ReadLineAsync() is { } line)
            {
                var verb = line.Split(' ', 2)[0].ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                        await writer.WriteLineAsync(Hello(secure));
                        break;

                    case "STARTTLS" when Tls == FakeSmtpTls.StartTls && !secure:
                        await writer.WriteLineAsync("220 Ready to start TLS");
                        stream = await SecureAsync(stream);
                        secure = true;
                        (reader, writer) = Talk(stream);
                        current = new Received { Secure = true };
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
                        current = new Received { Secure = secure };
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

    /// <summary>The answer to EHLO: what the server offers, STARTTLS only before the connection is secure.</summary>
    private string Hello(bool secure)
    {
        var startTls = Tls == FakeSmtpTls.StartTls && !secure ? "250-STARTTLS\r\n" : string.Empty;
        var login = AdvertiseLogin ? "250-AUTH LOGIN\r\n" : string.Empty;
        return $"250-fake\r\n{startTls}{login}250 OK";
    }

    /// <summary>Reads and writes SMTP lines on the stream; leaves the stream open, since STARTTLS replaces the reader.</summary>
    private static (StreamReader Reader, StreamWriter Writer) Talk(Stream stream) =>
        (new StreamReader(stream, Encoding.ASCII, leaveOpen: true),
         new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true });

    private async Task<Stream> SecureAsync(Stream stream)
    {
        var tls = new SslStream(stream, leaveInnerStreamOpen: false);
        await tls.AuthenticateAsServerAsync(Certificate!, clientCertificateRequired: false, checkCertificateRevocation: false);
        return tls;
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), password: null);
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

        if (WithholdDataReply)
        {
            await Task.Delay(Timeout.Infinite, _stop.Token);
        }

        await writer.WriteLineAsync("250 OK");
    }

    private static string Decode(string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));

    internal sealed class Received
    {
        /// <summary>Whether the email came in over TLS.</summary>
        public bool Secure { get; set; }

        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        public string Data { get; set; } = string.Empty;
    }
}
