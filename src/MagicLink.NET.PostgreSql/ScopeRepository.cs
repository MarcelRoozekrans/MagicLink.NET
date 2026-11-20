using System.Data.Async;
using System.Runtime.CompilerServices;
using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>The SQL behind the scope store.</summary>
[Dialect(SqlDialect.PostgreSql)]
internal sealed partial class ScopeRepository(IAsyncDbConnection connection)
{
    private const string Columns = "id, concurrency_token, name, description, descriptions, display_name, display_names, resources, properties";

    [Command("INSERT INTO magiclink.scopes (" + Columns + ") VALUES (@row_Id, @row_ConcurrencyToken, @row_Name, @row_Description, " +
        "@row_Descriptions, @row_DisplayName, @row_DisplayNames, @row_Resources, @row_Properties)")]
    public partial ValueTask<int> InsertAsync(ScopeRow row, CancellationToken cancellationToken);

    [Command("UPDATE magiclink.scopes SET concurrency_token = @row_ConcurrencyToken, name = @row_Name, description = @row_Description, " +
        "descriptions = @row_Descriptions, display_name = @row_DisplayName, display_names = @row_DisplayNames, resources = @row_Resources, " +
        "properties = @row_Properties WHERE id = @row_Id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> UpdateAsync(ScopeRow row, string expectedToken, CancellationToken cancellationToken);

    [Command("DELETE FROM magiclink.scopes WHERE id = @id AND concurrency_token = @expectedToken")]
    public partial ValueTask<int> DeleteAsync(Guid id, string expectedToken, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.scopes WHERE id = @id")]
    public partial ValueTask<ScopeRow?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.scopes WHERE name = @name")]
    public partial ValueTask<ScopeRow?> FindByNameAsync(string name, CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.scopes WHERE name = ANY(@names) ORDER BY id")]
    public partial IAsyncEnumerable<ScopeRow> FindByNamesAsync(string[] names, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.scopes WHERE resources @> @resources ORDER BY id")]
    public partial IAsyncEnumerable<ScopeRow> FindByResourceAsync(string[] resources, [EnumeratorCancellation] CancellationToken cancellationToken);

    [Query("SELECT count(*) FROM magiclink.scopes")]
    public partial ValueTask<long> CountAsync(CancellationToken cancellationToken);

    [Query("SELECT " + Columns + " FROM magiclink.scopes ORDER BY id LIMIT @count OFFSET @offset")]
    public partial IAsyncEnumerable<ScopeRow> ListAsync(int? count, int offset, [EnumeratorCancellation] CancellationToken cancellationToken);
}
