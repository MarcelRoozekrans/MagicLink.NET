namespace MagicLink;

/// <summary>
/// Counts the sign-in emails each address got in a sliding window, so nobody can flood a person's mailbox by starting
/// sign-ins for their address. Kept in memory, like <see cref="SignInMailQueue"/>: each process counts on its own, and
/// a restart starts the counts afresh.
/// </summary>
/// <remarks>
/// Only addresses that may sign in are counted, since only they get emails; the page answers the same either way, so
/// the count tells nobody which addresses exist. The clock is read under the lock, so each address's times are in
/// order. An address is forgotten once its last email is a window old.
/// </remarks>
internal sealed class SignInMailLimit(TimeProvider clock)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _sent = new(StringComparer.Ordinal);
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    /// <summary>How many addresses are counted now; tests read it to see that old ones are forgotten.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _sent.Count;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="email"/> got fewer than <paramref name="max"/> emails in the last
    /// <paramref name="window"/>; the email is then counted, at <paramref name="at"/>, until it is released. False, and
    /// nothing counted, when the address got that many already.
    /// </summary>
    public bool TryReserve(string email, int max, TimeSpan window, out DateTimeOffset at)
    {
        lock (_lock)
        {
            at = clock.GetUtcNow();
            var since = at - window;
            Sweep(at, since, window);
            if (!_sent.TryGetValue(email, out var sent))
            {
                sent = new List<DateTimeOffset>(max);
                _sent.Add(email, sent);
            }

            Forget(sent, since);
            if (sent.Count >= max)
            {
                return false;
            }

            sent.Add(at);
            return true;
        }
    }

    /// <summary>
    /// Stops counting the email reserved for <paramref name="email"/> at <paramref name="at"/>, because it was not sent
    /// after all; nothing happens when it is no longer counted.
    /// </summary>
    public void Release(string email, DateTimeOffset at)
    {
        lock (_lock)
        {
            if (_sent.TryGetValue(email, out var sent))
            {
                sent.Remove(at);
            }
        }
    }

    /// <summary>Once per window, forgets the addresses whose last email is a window old.</summary>
    private void Sweep(DateTimeOffset now, DateTimeOffset since, TimeSpan window)
    {
        if (now - _lastSweep < window)
        {
            return;
        }

        _lastSweep = now;
        List<string>? old = null;
        foreach (var (email, sent) in _sent)
        {
            Forget(sent, since);
            if (sent.Count == 0)
            {
                (old ??= []).Add(email);
            }
        }

        if (old is not null)
        {
            foreach (var email in old)
            {
                _sent.Remove(email);
            }
        }
    }

    /// <summary>Drops the emails of <paramref name="sent"/>, oldest first, that are a window old.</summary>
    private static void Forget(List<DateTimeOffset> sent, DateTimeOffset since)
    {
        var old = 0;
        while (old < sent.Count && sent[old] <= since)
        {
            old++;
        }

        sent.RemoveRange(0, old);
    }
}
