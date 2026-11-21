namespace MagicLink.Tests;

// The sign-in page returns only to a path on this site (issue #4, comment on the return URL).
public sealed class LocalUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/connect/authorize?client_id=app&redirect_uri=https%3A%2F%2Fapp.test%2F")]
    [InlineData("/a//b")]
    public void A_path_on_this_site_is_local(string url)
    {
        Assert.True(LocalUrl.IsLocal(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/")]
    [InlineData("evil.example")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("/\t/evil.example/")]
    [InlineData("/\r\n/evil.example/")]
    [InlineData("/path\u0000")]
    public void Anything_else_is_not(string? url)
    {
        Assert.False(LocalUrl.IsLocal(url));
    }
}
