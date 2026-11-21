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

    private static WebApplication Build(Uri? issuer, Action<MagicLinkOptions>? configure, Action<MagicLinkSmtpOptions>? smtp = null, ISignInMessageSender? sender = null)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
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

    private sealed class OwnSender : ISignInMessageSender
    {
        public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
