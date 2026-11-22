using MagicLink.PostgreSql.Tests;
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
        settings["MagicLink:Smtp:UserName"] = "mailer";
        settings["MagicLink:Smtp:Subject"] = "Sign in to Example";
        _host = new HostFactory(settings);

        var codes = _host.Services.GetRequiredService<IOptions<MagicLinkOptions>>().Value;
        var smtp = _host.Services.GetRequiredService<IOptions<MagicLinkSmtpOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(5), codes.CodeLifetime);
        Assert.Equal(3, codes.MaxCodeAttempts);
        Assert.Equal(2, codes.MaxEmailsPerAddress);
        Assert.Equal(TimeSpan.FromHours(1), codes.EmailLimitWindow);
        Assert.Equal(2525, smtp.Port);
        Assert.False(smtp.UseStartTls);
        Assert.Equal("mailer", smtp.UserName);
        Assert.Equal("Sign in to Example", smtp.Subject);
    }
}
