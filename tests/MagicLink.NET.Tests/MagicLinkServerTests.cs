using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MagicLink.Tests;

public sealed class MagicLinkServerTests
{
    // Test hosts read no settings files, so they need no file watchers.
    private static readonly string[] HostArgs = ["--hostBuilder:reloadConfigOnChange=false"];

    [Fact]
    public async Task MagicLink_does_not_start_without_an_issuer()
    {
        await using var app = Build(issuer: null, configure: null);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("issuer", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 5, 5, 15, 20, 15, "CodeLifetime")]
    [InlineData(10, 0, 5, 15, 20, 15, "MaxCodeAttempts")]
    [InlineData(10, 5, 0, 15, 20, 15, "MaxEmailsPerAddress")]
    [InlineData(10, 5, 5, 0, 20, 15, "EmailLimitWindow")]
    [InlineData(10, 5, 5, 15, 0, 15, "MaxSignInsPerClient")]
    [InlineData(10, 5, 5, 15, 20, 0, "ClientLimitWindow")]
    public async Task MagicLink_does_not_start_with_settings_that_lock_everyone_out(
        int lifetimeMinutes, int attempts, int emails, int windowMinutes, int signIns, int clientWindowMinutes, string setting)
    {
        await using var app = Build(new Uri("https://magiclink.test/"), options =>
        {
            options.CodeLifetime = TimeSpan.FromMinutes(lifetimeMinutes);
            options.MaxCodeAttempts = attempts;
            options.MaxEmailsPerAddress = emails;
            options.EmailLimitWindow = TimeSpan.FromMinutes(windowMinutes);
            options.MaxSignInsPerClient = signIns;
            options.ClientLimitWindow = TimeSpan.FromMinutes(clientWindowMinutes);
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 587, "Sign-in <signin@example.com>", "Host")]
    [InlineData("smtp.example.com", 0, "signin@example.com", "Port")]
    [InlineData("smtp.example.com", 587, null, "From")]
    [InlineData("smtp.example.com", 587, "not an address", "From")]
    public async Task MagicLink_does_not_start_without_the_settings_of_its_default_smtp_sender(string? host, int port, string? from, string setting)
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, smtp: options =>
        {
            options.Host = host;
            options.Port = port;
            options.From = from;
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("MagicLinkSmtpOptions." + setting, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MagicLink_does_not_start_with_an_unknown_smtp_security_setting()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, smtp: static options =>
        {
            options.Host = "smtp.example.com";
            options.From = "signin@example.com";
            options.Security = (MagicLinkSmtpSecurity)42;
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("MagicLinkSmtpOptions.Security", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MagicLink_does_not_start_with_an_empty_subject()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, smtp: static options =>
        {
            options.Host = "smtp.example.com";
            options.From = "signin@example.com";
            options.Subject = " ";
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("MagicLinkSmtpOptions.Subject", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task By_setting_alone_the_email_is_sent_over_smtp()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, smtp: static options =>
        {
            options.Host = "smtp.example.com";
            options.From = "signin@example.com";
        });

        Assert.IsType<SmtpSignInMessageSender>(app.Services.GetRequiredService<ISignInMessageSender>());
        Assert.NotNull(app.Services.GetRequiredService<IOptions<MagicLinkSmtpOptions>>().Value);
        Assert.Single(app.Services.GetServices<IHostedService>().OfType<SignInMailWorker>());
    }

    [Fact]
    public async Task An_own_sender_replaces_smtp_and_needs_none_of_its_settings()
    {
        var own = new OwnSender();
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, smtp: null, sender: own);

        Assert.Same(own, app.Services.GetRequiredService<ISignInMessageSender>());

        // Without SMTP settings the start check would refuse SMTP; it passes for the own sender.
        Assert.NotNull(app.Services.GetRequiredService<IOptions<MagicLinkSmtpOptions>>().Value);
    }

    [Fact]
    public async Task MagicLink_does_not_start_without_a_source_of_who_may_sign_in()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: static _ => { });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("who may sign in", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MagicLink_does_not_start_with_a_file_of_who_may_sign_in_that_it_cannot_read_and_says_why()
    {
        var missing = Path.Combine(Path.GetTempPath(), "magiclink-" + Guid.NewGuid().ToString("N") + ".json");
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: services => services.AddMagicLinkSignInFile(options => options.Path = missing));

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_own_source_of_who_may_sign_in_replaces_the_file()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: static services =>
        {
            services.AddMagicLinkSignInFile(static options => options.Path = "not read");
            services.AddMagicLinkSignInSource<OwnSource>();
        });

        Assert.IsType<OwnSource>(app.Services.GetRequiredService<ISignInSource>());

        // The file is no longer the source, so its start check passes without a file to read.
        Assert.NotNull(app.Services.GetRequiredService<IOptions<MagicLinkSignInFileOptions>>().Value);
    }

    [Fact]
    public async Task The_file_replaces_an_own_source_registered_before_it()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: static services =>
        {
            services.AddMagicLinkSignInSource<OwnSource>();
            services.AddMagicLinkSignInFile(static options => options.Path = "people.json");
        });

        Assert.IsType<SignInFileSource>(app.Services.GetRequiredService<ISignInSource>());
        Assert.Single(app.Services.GetServices<ISignInSource>());
    }

