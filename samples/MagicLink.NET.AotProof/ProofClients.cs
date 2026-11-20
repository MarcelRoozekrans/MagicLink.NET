namespace MagicLink.AotProof;

/// <summary>The fixed clients and test subject of the Native AOT proof. Proof-only values.</summary>
public static class ProofClients
{
    /// <summary>A confidential client that uses the client credentials grant.</summary>
    public const string ConfidentialClientId = "proof-service";

    /// <summary>The secret of <see cref="ConfidentialClientId"/>.</summary>
    public const string ConfidentialClientSecret = "proof-secret";

    /// <summary>A public client that uses the authorization code flow with PKCE and refresh tokens.</summary>
    public const string PublicClientId = "proof-cli";

    /// <summary>The redirect URI registered for <see cref="PublicClientId"/>.</summary>
    public const string PublicRedirectUri = "http://localhost/callback";

    /// <summary>A public client that uses the device authorization flow. Registered on PostgreSQL only.</summary>
    public const string DeviceClientId = "proof-device";

    /// <summary>The setting, or environment variable, with the PostgreSQL connection string the proof runs on.</summary>
    public const string PostgresSetting = "MAGICLINK_AOT_PROOF_POSTGRES";

    /// <summary>The subject the authorization endpoint signs in, since the proof has no sign-in page.</summary>
    public const string TestSubject = "proof-user";
}
