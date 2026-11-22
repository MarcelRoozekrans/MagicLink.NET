using System.Net;
using MagicLink.PostgreSql.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MagicLink.Host.Tests;

// The container passes MagicLink.NET's own settings through from configuration, as an app sets them in code.
public sealed class SettingsTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncDisposable
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
    public void The_codes_and_the_email_follow_the_settings()
    {
        var settings = _setup.Settings(database.ConnectionString, smtpPort: 2525);
        settings["MagicLink:CodeLifetime"] = "00:05:00";
        settings["MagicLink:MaxCodeAttempts"] = "3";
        settings["MagicLink:MaxEmailsPerAddress"] = "2";
        settings["MagicLink:EmailLimitWindow"] = "01:00:00";
        settings["MagicLink:MaxSignInsPerClient"] = "50";
        settings["MagicLink:ClientLimitWindow"] = "00:30:00";
        settings["MagicLink:Smtp:UserName"] = "mailer";
        settings["MagicLink:Smtp:Subject"] = "Sign in to Example";
        _host = new HostFactory(settings);

        var codes = _host.Services.GetRequiredService<IOptions<MagicLinkOptions>>().Value;
        var smtp = _host.Services.GetRequiredService<IOptions<MagicLinkSmtpOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(5), codes.CodeLifetime);
        Assert.Equal(3, codes.MaxCodeAttempts);
        Assert.Equal(2, codes.MaxEmailsPerAddress);
        Assert.Equal(TimeSpan.FromHours(1), codes.EmailLimitWindow);
        Assert.Equal(50, codes.MaxSignInsPerClient);
        Assert.Equal(TimeSpan.FromMinutes(30), codes.ClientLimitWindow);
        Assert.Equal(2525, smtp.Port);
        Assert.False(smtp.UseStartTls);
        Assert.Equal("mailer", smtp.UserName);
        Assert.Equal("Sign in to Example", smtp.Subject);
    }

    [Fact]
    public void The_trusted_proxies_follow_the_settings()
    {
        var settings = _setup.Settings(database.ConnectionString, smtpPort: 2525);
        settings["MagicLink:ForwardedHeaders:KnownProxies:0"] = "10.0.0.5";
        settings["MagicLink:ForwardedHeaders:KnownNetworks:0"] = "172.16.0.0/12";
        _host = new HostFactory(settings);

        var forwarded = _host.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal([IPAddress.Parse("10.0.0.5")], forwarded.KnownProxies);
        Assert.Equal([System.Net.IPNetwork.Parse("172.16.0.0/12")], forwarded.KnownIPNetworks);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, forwarded.ForwardedHeaders);
    }

    [Fact]
    public void Without_settings_only_the_framework_defaults_are_trusted()
    {
        _host = new HostFactory(_setup.Settings(database.ConnectionString, smtpPort: 2525));

        var forwarded = _host.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // The framework's defaults: only the loopback address is trusted, and only one hop is read.
        Assert.Equal([IPAddress.IPv6Loopback], forwarded.KnownProxies);
        Assert.Equal([System.Net.IPNetwork.Parse("127.0.0.0/8")], forwarded.KnownIPNetworks);
        Assert.Equal(1, forwarded.ForwardLimit);
    }
}
