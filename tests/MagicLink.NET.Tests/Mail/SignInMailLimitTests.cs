namespace MagicLink.Tests.Mail;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 7 (issue #39): at most a few sign-in
// emails per address in a sliding window.
public sealed class SignInMailLimitTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly DateTimeOffset Start = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_address_gets_as_many_emails_as_allowed_and_then_none()
    {
        var limit = new SignInMailLimit();

        for (var i = 0; i < 5; i++)
        {
            Assert.True(limit.TryTake("person@example.com", Start.AddMinutes(i), 5, Window));
        }

        Assert.False(limit.TryTake("person@example.com", Start.AddMinutes(5), 5, Window));
    }

    [Fact]
    public void Another_address_has_its_own_count()
    {
        var limit = new SignInMailLimit();
        Assert.True(limit.TryTake("person@example.com", Start, 1, Window));

        Assert.True(limit.TryTake("other@example.com", Start, 1, Window));
        Assert.False(limit.TryTake("person@example.com", Start, 1, Window));
    }

    [Fact]
    public void An_email_counts_until_it_is_a_window_old()
    {
        var limit = new SignInMailLimit();
        Assert.True(limit.TryTake("person@example.com", Start, 2, Window));
        Assert.True(limit.TryTake("person@example.com", Start.AddMinutes(10), 2, Window));

        Assert.False(limit.TryTake("person@example.com", Start + Window - TimeSpan.FromTicks(1), 2, Window));
        Assert.True(limit.TryTake("person@example.com", Start + Window, 2, Window));
        Assert.False(limit.TryTake("person@example.com", Start + Window, 2, Window));
    }

    [Fact]
    public void A_refused_email_does_not_count()
    {
        var limit = new SignInMailLimit();
        Assert.True(limit.TryTake("person@example.com", Start, 1, Window));
        Assert.False(limit.TryTake("person@example.com", Start.AddMinutes(14), 1, Window));

        Assert.True(limit.TryTake("person@example.com", Start + Window, 1, Window));
    }

    [Fact]
    public void Addresses_whose_emails_are_all_a_window_old_are_forgotten()
    {
        var limit = new SignInMailLimit();
        Assert.True(limit.TryTake("first@example.com", Start, 5, Window));
        Assert.True(limit.TryTake("second@example.com", Start.AddMinutes(10), 5, Window));
        Assert.Equal(2, limit.Count);

        Assert.True(limit.TryTake("third@example.com", Start + Window, 5, Window));

        Assert.Equal(2, limit.Count);
    }

    [Fact]
    public void A_smaller_limit_set_later_applies_to_the_emails_already_sent()
    {
        var limit = new SignInMailLimit();
        Assert.True(limit.TryTake("person@example.com", Start, 5, Window));
        Assert.True(limit.TryTake("person@example.com", Start, 5, Window));

        Assert.False(limit.TryTake("person@example.com", Start, 2, Window));
    }
}
