using MagicLink.Host.Tests.Mail;
using MagicLink.PostgreSql.Tests;
using MagicLink.Tests.Mail;

namespace MagicLink.Host.Tests;

/// <summary>
/// The container's host for the flow tests. When MAGICLINK_HOST_URL is set, the tests call that URL, where CI runs the
/// native binary or the container with the settings of <see cref="TestSetup"/>, and read the email from the Mailpit at
/// MAGICLINK_HOST_MAILPIT_URL. Otherwise the host runs in-process on a database of its own, on the server named by
/// MAGICLINK_TEST_POSTGRES, and sends its email to an SMTP server in the test.
/// </summary>
public sealed class HostUnderTest : IAsyncLifetime
{
    public const string UrlVariable = "MAGICLINK_HOST_URL";
    public const string MailpitVariable = "MAGICLINK_HOST_MAILPIT_URL";
    public const string IssuerVariable = "MAGICLINK_HOST_ISSUER";
    public const string SigningThumbprintVariable = "MAGICLINK_HOST_SIGNING_THUMBPRINT";

    private readonly PostgresDatabase _database = new();
    private TestSetup? _setup;
    private FakeSmtpServer? _smtp;
    private HostFactory? _factory;
    private HttpClient? _client;
    private HttpClient? _mailpit;
    private ITestMailbox? _mailbox;

    /// <summary>A client without cookies; each <see cref="TestBrowser"/> keeps its own.</summary>
    public HttpClient Client => _client ?? throw Unavailable();

    internal ITestMailbox Mailbox => _mailbox ?? throw Unavailable();

    public Uri Issuer { get; private set; } = TestSetup.InProcessIssuer;

    /// <summary>The SHA-1 thumbprint of the certificate the host signs tokens with.</summary>
    public string SigningThumbprint { get; private set; } = string.Empty;

    /// <summary>The in-process host's services, or <see langword="null"/> for a host CI started.</summary>
    public IServiceProvider? Services => _factory?.Services;

    public async ValueTask InitializeAsync()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        if (!string.IsNullOrWhiteSpace(url))
        {
            UseStartedHost(new Uri(url));
            return;
        }

        await _database.InitializeAsync();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresDatabase.Variable)))
        {
            return;
        }

        _setup = new TestSetup();
        _smtp = new FakeSmtpServer();
        _mailbox = new SmtpTestMailbox(_smtp);
        SigningThumbprint = _setup.SigningThumbprint;
        _factory = new HostFactory(_setup.Settings(_database.ConnectionString, _smtp.Port));
        _client = _factory.CreateHttpsClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _mailpit?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_smtp is not null)
        {
            await _smtp.DisposeAsync();
        }

        _setup?.Dispose();
        await _database.DisposeAsync();
    }

    private void UseStartedHost(Uri url)
    {
        Issuer = new Uri(Required(IssuerVariable));
        SigningThumbprint = Required(SigningThumbprintVariable);

        // CI serves the host over HTTPS with a certificate it made for the run and added to the machine's trusted roots.
#pragma warning disable CA2000 // The client owns its handler and disposes it with itself.
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }, disposeHandler: true)
        {
            BaseAddress = url,
        };
#pragma warning restore CA2000
        _mailpit = new HttpClient { BaseAddress = new Uri(Required(MailpitVariable)) };
        _mailbox = new MailpitTestMailbox(_mailpit);
    }

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{variable} must be set next to {UrlVariable}.");

    private static Exception Unavailable()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{PostgresDatabase.Variable} is not set. CI must run the host's tests on PostgreSQL.");
        }

        Assert.Skip($"Set {PostgresDatabase.Variable} to a PostgreSQL connection string to run the host's tests.");
        return null!; // Assert.Skip throws.
    }
}
