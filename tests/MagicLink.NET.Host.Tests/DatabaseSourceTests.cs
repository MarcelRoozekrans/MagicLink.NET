using MagicLink.PostgreSql.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace MagicLink.Host.Tests;

// The container reads who may sign in from an existing PostgreSQL database instead of a file when its settings name one
// (issue #8). SQL Server needs Microsoft.Data.SqlClient, which does not run under Native AOT, so the container offers
// PostgreSQL only; an app that hosts MagicLink.NET can still use SQL Server.
public sealed class DatabaseSourceTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncDisposable
{
    private readonly TestSetup _setup = new();
    private HostFactory? _host;

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        _setup.Dispose();
    }

    [Fact]
    public async Task A_person_in_the_database_may_sign_in_with_their_groups()
    {
        await database.ScalarAsync<int>("""
            CREATE TABLE people (email text NOT NULL, group_name text NULL);
            INSERT INTO people VALUES ('db-person@example.com', 'db-users'), ('db-person@example.com', 'admins');
            SELECT 1;
            """);
        var settings = _setup.Settings(database.ConnectionString, smtpPort: 25);
        settings.Remove("MagicLink:SignInFile:Path");
        settings["MagicLink:SignInDatabase:ConnectionString"] = database.ConnectionString;
        settings["MagicLink:SignInDatabase:Query"] = "SELECT group_name FROM people WHERE lower(email) = @email";
        _host = new HostFactory(settings);

        var source = _host.Services.GetRequiredService<ISignInSource>();

        var person = await source.LookUpAsync("db-person@example.com", TestContext.Current.CancellationToken);
        Assert.True(person.MaySignIn);
        Assert.Equal(["admins", "db-users"], person.Groups.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.False((await source.LookUpAsync(TestSetup.Person, TestContext.Current.CancellationToken)).MaySignIn);
    }
}
