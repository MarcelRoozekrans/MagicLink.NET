namespace MagicLink.AotProof;

/// <summary>The fixed client, person and settings of the Native AOT proof. Proof-only values.</summary>
public static class ProofClients
{
    /// <summary>A public client that uses the authorization code flow with PKCE, the device flow, and refresh tokens.</summary>
    public const string PublicClientId = "proof-cli";

    /// <summary>The redirect URI registered for <see cref="PublicClientId"/>.</summary>
    public const string PublicRedirectUri = "http://localhost/callback";

    /// <summary>The setting, or environment variable, with the PostgreSQL connection string the proof runs on.</summary>
    public const string PostgresSetting = "MAGICLINK_AOT_PROOF_POSTGRES";

    /// <summary>The setting, or environment variable, with the issuer; <c>http://localhost/</c> without it.</summary>
    public const string IssuerSetting = "MAGICLINK_AOT_PROOF_ISSUER";

    /// <summary>The only person who may sign in. MagicLink.NET uses the address as the subject of its tokens.</summary>
    public const string TestPerson = "proof-user@example.com";

    /// <summary>The group of <see cref="TestPerson"/>.</summary>
    public const string TestGroup = "proof";

    /// <summary>The claim MagicLink.NET puts a person's groups in.</summary>
    public const string GroupsClaim = "groups";
}
