using MagicLink.Tests.SignIn;

namespace MagicLink.Tests.Mail;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 7 (issue #39): at most a few sign-in
// emails per address in a sliding window.
public sealed class SignInMailLimitTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly TestClock _clock = new();

    [Fact]
    public void An_address_gets_as_many_emails_as_allowed_and_then_none()
    {
        var limit = new SignInMailLimit(_clock);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(limit.TryReserve("person@example.com", 5, Window, out _));
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.False(limit.TryReserve("person@example.com", 5, Window, out _));
    }

    [Fact]
    public void Another_address_has_its_own_count()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("person@example.com", 1, Window, out _));

        Assert.True(limit.TryReserve("other@example.com", 1, Window, out _));
        Assert.False(limit.TryReserve("person@example.com", 1, Window, out _));
    }

    [Fact]
    public void An_email_counts_until_it_is_a_window_old()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("person@example.com", 2, Window, out _));
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.True(limit.TryReserve("person@example.com", 2, Window, out _));

        _clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.False(limit.TryReserve("person@example.com", 2, Window, out _));
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(limit.TryReserve("person@example.com", 2, Window, out _));
        Assert.False(limit.TryReserve("person@example.com", 2, Window, out _));
    }

    [Fact]
    public void A_refused_email_does_not_count()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("person@example.com", 1, Window, out _));
        _clock.Advance(TimeSpan.FromMinutes(14));
        Assert.False(limit.TryReserve("person@example.com", 1, Window, out _));

        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(limit.TryReserve("person@example.com", 1, Window, out _));
    }

    [Fact]
    public void A_released_email_frees_its_place()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("person@example.com", 2, Window, out _));
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(limit.TryReserve("person@example.com", 2, Window, out var second));

        limit.Release("person@example.com", second);

        Assert.True(limit.TryReserve("person@example.com", 2, Window, out _));
        Assert.False(limit.TryReserve("person@example.com", 2, Window, out _));
    }

    [Fact]
    public void Releasing_what_is_no_longer_counted_changes_nothing()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("person@example.com", 1, Window, out var first));
        _clock.Advance(Window);
        Assert.True(limit.TryReserve("person@example.com", 1, Window, out _));

        limit.Release("person@example.com", first);
        limit.Release("unknown@example.com", first);

        Assert.False(limit.TryReserve("person@example.com", 1, Window, out _));
    }

    [Fact]
    public void Addresses_whose_emails_are_all_a_window_old_are_forgotten()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("first@example.com", 5, Window, out _));
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.True(limit.TryReserve("second@example.com", 5, Window, out _));
        Assert.Equal(2, limit.Count);

        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(limit.TryReserve("third@example.com", 5, Window, out _));

        Assert.Equal(2, limit.Count);
    }

    [Fact]
    public void A_smaller_limit_set_later_applies_to_the_emails_already_sent()
    {
        var limit = new SignInMailLimit(_clock);
        Assert.True(limit.TryReserve("person@example.com", 5, Window, out _));
        Assert.True(limit.TryReserve("person@example.com", 5, Window, out _));

        Assert.False(limit.TryReserve("person@example.com", 2, Window, out _));
    }

    [Fact]
    public void A_limit_as_high_as_it_goes_still_works()
    {
        var limit = new SignInMailLimit(_clock);

        Assert.True(limit.TryReserve("person@example.com", int.MaxValue, Window, out _));
        Assert.True(limit.TryReserve("person@example.com", int.MaxValue, Window, out _));
    }
}
