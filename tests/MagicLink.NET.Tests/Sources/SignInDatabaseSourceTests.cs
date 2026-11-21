using Microsoft.Extensions.Options;

namespace MagicLink.Tests.Sources;

// docs/specs/magiclink-net-decide-who-may-sign-in-and-with-which-groups.md: a read-only query on an existing PostgreSQL or
// SQL Server database says who may sign in and with which groups, and is checked at startup (issue #8).
public abstract class SignInDatabaseSourceTests<TDatabase>(TDatabase database) : IClassFixture<TDatabase>
    where TDatabase : UserDatabase
{
    /// <summary>A row per group; a person without groups has one row with no group.</summary>
    private const string GroupsQuery = """
        SELECT m.group_name
        FROM people p LEFT JOIN memberships m ON m.email = p.email
        WHERE p.email = @email
        """;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_known_address_may_sign_in_with_its_groups_once_each()
    {
        await using var source = Source(GroupsQuery);

        var access = await source.LookUpAsync("ann@example.com", Cancel);

        Assert.True(access.MaySignIn);
        Assert.Equal(["admins", "developers"], access.Groups.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_known_address_without_groups_may_sign_in_with_none()
    {
        await using var source = Source(GroupsQuery);

        var access = await source.LookUpAsync("bob@example.com", Cancel);

        Assert.True(access.MaySignIn);
        Assert.Empty(access.Groups);
    }

    [Fact]
    public async Task An_address_without_a_row_may_not_sign_in()
    {
        // Disposed the way a container that is not disposed asynchronously would.
        using var source = Source(GroupsQuery);

        var access = await source.LookUpAsync("carol@example.com", Cancel);

        Assert.False(access.MaySignIn);
    }

    [Fact]
    public async Task A_write_the_query_makes_is_rolled_back()
    {
        // Run as the owner, who may write. The account that may only read is what keeps the database unchanged; the
        // rollback cleans up after a query that writes all the same.
        await using var source = Source(database.WritingQuery, database.AdminConnectionString);

        var access = await source.LookUpAsync("ann@example.com", Cancel);

        Assert.True(access.MaySignIn);
        Assert.Equal(0, Convert.ToInt32(await database.ScalarAsync("SELECT COUNT(*) FROM notes"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_group_with_an_empty_name_keeps_that_person_out()
    {
        await using var source = Source("SELECT ' ' FROM people WHERE email = @email");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.LookUpAsync("ann@example.com", Cancel));
    }

    [Fact]
    public async Task A_query_that_fails_after_its_rows_lets_nobody_in()
    {
        await using var source = Source(database.FailingAfterItsRowsQuery);

        await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(async () => await source.LookUpAsync("ann@example.com", Cancel));
    }

    [Fact]
    public async Task A_query_that_returns_more_than_one_result_lets_nobody_in()
    {
        await using var source = Source("SELECT group_name FROM memberships WHERE email = @email; SELECT group_name FROM memberships");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.LookUpAsync("ann@example.com", Cancel));
        Assert.Contains("one result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_start_check_refuses_a_query_that_fails_after_its_rows()
    {
        var result = Check(database.FailingAfterItsRowsQuery);

        Assert.True(result.Failed);
        Assert.Contains("MagicLinkSignInDatabaseOptions.Query", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void The_start_check_passes_a_query_that_works_for_an_account_that_may_only_read()
    {
        var result = Check(GroupsQuery);

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("SELEC group_name FROM memberships WHERE email = @email", "syntax")]
    [InlineData("SELECT m.no_such_column FROM memberships m WHERE m.email = @email", "no_such_column")]
    [InlineData("SELECT email, group_name FROM memberships WHERE email = @email", "one column")]
    [InlineData("SELECT id FROM people WHERE email = @email", "text")]
    [InlineData("SELECT group_name FROM memberships", "lets the made-up address")]
    [InlineData("SELECT group_name FROM memberships WHERE email = @email; SELECT group_name FROM memberships WHERE email = @email", "one result")]
    public void The_start_check_refuses_a_query_that_fails_and_says_why(string query, string reason)
    {
        var result = Check(query);

        Assert.True(result.Failed);
        Assert.Contains("MagicLinkSignInDatabaseOptions.Query", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains(reason, result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_start_check_refuses_a_query_that_writes_for_an_account_that_may_only_read()
    {
        var result = Check(database.WritingQuery);

        Assert.True(result.Failed);
        Assert.Contains("MagicLinkSignInDatabaseOptions.Query", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void The_start_check_refuses_a_database_it_cannot_sign_in_to()
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = database.ReaderConnectionString };
        builder["Password"] = "wrong";

        var result = SignInDatabaseCheck.Check(database.Factory, Options(GroupsQuery, builder.ConnectionString));

        Assert.True(result.Failed);
        Assert.Contains("cannot run its query", result.FailureMessage, StringComparison.Ordinal);
    }

    private ValidateOptionsResult Check(string query) => SignInDatabaseCheck.Check(database.Factory, Options(query, database.ReaderConnectionString));

    private SignInDatabaseSource Source(string query, string? connectionString = null) =>
        new(Microsoft.Extensions.Options.Options.Create(Options(query, connectionString ?? database.ReaderConnectionString)), new SignInDatabaseProvider(database.Factory));

    private static MagicLinkSignInDatabaseOptions Options(string query, string connectionString) =>
        new() { ConnectionString = connectionString, Query = query };
}
