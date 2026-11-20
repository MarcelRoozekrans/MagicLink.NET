namespace MagicLink.PostgreSql.Tests;

/// <summary>Creates related rows through the stores, for the authorization and token tests.</summary>
internal sealed class StoreData(StoreFixture fixture)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public MagicLinkApplicationStore Applications { get; } = new(fixture.Store);

    public MagicLinkAuthorizationStore Authorizations { get; } = new(fixture.Store);

    public MagicLinkTokenStore Tokens { get; } = new(fixture.Store);

    public static string NewSubject() => "subject-" + Guid.NewGuid().ToString("N");

    public async Task<MagicLinkApplication> ApplicationAsync()
    {
        var application = new MagicLinkApplication { ClientId = "client-" + Guid.NewGuid().ToString("N") };
        await Applications.CreateAsync(application, Cancel);
        return application;
    }

    public async Task<MagicLinkAuthorization> AuthorizationAsync(
        MagicLinkApplication? application = null, string? subject = null, string status = "valid", string type = "permanent",
        DateTimeOffset? created = null, string[]? scopes = null)
    {
        var authorization = new MagicLinkAuthorization
        {
            ApplicationId = application?.Id,
            Subject = subject ?? NewSubject(),
            Status = status,
            Type = type,
            CreationDate = created ?? DateTimeOffset.UtcNow,
            Scopes = [.. scopes ?? ["openid"]],
        };
        await Authorizations.CreateAsync(authorization, Cancel);
        return authorization;
    }

    public async Task<MagicLinkToken> TokenAsync(
        MagicLinkApplication? application = null, MagicLinkAuthorization? authorization = null, string? subject = null,
        string status = "valid", string type = "refresh_token", DateTimeOffset? created = null, DateTimeOffset? expires = null)
    {
        var token = new MagicLinkToken
        {
            ApplicationId = application?.Id,
            AuthorizationId = authorization?.Id,
            Subject = subject ?? NewSubject(),
            Status = status,
            Type = type,
            CreationDate = created ?? DateTimeOffset.UtcNow,
            ExpirationDate = expires,
        };
        await Tokens.CreateAsync(token, Cancel);
        return token;
    }
}
