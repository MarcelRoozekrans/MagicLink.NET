using System.Data.Async;
using System.Runtime.CompilerServices;
using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>The SQL behind the token store.</summary>
[Dialect(SqlDialect.PostgreSql)]
internal sealed partial class TokenRepository(IAsyncDbConnection connection)
{
    private const string Columns =
        "id, concurrency_token, application_id, authorization_id, creation_date, expiration_date, redemption_date, payload, " +
        "properties, reference_id, status, subject, type";

    // Optional filters, as OpenIddict's FindAsync and RevokeAsync define them: a null argument filters nothing.
    private const string Filters =
        "(@subject::text IS NULL OR subject = @subject) AND (@client::uuid IS NULL OR application_id = @client) " +
        "AND (@status::text IS NULL OR status = @status) AND (@type::text IS NULL OR type = @type)";

    private const string Revoke = "UPDATE magiclink.tokens SET status = 'revoked', concurrency_token = replace(gen_random_uuid()::text, '-', '') WHERE ";

    [Command("INSERT INTO magiclink.tokens (" + Columns + ") VALUES (@row_Id, @row_ConcurrencyToken, @row_ApplicationId, @row_AuthorizationId, " +
        "@row_CreationDate, @row_ExpirationDate, @row_RedemptionDate, @row_Payload, @row_Properties, @row_ReferenceId, @row_Status, " +
        "@row_Subject, @row_Type)")]
    public partial ValueTask<int> InsertAsync(TokenRow row, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.tokens SET concurrency_token = @row_ConcurrencyToken, application_id = @row_ApplicationId, " +
        "authorization_id = @row_AuthorizationId, creation_date = @row_CreationDate, expiration_date = @row_ExpirationDate, " +
        "redemption_date = @row_RedemptionDate, payload = @row_Payload, properties = @row_Properties, reference_id = @row_ReferenceId, " +
        "status = @row_Status, subject = @row_Subject, type = @row_Type WHERE id = @row_Id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> UpdateAsync(TokenRow row, string expectedToken, CancellationToken cancellationToken);

    [Command("DELETE FROM magiclink.tokens WHERE id = @id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> DeleteAsync(Guid id, string expectedToken, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens WHERE id = @id")]
    public partial ValueTask<TokenRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens WHERE reference_id = @referenceId")]
    public partial ValueTask<TokenRow?> FindByReferenceIdAsync(string referenceId, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens WHERE " + Filters + " ORDER BY id")]
    public partial IAsyncEnumerable<TokenRow> FindAsync(string? subject, Guid? client, string? status, string? type, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens WHERE application_id = @applicationId ORDER BY id")]
    public partial IAsyncEnumerable<TokenRow> FindByApplicationIdAsync(Guid applicationId, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens WHERE authorization_id = @authorizationId ORDER BY id")]
    public partial IAsyncEnumerable<TokenRow> FindByAuthorizationIdAsync(Guid authorizationId, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens WHERE subject = @subject ORDER BY id")]
    public partial IAsyncEnumerable<TokenRow> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT count(*) FROM magiclink.tokens")]
    public partial ValueTask<long> CountAsync(CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.tokens ORDER BY id LIMIT @count OFFSET @offset")]
    public partial IAsyncEnumerable<TokenRow> ListAsync(int? count, int offset, [EnumeratorCancellation] CancellationToken cancellationToken);

    // OpenIddict 7.9.0's EF Core rule: created before the threshold, and either not inactive or valid, or under an
    // authorization that is not valid, or expired. IS DISTINCT FROM keeps C#'s null semantics.
    private const string Prunable =
        "creation_date < @threshold AND ((status IS DISTINCT FROM 'inactive' AND status IS DISTINCT FROM 'valid') " +
        "OR EXISTS (SELECT 1 FROM magiclink.authorizations a WHERE a.id = magiclink.tokens.authorization_id AND a.status IS DISTINCT FROM 'valid') " +
        "OR expiration_date < now())";

    // The subquery picks a batch; the outer WHERE repeats the rule, because PostgreSQL re-checks only the outer WHERE
    // on a row another transaction updated meanwhile, and that update may have made the row worth keeping.
    // LIMIT matches StoreFilter.PruneBatch.
    [Command("DELETE FROM magiclink.tokens WHERE id IN (SELECT id FROM magiclink.tokens WHERE " + Prunable +
        " ORDER BY id LIMIT 1000) AND " + Prunable)]
    public partial ValueTask<int> PruneBatchAsync(DateTimeOffset threshold, CancellationToken cancellationToken);

    [Command(Revoke + Filters)]
    public partial ValueTask<int> RevokeAsync(string? subject, Guid? client, string? status, string? type, CancellationToken cancellationToken);

    [Command(Revoke + "application_id = @applicationId AND status IS DISTINCT FROM 'revoked'")]
    public partial ValueTask<int> RevokeByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken);

    [Command(Revoke + "authorization_id = @authorizationId AND status IS DISTINCT FROM 'revoked'")]
    public partial ValueTask<int> RevokeByAuthorizationIdAsync(Guid authorizationId, CancellationToken cancellationToken);

    [Command(Revoke + "subject = @subject AND status IS DISTINCT FROM 'revoked'")]
    public partial ValueTask<int> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken);
}
