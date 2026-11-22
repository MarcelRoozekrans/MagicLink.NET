namespace MagicLink.Host;

/// <summary>A certificate with its private key in a PKCS#12 file, such as a mounted secret.</summary>
internal sealed class CertificateSettings
{
    /// <summary>The path of the <c>.pfx</c> file.</summary>
    public string? Path { get; set; }

    /// <summary>The password of the file, if it has one.</summary>
    public string? Password { get; set; }
}
