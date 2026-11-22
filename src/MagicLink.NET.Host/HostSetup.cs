using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using ZeroAlloc.Results;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.Host;

/// <summary>
/// The container's settings, checked: the host starts only from a setup without problems, and lists every problem at
/// once, each named as the environment variable that sets it.
/// </summary>
internal sealed class HostSetup
{
    private HostSetup(HostConfiguration configuration, Uri issuer, X509Certificate2 signing, X509Certificate2 encryption, IReadOnlyList<OpenIddictApplicationDescriptor> clients)
    {
        Configuration = configuration;
        Issuer = issuer;
        SigningCertificate = signing;
        EncryptionCertificate = encryption;
        Clients = clients;
    }

    /// <summary>The settings as configured.</summary>
    public HostConfiguration Configuration { get; }

    /// <summary>The issuer.</summary>
    public Uri Issuer { get; }

    /// <summary>The certificate that signs tokens.</summary>
    public X509Certificate2 SigningCertificate { get; }

    /// <summary>The certificate that encrypts tokens.</summary>
    public X509Certificate2 EncryptionCertificate { get; }

    /// <summary>The applications that may sign people in, as OpenIddict registers them.</summary>
    public IReadOnlyList<OpenIddictApplicationDescriptor> Clients { get; }

    /// <summary>Whether who may sign in comes from a database rather than a file.</summary>
    public bool UsesSignInDatabase => HasDatabase(Configuration.SignInDatabase);

    /// <summary>Checks <paramref name="configuration"/>, and loads the certificates it names.</summary>
    public static Result<HostSetup> Read(HostConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var problems = new List<string>();
        var issuer = ReadIssuer(configuration.Issuer, problems);
        if (string.IsNullOrWhiteSpace(configuration.Store.ConnectionString))
        {
            problems.Add("Set MagicLink__Store__ConnectionString to the PostgreSQL database that holds MagicLink.NET's data.");
        }

        var signing = ReadCertificate(configuration.SigningCertificate, "SigningCertificate", "sign tokens", problems);
        var encryption = ReadCertificate(configuration.EncryptionCertificate, "EncryptionCertificate", "encrypt tokens", problems);
        CheckSource(configuration, problems);
        var clients = ReadClients(configuration.Clients, problems);

        if (problems.Count > 0)
        {
            signing?.Dispose();
            encryption?.Dispose();
            return Result<HostSetup>.Failure("MagicLink.NET cannot start:" + string.Concat(problems.Select(static problem => Environment.NewLine + "- " + problem)));
        }

        return Result<HostSetup>.Success(new HostSetup(configuration, issuer!, signing!, encryption!, clients));
    }

    private static Uri? ReadIssuer(string? value, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add("Set MagicLink__Issuer to the address people and applications reach MagicLink.NET on, such as https://signin.example.com/.");
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var issuer)
            || !string.Equals(issuer.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || issuer.Query.Length > 0
            || issuer.Fragment.Length > 0)
        {
            problems.Add($"MagicLink__Issuer must be an absolute https address without a query, such as https://signin.example.com/, not '{value}'.");
            return null;
        }

