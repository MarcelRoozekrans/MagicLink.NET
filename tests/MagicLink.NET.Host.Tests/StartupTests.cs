namespace MagicLink.Host.Tests;

// The container's host refuses to start while a setting is missing or wrong, and names each one as the environment
// variable that sets it, as MagicLink.NET does when hosted in an app (issue #12).
public sealed class StartupTests : IDisposable
{
    private const string UnusedStore = "Host=localhost;Database=unused";

    private readonly TestSetup _setup = new();

    public void Dispose() => _setup.Dispose();

    [Fact]
    public void Without_settings_the_host_names_every_one_it_needs()
    {
        var error = StartFailure(new Dictionary<string, string?>(StringComparer.Ordinal));

        Assert.Contains("MagicLink__Issuer", error, StringComparison.Ordinal);
        Assert.Contains("MagicLink__Store__ConnectionString", error, StringComparison.Ordinal);
        Assert.Contains("MagicLink__SigningCertificate__Path", error, StringComparison.Ordinal);
        Assert.Contains("MagicLink__EncryptionCertificate__Path", error, StringComparison.Ordinal);
        Assert.Contains("MagicLink__SignInFile__Path", error, StringComparison.Ordinal);
        Assert.Contains("MagicLink__SignInDatabase__ConnectionString", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MagicLink:Issuer", "signin.example.com", "MagicLink__Issuer")]
    [InlineData("MagicLink:Issuer", "http://signin.example.com/", "MagicLink__Issuer")]
    [InlineData("MagicLink:Issuer", "https://signin.example.com/?tenant=1", "MagicLink__Issuer")]
    [InlineData("MagicLink:SigningCertificate:Path", "/does/not/exist.pfx", "MagicLink__SigningCertificate__Path")]
    [InlineData("MagicLink:SigningCertificate:Password", "wrong", "MagicLink__SigningCertificate__Path")]
    [InlineData("MagicLink:EncryptionCertificate:Path", "/does/not/exist.pfx", "MagicLink__EncryptionCertificate__Path")]
    [InlineData("MagicLink:SignInDatabase:ConnectionString", "Host=localhost", "only one")]
    [InlineData("MagicLink:SignInDatabase:Query", "SELECT NULL", "only one")]
    [InlineData("MagicLink:Clients:0:ClientId", "", "MagicLink__Clients__0__ClientId")]
    [InlineData("MagicLink:Clients:0:RedirectUris:0", "/callback", "MagicLink__Clients__0__RedirectUris__0")]
    [InlineData("MagicLink:Clients:0:RedirectUris:0", "http://app.example/callback", "MagicLink__Clients__0__RedirectUris__0")]
    [InlineData("MagicLink:Clients:0:RedirectUris:0", "http://localhost/callback?iss=https://other.example/", "MagicLink__Clients__0__RedirectUris__0")]
    [InlineData("MagicLink:Clients:0:Scopes:0", "profile", "MagicLink__Clients__0__Scopes__0")]
    [InlineData("MagicLink:Clients:1:ClientId", TestSetup.PublicClient, "MagicLink__Clients__1__ClientId")]
    public void A_wrong_setting_stops_the_host_and_is_named(string key, string value, string named)
    {
        var settings = _setup.Settings(UnusedStore, smtpPort: 25);
        settings[key] = value;

        Assert.Contains(named, StartFailure(settings), StringComparison.Ordinal);
    }

    [Fact]
    public void A_client_without_a_redirect_uri_stops_the_host()
    {
        var settings = _setup.Settings(UnusedStore, smtpPort: 25);
        settings.Remove("MagicLink:Clients:1:RedirectUris:0");

        Assert.Contains("MagicLink__Clients__1__RedirectUris", StartFailure(settings), StringComparison.Ordinal);
    }

    [Fact]
    public void A_certificate_without_its_private_key_stops_the_host()
    {
        var settings = _setup.Settings(UnusedStore, smtpPort: 25);
        var publicOnly = Path.Combine(_setup.DirectoryPath, "public-only.pfx");
        TestCertificates.WritePublicOnly(publicOnly);
        settings["MagicLink:SigningCertificate:Path"] = publicOnly;

        var error = StartFailure(settings);

        Assert.Contains("MagicLink__SigningCertificate__Path", error, StringComparison.Ordinal);
        Assert.Contains("private key", error, StringComparison.Ordinal);
    }

    private static string StartFailure(Dictionary<string, string?> settings)
    {
        using var factory = new HostFactory(settings);
        var error = Assert.Throws<InvalidOperationException>(() => factory.Services);
        return error.Message;
    }
}
