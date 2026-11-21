using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>
/// One sign-in request: an email address, the page to return to, and the link and code that confirm it. It lives as a
/// token of its own type in OpenIddict's token store, so every store works, the store's concurrency check makes link
/// and code single-use and counts code attempts exactly, and expired requests are pruned with the other tokens.
/// </summary>
/// <remarks>
/// The store keeps no secret in a usable form. The link token is the reference id, which OpenIddict stores as a SHA-256
/// hash. The request is bound to the browser that started it by a secret in a cookie; the store keeps its hash, and
/// the code's HMAC keyed with that secret, so a copy of the database alone does not reveal or allow guessing the code.
/// </remarks>
internal sealed class SignInRequest
{
    /// <summary>The token type sign-in requests are stored under.</summary>
    public const string TokenType = "magiclink_sign_in";

    /// <summary>The cookie that binds a sign-in request to the browser that started it.</summary>
    public const string BindingCookie = "__Host-MagicLink.SignIn";

    // Letters and digits that are hard to mistake for each other: no O, I, L, S, Z, U or V, and no 0, 1 or 2.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRTWXY3456789";
    private const int CodeLength = 8;

    private readonly object _token;
    private SignInData _data;

    private SignInRequest(object token, string id, string email, DateTimeOffset? expiresAt, string? status, SignInData data)
    {
        _token = token;
        Id = id;
        Email = email;
        ExpiresAt = expiresAt;
        Status = status;
        _data = data;
    }

    /// <summary>The token id, which the binding cookie names.</summary>
    public string Id { get; }

    /// <summary>The normalized email address the request is for.</summary>
    public string Email { get; }

    /// <summary>The path on this site to return to once signed in.</summary>
    public string ReturnUrl => _data.ReturnUrl;

    private DateTimeOffset? ExpiresAt { get; }

    private string? Status { get; }

    /// <summary>
    /// Starts a sign-in request for <paramref name="email"/>. Returns the request, the value of the binding cookie,
    /// and the link token and code for the email.
    /// </summary>
    public static async ValueTask<(SignInRequest Request, string Cookie, string LinkToken, string Code)> CreateAsync(
        IOpenIddictTokenManager tokens, string email, string returnUrl, DateTimeOffset now, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var binding = NewSecret();
        var linkToken = NewSecret();
        var code = NewCode();
        var data = new SignInData(returnUrl, Hash(binding), CodeHash(binding, code), Attempts: 0);

        var token = await tokens.CreateAsync(
            new OpenIddictTokenDescriptor
            {
                Type = TokenType,
                Status = Statuses.Valid,
                Subject = email,
                CreationDate = now,
                ExpirationDate = now + lifetime,
                ReferenceId = linkToken,
                Payload = data.Write(),
            },
            cancellationToken).ConfigureAwait(false);

        var id = await tokens.GetIdAsync(token, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The token store returned no id for a new sign-in request.");
        var request = new SignInRequest(token, id, email, now + lifetime, Statuses.Valid, data);
        return (request, id + "." + binding, linkToken, code);
    }

    /// <summary>The request this browser's binding cookie names, or null when there is none or the cookie is wrong.</summary>
    public static async ValueTask<SignInRequest?> FindByCookieAsync(IOpenIddictTokenManager tokens, string? cookie, CancellationToken cancellationToken)
    {
        if (!TrySplitCookie(cookie, out var id, out var binding))
        {
            return null;
        }

        var token = await tokens.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
        var request = token is null ? null : await ReadAsync(tokens, token, cancellationToken).ConfigureAwait(false);
        return request is not null && request.IsBoundTo(binding) ? request : null;
    }

    /// <summary>The request <paramref name="linkToken"/> belongs to, in whatever browser the link was opened.</summary>
    public static async ValueTask<SignInRequest?> FindByLinkAsync(IOpenIddictTokenManager tokens, string? linkToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(linkToken))
        {
            return null;
        }

        var token = await tokens.FindByReferenceIdAsync(linkToken, cancellationToken).ConfigureAwait(false);
        return token is null ? null : await ReadAsync(tokens, token, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>True when <paramref name="cookie"/>, the binding cookie of a browser, belongs to this request.</summary>
    public bool BelongsTo(string? cookie) =>
        TrySplitCookie(cookie, out var id, out var binding) && string.Equals(id, Id, StringComparison.Ordinal) && IsBoundTo(binding);

    /// <summary>True while link and code can still be used: not used, not expired, attempts left.</summary>
    public bool IsUsable(DateTimeOffset now, int maxAttempts) =>
        string.Equals(Status, Statuses.Valid, StringComparison.Ordinal) && ExpiresAt > now && _data.Attempts < maxAttempts;

    /// <summary>How many more codes may be tried.</summary>
    public int AttemptsLeft(int maxAttempts) => Math.Max(0, maxAttempts - _data.Attempts);

    /// <summary>
    /// Tries <paramref name="code"/>, typed in the browser holding <paramref name="cookie"/>. The attempt is counted in
    /// the store before the code is compared, so parallel guesses cannot exceed the limit.
    /// </summary>
    public async ValueTask<CodeOutcome> TryCodeAsync(
        IOpenIddictTokenManager tokens, string cookie, string code, DateTimeOffset now, int maxAttempts, CancellationToken cancellationToken)
    {
        if (!IsUsable(now, maxAttempts) || !TrySplitCookie(cookie, out _, out var binding))
        {
            return CodeOutcome.NoLongerValid;
        }

        var counted = _data with { Attempts = _data.Attempts + 1 };
        var descriptor = new OpenIddictTokenDescriptor();
        await tokens.PopulateAsync(descriptor, _token, cancellationToken).ConfigureAwait(false);
        descriptor.Payload = counted.Write();
        try
        {
            await tokens.UpdateAsync(_token, descriptor, cancellationToken).ConfigureAwait(false);
        }
        catch (OpenIddictExceptions.ConcurrencyException)
        {
            // Another attempt or the link changed the request meanwhile; this code was not compared.
            return CodeOutcome.TryAgain;
        }

        _data = counted;
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(_data.CodeHash), Convert.FromBase64String(CodeHash(binding, code))))
        {
            if (_data.Attempts < maxAttempts)
            {
                return CodeOutcome.Wrong;
            }

            await tokens.TryRejectAsync(_token, cancellationToken).ConfigureAwait(false);
            return CodeOutcome.NoLongerValid;
        }

        return await tokens.TryRedeemAsync(_token, cancellationToken).ConfigureAwait(false) ? CodeOutcome.SignedIn : CodeOutcome.NoLongerValid;
    }

