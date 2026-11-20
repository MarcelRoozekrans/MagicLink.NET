using System.Data.Async;
using System.Runtime.CompilerServices;
using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>The SQL behind <see cref="MagicLinkApplicationStore"/>.</summary>
[Dialect(SqlDialect.PostgreSql)]
internal sealed partial class ApplicationRepository(IAsyncDbConnection connection)
{
    private const string Columns =
        "id, concurrency_token, application_type, client_id, client_secret, client_type, consent_type, display_name, display_names, " +
        "json_web_key_set, permissions, post_logout_redirect_uris, redirect_uris, requirements, settings, properties";

    [Command("INSERT INTO magiclink.applications (" + Columns + ") VALUES (@row_Id, @row_ConcurrencyToken, @row_ApplicationType, " +
        "@row_ClientId, @row_ClientSecret, @row_ClientType, @row_ConsentType, @row_DisplayName, @row_DisplayNames, @row_JsonWebKeySet, " +
        "@row_Permissions, @row_PostLogoutRedirectUris, @row_RedirectUris, @row_Requirements, @row_Settings, @row_Properties)")]
    public partial ValueTask<int> InsertAsync(ApplicationRow row, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.applications SET concurrency_token = @row_ConcurrencyToken, application_type = @row_ApplicationType, " +
        "client_id = @row_ClientId, client_secret = @row_ClientSecret, client_type = @row_ClientType, consent_type = @row_ConsentType, " +
        "display_name = @row_DisplayName, display_names = @row_DisplayNames, json_web_key_set = @row_JsonWebKeySet, " +
        "permissions = @row_Permissions, post_logout_redirect_uris = @row_PostLogoutRedirectUris, redirect_uris = @row_RedirectUris, " +
        "requirements = @row_Requirements, settings = @row_Settings, properties = @row_Properties " +
        "WHERE id = @row_Id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> UpdateAsync(ApplicationRow row, string expectedToken, CancellationToken cancellationToken);

    [Command("DELETE FROM magiclink.applications WHERE id = @id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> DeleteAsync(Guid id, string expectedToken, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.applications WHERE id = @id")]
    public partial ValueTask<ApplicationRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.applications WHERE client_id = @clientId")]
    public partial ValueTask<ApplicationRow?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken);

    // @> uses the GIN index; the comparison of text array elements is exact and case-sensitive.
    [Query("SELECT " + Columns + " FROM magiclink.applications WHERE redirect_uris @> @uris ORDER BY id")]
    public partial IAsyncEnumerable<ApplicationRow> FindByRedirectUriAsync(string[] uris, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.applications WHERE post_logout_redirect_uris @> @uris ORDER BY id")]
    public partial IAsyncEnumerable<ApplicationRow> FindByPostLogoutRedirectUriAsync(string[] uris, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT count(*) FROM magiclink.applications")]
    public partial ValueTask<long> CountAsync(CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.applications ORDER BY id LIMIT @count OFFSET @offset")]
    public partial IAsyncEnumerable<ApplicationRow> ListAsync(int? count, int offset, [EnumeratorCancellation] CancellationToken cancellationToken);
}
