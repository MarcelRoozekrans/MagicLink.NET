using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MagicLink.Host.Tests;

/// <summary>Writes the self-signed signing and encryption certificates the host loads, as PKCS#12 files.</summary>
internal static class TestCertificates
{
    public const string Password = "host-test";

    /// <summary>Writes a certificate for signing tokens to <paramref name="path"/> and returns its SHA-1 thumbprint.</summary>
    public static string WriteSigning(string path) => Write(path, "MagicLink.NET host test signing", X509KeyUsageFlags.DigitalSignature, withPrivateKey: true);

    /// <summary>Writes a certificate for encrypting tokens to <paramref name="path"/> and returns its SHA-1 thumbprint.</summary>
    public static string WriteEncryption(string path) => Write(path, "MagicLink.NET host test encryption", X509KeyUsageFlags.KeyEncipherment, withPrivateKey: true);

    /// <summary>Writes a signing certificate without its private key, which cannot sign anything.</summary>
    public static string WritePublicOnly(string path) => Write(path, "MagicLink.NET host test public only", X509KeyUsageFlags.DigitalSignature, withPrivateKey: false);

    private static string Write(string path, string name, X509KeyUsageFlags usage, bool withPrivateKey)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, critical: true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(2));
        if (withPrivateKey)
        {
            File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, Password));
        }
        else
        {
            using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
            File.WriteAllBytes(path, publicOnly.Export(X509ContentType.Pkcs12, Password));
        }

        return certificate.Thumbprint;
    }
}
