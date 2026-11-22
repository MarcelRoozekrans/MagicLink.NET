namespace MagicLink.Tests;

// Values from docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 3:
// a code is valid for 10 minutes and allows 5 attempts; criterion 7: at most 5 emails per address in 15 minutes.
public sealed class MagicLinkOptionsTests
{
    [Fact]
    public void CodeLifetime_DefaultsTo10Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), new MagicLinkOptions().CodeLifetime);
    }

    [Fact]
    public void MaxCodeAttempts_DefaultsTo5()
    {
        Assert.Equal(5, new MagicLinkOptions().MaxCodeAttempts);
    }

    [Fact]
    public void MaxEmailsPerAddress_DefaultsTo5()
    {
        Assert.Equal(5, new MagicLinkOptions().MaxEmailsPerAddress);
    }

    [Fact]
    public void EmailLimitWindow_DefaultsTo15Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), new MagicLinkOptions().EmailLimitWindow);
    }

    [Fact]
    public void MaxSignInsPerClient_DefaultsTo20()
    {
        Assert.Equal(20, new MagicLinkOptions().MaxSignInsPerClient);
    }

    [Fact]
    public void ClientLimitWindow_DefaultsTo15Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), new MagicLinkOptions().ClientLimitWindow);
    }
}
