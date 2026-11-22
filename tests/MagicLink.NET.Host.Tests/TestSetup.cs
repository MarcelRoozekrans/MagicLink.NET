using System.Text.Json;

namespace MagicLink.Host.Tests;

/// <summary>
/// The settings the host runs with in the tests: in-process here, and in CI the same values for the container, which CI
/// passes as environment variables, in the container-image job of .github/workflows/ci.yml.
/// </summary>
internal sealed class TestSetup : IDisposable
{
    /// <summary>A public client: a CLI or a single-page app, with PKCE and refresh tokens.</summary>
    public const string PublicClient = "host-test-cli";

    /// <summary>A confidential client: a web app with a secret.</summary>
    public const string ConfidentialClient = "host-test-web";

    public const string ClientSecret = "host-test-secret";

    /// <summary>A CLI that signs in with the device flow only, without a redirect URI.</summary>
    public const string DeviceClient = "host-test-device";

    public const string RedirectUri = "http://localhost/callback";

    /// <summary>The person the file lets sign in, with <see cref="Group"/>.</summary>
    public const string Person = "host-person@example.com";

    public const string Group = "host-users";

    /// <summary>An address the file does not list.</summary>
    public const string Outsider = "outsider@example.com";

    public static readonly Uri InProcessIssuer = new("https://localhost/");

    public TestSetup()
    {
        Directory.CreateDirectory(DirectoryPath);
        WritePeople(PeopleFile, Person, Group);
        SigningThumbprint = TestCertificates.WriteSigning(SigningCertificate);
        TestCertificates.WriteEncryption(EncryptionCertificate);
    }

    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "magiclink-host-" + Guid.NewGuid().ToString("N"));

    public string PeopleFile => Path.Combine(DirectoryPath, "people.json");

    public string SigningCertificate => Path.Combine(DirectoryPath, "signing.pfx");

    public string EncryptionCertificate => Path.Combine(DirectoryPath, "encryption.pfx");

    public string SigningThumbprint { get; }

    /// <summary>Settings with which the host starts, as environment variables name them, for the in-process host.</summary>
    public Dictionary<string, string?> Settings(string storeConnectionString, int smtpPort) => new(StringComparer.Ordinal)
    {
        ["MagicLink:Issuer"] = InProcessIssuer.AbsoluteUri,
        ["MagicLink:Store:ConnectionString"] = storeConnectionString,
        ["MagicLink:SigningCertificate:Path"] = SigningCertificate,
        ["MagicLink:SigningCertificate:Password"] = TestCertificates.Password,
        ["MagicLink:EncryptionCertificate:Path"] = EncryptionCertificate,
        ["MagicLink:EncryptionCertificate:Password"] = TestCertificates.Password,
        ["MagicLink:SignInFile:Path"] = PeopleFile,
        ["MagicLink:Smtp:Host"] = "127.0.0.1",
        ["MagicLink:Smtp:Port"] = smtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["MagicLink:Smtp:Security"] = "None",
        ["MagicLink:Smtp:From"] = "Sign-in <signin@example.com>",
        ["MagicLink:Clients:0:ClientId"] = PublicClient,
        ["MagicLink:Clients:0:RedirectUris:0"] = RedirectUri,
        ["MagicLink:Clients:0:Scopes:0"] = "email",
        ["MagicLink:Clients:0:Scopes:1"] = "offline_access",
        ["MagicLink:Clients:1:ClientId"] = ConfidentialClient,
        ["MagicLink:Clients:1:ClientSecret"] = ClientSecret,
        ["MagicLink:Clients:1:RedirectUris:0"] = RedirectUri,
        ["MagicLink:Clients:2:ClientId"] = DeviceClient,
        ["MagicLink:Clients:2:DeviceFlow"] = "true",
        ["MagicLink:Clients:2:Scopes:0"] = "offline_access",
    };

    public static void WritePeople(string path, string email, string group) =>
        File.WriteAllText(path, JsonSerializer.Serialize(new { people = new[] { new { email, groups = new[] { group } } } }));

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}
