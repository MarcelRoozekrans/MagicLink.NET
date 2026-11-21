using MagicLink.Tests.Sources;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MagicLink.Tests.Commands;

// docs/specs/magiclink-net-decide-who-may-sign-in-and-with-which-groups.md, criterion 5: a test command takes an email
// address and shows whether it may sign in and with which groups, for the database and the file, without an email (issue #9).
public sealed class CheckSignInCommandTests(PostgreSqlUserDatabase database) : IClassFixture<PostgreSqlUserDatabase>, IDisposable
{
    private const string People = """
        { "people": [ { "email": "ann@example.com", "groups": ["admins", "developers"] }, { "email": "bob@example.com" } ] }
        """;

    private const string GroupsQuery = """
        SELECT m.group_name
        FROM people p LEFT JOIN memberships m ON m.email = p.email
        WHERE p.email = @email
        ORDER BY m.group_name
        """;

    // Test hosts read no settings files, so they need no file watchers.
    private static readonly string[] HostArgs = ["--hostBuilder:reloadConfigOnChange=false"];

    private readonly string _directory = Directory.CreateTempSubdirectory("magiclink-check-sign-in-").FullName;
    private readonly CountingSender _sender = new();

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private string FilePath => Path.Combine(_directory, "people.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Arguments_that_are_not_the_command_are_left_to_the_host()
    {
        await using var app = await FileHostAsync();

        Assert.Null(await RunAsync(app, []));
        Assert.Null(await RunAsync(app, ["--urls", "http://localhost:5000"]));
    }

    [Fact]
    public async Task A_listed_address_in_the_file_may_sign_in_with_its_groups()
    {
        await using var app = await FileHostAsync();

        var (exitCode, output) = await RunAndCaptureAsync(app, ["check-sign-in", " Ann@Example.com "]);

        Assert.Equal(0, exitCode);
        Assert.Contains("ann@example.com may sign in", output, StringComparison.Ordinal);
        Assert.Contains("Groups: admins, developers", output, StringComparison.Ordinal);
        AssertNoEmail(app);
    }

    [Fact]
    public async Task A_listed_address_without_groups_may_sign_in_with_none()
    {
        await using var app = await FileHostAsync();

        var (exitCode, output) = await RunAndCaptureAsync(app, ["check-sign-in", "bob@example.com"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("bob@example.com may sign in", output, StringComparison.Ordinal);
        Assert.Contains("Groups: none", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unlisted_address_may_not_sign_in_and_gets_no_email()
    {
        await using var app = await FileHostAsync();

        var (exitCode, output) = await RunAndCaptureAsync(app, ["check-sign-in", "carol@example.com"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("carol@example.com may not sign in", output, StringComparison.Ordinal);
        AssertNoEmail(app);
    }

    [Fact]
    public async Task The_database_says_who_may_sign_in_with_which_groups()
    {
        await using var app = Host(services => services.AddMagicLinkSignInDatabase(database.Factory, options =>
        {
            options.ConnectionString = database.ReaderConnectionString;
            options.Query = GroupsQuery;
        }));

        var (annExit, annOutput) = await RunAndCaptureAsync(app, ["check-sign-in", "ann@example.com"]);
        var (carolExit, carolOutput) = await RunAndCaptureAsync(app, ["check-sign-in", "carol@example.com"]);

        Assert.Equal(0, annExit);
        Assert.Contains("Groups: admins, developers", annOutput, StringComparison.Ordinal);
        Assert.Equal(1, carolExit);
        Assert.Contains("carol@example.com may not sign in", carolOutput, StringComparison.Ordinal);
        AssertNoEmail(app);
    }

    [Fact]
    public async Task A_source_that_cannot_answer_is_reported_with_the_reason()
    {
        await using var app = Host(services => services.AddMagicLinkSignInFile(options => options.Path = FilePath));

        var (exitCode, output) = await RunAndCaptureAsync(app, ["check-sign-in", "ann@example.com"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("cannot tell", output, StringComparison.Ordinal);
        Assert.Contains(FilePath, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_query_that_the_start_check_refuses_is_reported_with_the_reason()
    {
        // This query ignores the address, so a look-up alone would say ann may sign in; the start check refuses it.
        await using var app = Host(services => services.AddMagicLinkSignInDatabase(database.Factory, options =>
        {
            options.ConnectionString = database.ReaderConnectionString;
            options.Query = "SELECT group_name FROM memberships";
        }));

        var (exitCode, output) = await RunAndCaptureAsync(app, ["check-sign-in", "ann@example.com"]);

        Assert.Equal(2, exitCode);
        Assert.Contains("lets the made-up address", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("not an address")]
    [InlineData("ann@example.com", "bob@example.com")]
    public async Task Anything_but_one_email_address_shows_how_to_use_the_command(params string[] arguments)
    {
        await using var app = await FileHostAsync();

        var (exitCode, output) = await RunAndCaptureAsync(app, ["check-sign-in", .. arguments]);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage: check-sign-in <email address>", output, StringComparison.Ordinal);
    }

    private static async Task<(int? ExitCode, string Output)> RunAndCaptureAsync(WebApplication app, string[] arguments)
    {
        await using var output = new StringWriter();
        var exitCode = await CheckSignInCommand.TryRunAsync(app.Services, arguments, output, Cancel);
        return (exitCode, output.ToString());
    }

    private static async Task<int?> RunAsync(WebApplication app, string[] arguments) => (await RunAndCaptureAsync(app, arguments)).ExitCode;

    private async Task<WebApplication> FileHostAsync()
    {
        await File.WriteAllTextAsync(FilePath, People, Cancel);
        return Host(services => services.AddMagicLinkSignInFile(options => options.Path = FilePath));
    }

    /// <summary>MagicLink.NET with its sign-in email, built but not started, as a command runs it.</summary>
    private WebApplication Host(Action<IServiceCollection> source)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
        source(builder.Services);
        builder.Services.AddSingleton<ISignInMessageSender>(_sender);
        builder.Services.AddOpenIddict()
            .AddCore(options => options.UsePostgreSql(store => store.ConnectionString = "Host=localhost;Database=unused"))
            .AddMagicLinkServer(server => server.AddEphemeralEncryptionKey().AddEphemeralSigningKey().SetIssuer(new Uri("https://magiclink.test/")));
        return builder.Build();
    }

    private void AssertNoEmail(WebApplication app)
    {
        Assert.Equal(0, app.Services.GetRequiredService<SignInMailQueue>().Queued);
        Assert.Equal(0, _sender.Sent);
    }

    private sealed class CountingSender : ISignInMessageSender
    {
        private int _sent;

        public int Sent => Volatile.Read(ref _sent);

        public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sent);
            return ValueTask.CompletedTask;
        }
    }
}
