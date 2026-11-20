namespace MagicLink.Tests;

public sealed class MagicLinkOptionsTests
{
    [Fact]
    public void TokenLifetime_DefaultsTo15Minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), new MagicLinkOptions().TokenLifetime);
    }

    [Fact]
    public void Validate_AcceptsDefault()
    {
        var options = new MagicLinkOptions();

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_AcceptsMaximum()
    {
        var options = new MagicLinkOptions { TokenLifetime = TimeSpan.FromHours(24) };

        var exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void MaxTokenLifetime_Is24Hours()
    {
        Assert.Equal(TimeSpan.FromHours(24), MagicLinkOptions.MaxTokenLifetime);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(864_000_000_001L)] // 24 hours plus one tick
    public void Validate_RejectsOutOfRange(long ticks)
    {
        var options = new MagicLinkOptions { TokenLifetime = TimeSpan.FromTicks(ticks) };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(MagicLinkOptions.TokenLifetime), exception.Message, StringComparison.Ordinal);
    }
}
