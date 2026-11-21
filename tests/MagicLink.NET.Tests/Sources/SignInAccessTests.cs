namespace MagicLink.Tests.Sources;

public sealed class SignInAccessTests
{
    [Fact]
    public void An_own_source_cannot_hand_out_an_empty_group()
    {
        Assert.Throws<ArgumentException>(static () => SignInAccess.Allowed(["admins", " "]));
    }

    [Fact]
    public void A_refused_address_has_no_groups()
    {
        Assert.False(SignInAccess.Refused.MaySignIn);
        Assert.Empty(SignInAccess.Refused.Groups);
    }
}
