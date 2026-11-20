using System.Data.Async;
using System.Runtime.CompilerServices;
using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>The SQL behind the authorization store.</summary>
[Dialect(SqlDialect.PostgreSql)]
internal sealed partial class AuthorizationRepository(IAsyncDbConnection connection)
{
    private const string Columns = "id, concurrency_token, application_id, creation_date, properties, scopes, status, subject, type";

    // Optional filters, as OpenIddict's FindAsync and RevokeAsync define them: a null argument filters nothing.
    private const string Filters =
        "(@subject::text IS NULL OR subject = @subject) AND (@client::uuid IS NULL OR application_id = @client) " +
        "AND (@status::text IS NULL OR status = @status) AND (@type::text IS NULL OR type = @type)";

    private const string NewToken = "replace(gen_random_uuid()::text, '-', '')";

    [Command("INSERT INTO magiclink.authorizations (" + Columns + ") VALUES (@row_Id, @row_ConcurrencyToken, @row_ApplicationId, " +
        "@row_CreationDate, @row_Properties, @row_Scopes, @row_Status, @row_Subject, @row_Type)")]
    public partial ValueTask<int> InsertAsync(AuthorizationRow row, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.authorizations SET concurrency_token = @row_ConcurrencyToken, application_id = @row_ApplicationId, " +
        "creation_date = @row_CreationDate, properties = @row_Properties, scopes = @row_Scopes, status = @row_Status, " +
        "subject = @row_Subject, type = @row_Type WHERE id = @row_Id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> UpdateAsync(AuthorizationRow row, string expectedToken, CancellationToken cancellationToken);

    // Its tokens go with it, by cascade.
    [Command("DELETE FROM magiclink.authorizations WHERE id = @id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> DeleteAsync(Guid id, string expectedToken, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.authorizations WHERE id = @id")]
    public partial ValueTask<AuthorizationRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    // A null scopes argument filters nothing; otherwise the authorization must hold every scope asked for.
    [Query("SELECT " + Columns + " FROM magiclink.authorizations WHERE " + Filters +
        " AND (@scopes::text[] IS NULL OR scopes @> @scopes) ORDER BY id")]
    public partial IAsyncEnumerable<AuthorizationRow> FindAsync(
        string? subject, Guid? client, string? status, string? type, string[]? scopes, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.authorizations WHERE application_id = @applicationId ORDER BY id")]
    public partial IAsyncEnumerable<AuthorizationRow> FindByApplicationIdAsync(Guid applicationId, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.authorizations WHERE subject = @subject ORDER BY id")]
    public partial IAsyncEnumerable<AuthorizationRow> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT count(*) FROM magiclink.authorizations")]
    public partial ValueTask<long> CountAsync(CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.authorizations ORDER BY id LIMIT @count OFFSET @offset")]
    public partial IAsyncEnumerable<AuthorizationRow> ListAsync(int? count, int offset, [EnumeratorCancellation] CancellationToken cancellationToken);

    // OpenIddict 7.9.0's EF Core rule: created before the threshold, not valid or ad-hoc, and no token left.
    // IS DISTINCT FROM keeps C#'s null semantics: a null status is not valid.
    [Command("DELETE FROM magiclink.authorizations WHERE id IN (SELECT a.id FROM magiclink.authorizations a " +
        "WHERE a.creation_date < @threshold AND (a.status IS DISTINCT FROM 'valid' OR a.type = 'ad-hoc') " +
        "AND NOT EXISTS (SELECT 1 FROM magiclink.tokens t WHERE t.authorization_id = a.id) ORDER BY a.id LIMIT 1000)")] // LIMIT matches StoreFilter.PruneBatch.
    public partial ValueTask<int> PruneBatchAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.authorizations SET status = 'revoked', concurrency_token = " + NewToken + " WHERE " + Filters)]
    public partial ValueTask<int> RevokeAsync(string? subject, Guid? client, string? status, string? type, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.authorizations SET status = 'revoked', concurrency_token = " + NewToken +
        " WHERE application_id = @applicationId AND status IS DISTINCT FROM 'revoked'")]
    public partial ValueTask<int> RevokeByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.authorizations SET status = 'revoked', concurrency_token = " + NewToken +
        " WHERE subject = @subject AND status IS DISTINCT FROM 'revoked'")]
    public partial ValueTask<int> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken);
}
