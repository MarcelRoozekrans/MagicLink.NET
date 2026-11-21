using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    [InlineData(0, 5, "CodeLifetime")]
    [InlineData(10, 0, "MaxCodeAttempts")]
    public async Task MagicLink_does_not_start_with_settings_that_lock_everyone_out(int lifetimeMinutes, int attempts, string setting)
    {
        await using var app = Build(new Uri("https://magiclink.test/"), options =>
        {
            options.CodeLifetime = TimeSpan.FromMinutes(lifetimeMinutes);
            options.MaxCodeAttempts = attempts;
        });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_a_mail_sender_the_email_is_not_sent_and_the_log_says_so_without_link_or_code()
    {
        var logger = new ListLogger<UnsentSignInMessages>();
        var sender = new UnsentSignInMessages(logger);

        await sender.SendAsync(new SignInMessage("person@example.com", new Uri("https://magiclink.test/signin/link?token=secret"), "ABCD-EFGH", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("secret", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCD", entry.Message, StringComparison.Ordinal);
    }

    private static WebApplication Build(Uri? issuer, Action<MagicLinkOptions>? configure)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
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

        return builder.Build();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
