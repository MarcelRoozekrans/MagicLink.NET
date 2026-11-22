using MagicLink.PostgreSql;

namespace MagicLink.Host;

/// <summary>
/// The container's settings, under <c>MagicLink</c> in configuration: environment variables such as
/// <c>MagicLink__Issuer</c>, or <c>appsettings.json</c>. Bound by the configuration binding source generator, which runs
/// under Native AOT.
/// </summary>
internal sealed class HostConfiguration
{
    /// <summary>The configuration section that holds these settings.</summary>
    public const string Section = "MagicLink";

    /// <summary>The address people and applications reach MagicLink.NET on; the links in the sign-in email start with it.</summary>
    public string? Issuer { get; set; }

    /// <summary>The PostgreSQL store that holds OpenIddict's data and the sign-in requests.</summary>
    public MagicLinkPostgreSqlOptions Store { get; set; } = new();

    /// <summary>The certificate that signs tokens.</summary>
    public CertificateSettings SigningCertificate { get; set; } = new();

    /// <summary>The certificate that encrypts the tokens only MagicLink.NET reads, such as authorization codes.</summary>
    public CertificateSettings EncryptionCertificate { get; set; } = new();

    /// <summary>The file of who may sign in, when that is the source.</summary>
    public MagicLinkSignInFileOptions SignInFile { get; set; } = new();

    /// <summary>The PostgreSQL database of who may sign in, when that is the source.</summary>
    public MagicLinkSignInDatabaseOptions SignInDatabase { get; set; } = new();

    /// <summary>The applications that may sign people in through MagicLink.NET.</summary>
    public List<ClientSettings> Clients { get; set; } = [];
}
