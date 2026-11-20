namespace MagicLink.Tests;

// Values from docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 3:
// a code is valid for 10 minutes and allows 5 attempts.
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
}