    [Fact]
    public async Task MagicLink_does_not_start_with_a_database_of_who_may_sign_in_that_it_cannot_query_and_says_why()
    {
        // Nothing listens on port 1, so the start check cannot run the query.
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: static services => services.AddMagicLinkSignInDatabase(
            Npgsql.NpgsqlFactory.Instance,
            static options =>
            {
                options.ConnectionString = "Host=127.0.0.1;Port=1;Database=users;Username=reader;Password=unused;Timeout=2";
                options.Query = "SELECT group_name FROM memberships WHERE email = @email";
            }));

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("cannot run its query", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_database_replaces_the_file_and_only_the_source_in_use_is_checked()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: static services =>
        {
            services.AddMagicLinkSignInFile(static options => options.Path = "not read");
            services.AddMagicLinkSignInDatabase(Npgsql.NpgsqlFactory.Instance, static options => options.Query = "SELECT 1");
        });

        Assert.IsType<SignInDatabaseSource>(app.Services.GetRequiredService<ISignInSource>());
        Assert.Single(app.Services.GetServices<ISignInSource>());

        // The file is no longer the source, so its start check passes without a file to read.
        Assert.NotNull(app.Services.GetRequiredService<IOptions<MagicLinkSignInFileOptions>>().Value);
    }

    [Fact]
    public async Task An_own_source_replaces_the_database_and_needs_none_of_its_settings()
    {
        await using var app = Build(new Uri("https://magiclink.test/"), configure: null, source: static services =>
        {
            services.AddMagicLinkSignInDatabase(Npgsql.NpgsqlFactory.Instance, static _ => { });
            services.AddMagicLinkSignInSource<OwnSource>();
        });

        Assert.IsType<OwnSource>(app.Services.GetRequiredService<ISignInSource>());
        Assert.NotNull(app.Services.GetRequiredService<IOptions<MagicLinkSignInDatabaseOptions>>().Value);
    }

    private static WebApplication Build(Uri? issuer, Action<MagicLinkOptions>? configure, Action<MagicLinkSmtpOptions>? smtp = null, ISignInMessageSender? sender = null, Action<IServiceCollection>? source = null)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
        if (source is null)
        {
            builder.Services.AddMagicLinkSignInSource<OwnSource>();
        }
        else
        {
            source(builder.Services);
        }

        if (sender is not null || smtp is null)
        {
            // Registered before MagicLink.NET, which then keeps it; without SMTP settings the tests use their own sender.
            builder.Services.AddSingleton(sender ?? new OwnSender());
        }

        builder.Services.AddOpenIddict()
            .AddCore(options => options.UsePostgreSql(store => store.ConnectionString = "Host=localhost;Database=unused"))
            .AddMagicLinkServer(server =>
            {
                server.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                if (issuer is not null)
                {
                    server.SetIssuer(issuer);
                }
            });
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        if (smtp is not null)
        {
            builder.Services.Configure(smtp);
        }

        return builder.Build();
    }

    private sealed class OwnSource : ISignInSource
    {
        public ValueTask<SignInAccess> LookUpAsync(string email, CancellationToken cancellationToken) => ValueTask.FromResult(SignInAccess.Refused);
    }

    private sealed class OwnSender : ISignInMessageSender
    {
        public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
