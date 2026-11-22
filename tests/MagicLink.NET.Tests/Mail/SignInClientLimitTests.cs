using System.Net;

namespace MagicLink.Tests.Mail;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 8 (issue #55): what a client counts as.
public sealed class SignInClientLimitTests
{
    [Theory]
    [InlineData("192.0.2.10", "192.0.2.10")]
    [InlineData("::ffff:192.0.2.10", "192.0.2.10")]
    [InlineData("2001:db8:aa:1::5", "2001:db8:aa:1::/64")]
    [InlineData("2001:db8:aa:1:ffff:ffff:ffff:ffff", "2001:db8:aa:1::/64")]
    [InlineData("::1", "::/64")]
    public void A_client_counts_under_its_ipv4_address_or_its_ipv6_64_network(string address, string key)
    {
        Assert.Equal(key, SignInClientLimit.Key(IPAddress.Parse(address)));
    }
}