        return issuer;
    }

    private static X509Certificate2? ReadCertificate(CertificateSettings settings, string name, string purpose, List<string> problems)
    {
        var path = $"MagicLink__{name}__Path";
        if (string.IsNullOrWhiteSpace(settings.Path))
        {
            problems.Add($"Set {path} to a PKCS#12 file, .pfx, with the certificate and private key to {purpose} with, and MagicLink__{name}__Password to its password.");
            return null;
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12FromFile(settings.Path, settings.Password);
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            problems.Add($"{path} names '{settings.Path}', which cannot be read as a PKCS#12 file with MagicLink__{name}__Password: {exception.Message}");
            return null;
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            problems.Add($"{path} names '{settings.Path}', a certificate without its private key, so MagicLink.NET cannot {purpose} with it.");
            return null;
        }

        return certificate;
    }

    private static void CheckSource(HostConfiguration configuration, List<string> problems)
    {
        var file = !string.IsNullOrWhiteSpace(configuration.SignInFile.Path);
        var database = HasDatabase(configuration.SignInDatabase);
        if (file && database)
        {
            problems.Add("Set only one source of who may sign in: MagicLink__SignInFile__Path, or MagicLink__SignInDatabase__ConnectionString with MagicLink__SignInDatabase__Query, not both.");
        }
        else if (!file && !database)
        {
            problems.Add("Set MagicLink__SignInFile__Path to the file of who may sign in, or MagicLink__SignInDatabase__ConnectionString and MagicLink__SignInDatabase__Query to read them from a PostgreSQL database.");
        }
    }

    private static bool HasDatabase(MagicLinkSignInDatabaseOptions options) =>
        !string.IsNullOrWhiteSpace(options.ConnectionString) || !string.IsNullOrWhiteSpace(options.Query);

    private static List<OpenIddictApplicationDescriptor> ReadClients(List<ClientSettings> clients, List<string> problems)
    {
        var descriptors = new List<OpenIddictApplicationDescriptor>(clients.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < clients.Count; index++)
        {
            var descriptor = ReadClient(clients[index], $"MagicLink__Clients__{index}__", seen, problems);
            if (descriptor is not null)
            {
                descriptors.Add(descriptor);
            }
        }

        return descriptors;
    }

    private static OpenIddictApplicationDescriptor? ReadClient(ClientSettings client, string prefix, HashSet<string> seen, List<string> problems)
    {
        var count = problems.Count;
        if (string.IsNullOrWhiteSpace(client.ClientId))
        {
            problems.Add($"Set {prefix}ClientId to the application's client id.");
        }
        else if (!seen.Add(client.ClientId))
        {
            problems.Add($"{prefix}ClientId is '{client.ClientId}', which an earlier client already has.");
        }

        var confidential = !string.IsNullOrEmpty(client.ClientSecret);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = confidential ? client.ClientSecret : null,
            ClientType = confidential ? ClientTypes.Confidential : ClientTypes.Public,
            DisplayName = string.IsNullOrWhiteSpace(client.DisplayName) ? null : client.DisplayName,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        if (client.RedirectUris.Count == 0)
        {
            problems.Add($"Set {prefix}RedirectUris__0 to the address MagicLink.NET sends the person back to with the authorization code.");
        }

        for (var index = 0; index < client.RedirectUris.Count; index++)
        {
            // On Linux a path such as /callback parses as an absolute file URI, which is no address to redirect to.
            if (!Uri.TryCreate(client.RedirectUris[index], UriKind.Absolute, out var redirect) || redirect.IsFile || redirect.Fragment.Length > 0)
            {
                problems.Add($"{prefix}RedirectUris__{index} must be an absolute address without a fragment, not '{client.RedirectUris[index]}'.");
            }
            else if (HasIssParameter(redirect))
            {
                // MagicLink.NET adds the issuer as iss to the redirect (RFC 9207), so OpenIddict refuses a redirect that has one.
                problems.Add($"{prefix}RedirectUris__{index} must not have an iss parameter in its query, which MagicLink.NET adds itself: '{client.RedirectUris[index]}'.");
            }
            else
            {
                descriptor.RedirectUris.Add(redirect);
            }
        }

        for (var index = 0; index < client.Scopes.Count; index++)
        {
            AddScope(descriptor, client.Scopes[index], $"{prefix}Scopes__{index}", problems);
        }

        return problems.Count == count ? descriptor : null;
    }

    private static bool HasIssParameter(Uri redirect) =>
        QueryHelpers.ParseQuery(redirect.Query).ContainsKey(Parameters.Iss);

    private static void AddScope(OpenIddictApplicationDescriptor descriptor, string scope, string setting, List<string> problems)
    {
        switch (scope)
        {
            case Scopes.OpenId:
                // Every client may ask for openid; OpenIddict needs no permission for it.
                break;

            case Scopes.Email:
                descriptor.Permissions.Add(Permissions.Scopes.Email);
                break;

            case Scopes.OfflineAccess:
                descriptor.Permissions.Add(Permissions.Prefixes.Scope + Scopes.OfflineAccess);
                descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
                break;

            default:
                problems.Add($"{setting} is '{scope}'; a client may ask for {Scopes.Email} and {Scopes.OfflineAccess}, besides {Scopes.OpenId}.");
                break;
        }
    }
}