    /// <summary>Uses up the request for a link confirmed in the browser that started it. False when it was used first.</summary>
    public ValueTask<bool> TryRedeemAsync(IOpenIddictTokenManager tokens, CancellationToken cancellationToken) =>
        tokens.TryRedeemAsync(_token, cancellationToken);

    /// <summary>A code as the person may type it: lower case, with spaces or a dash between the groups.</summary>
    internal static string NormalizeCode(string code)
    {
        var builder = new StringBuilder(CodeLength);
        foreach (var character in code)
        {
            if (!char.IsWhiteSpace(character) && character != '-')
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>A code of <see cref="CodeLength"/> characters, shown in two groups: ABCD-EFGH.</summary>
    internal static string NewCode()
    {
        Span<char> code = stackalloc char[CodeLength + 1];
        for (var i = 0; i < CodeLength; i++)
        {
            code[i < CodeLength / 2 ? i : i + 1] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        code[CodeLength / 2] = '-';
        return new string(code);
    }

    private static async ValueTask<SignInRequest?> ReadAsync(IOpenIddictTokenManager tokens, object token, CancellationToken cancellationToken)
    {
        if (!await tokens.HasTypeAsync(token, TokenType, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var id = await tokens.GetIdAsync(token, cancellationToken).ConfigureAwait(false);
        var email = await tokens.GetSubjectAsync(token, cancellationToken).ConfigureAwait(false);
        var payload = await tokens.GetPayloadAsync(token, cancellationToken).ConfigureAwait(false);
        if (id is null || email is null || !SignInData.TryRead(payload, out var data))
        {
            return null;
        }

        var expiresAt = await tokens.GetExpirationDateAsync(token, cancellationToken).ConfigureAwait(false);
        var status = await tokens.GetStatusAsync(token, cancellationToken).ConfigureAwait(false);
        return new SignInRequest(token, id, email, expiresAt, status, data);
    }

    private bool IsBoundTo(string binding) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(_data.BindingHash), Convert.FromBase64String(Hash(binding)));

    private static bool TrySplitCookie(string? cookie, out string id, out string binding)
    {
        var dot = cookie?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        if (cookie is null || dot <= 0 || dot == cookie.Length - 1)
        {
            id = binding = string.Empty;
            return false;
        }

        id = cookie[..dot];
        binding = cookie[(dot + 1)..];
        return true;
    }

    private static string NewSecret() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string secret) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string CodeHash(string binding, string code) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(binding), Encoding.UTF8.GetBytes(NormalizeCode(code))));

    /// <summary>What a sign-in request keeps in its token's payload.</summary>
    private readonly record struct SignInData(string ReturnUrl, string BindingHash, string CodeHash, int Attempts)
    {
        public static bool TryRead(string? payload, out SignInData data)
        {
            data = default;
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                data = new SignInData(
                    root.GetProperty("return_url").GetString()!,
                    root.GetProperty("binding_hash").GetString()!,
                    root.GetProperty("code_hash").GetString()!,
                    root.GetProperty("attempts").GetInt32());
                return data.ReturnUrl is not null && data.BindingHash is not null && data.CodeHash is not null;
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                return false;
            }
        }

        public string Write()
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("return_url", ReturnUrl);
                writer.WriteString("binding_hash", BindingHash);
                writer.WriteString("code_hash", CodeHash);
                writer.WriteNumber("attempts", Attempts);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
    }
}
