namespace MagicLink;

/// <summary>
/// Counts the sign-in emails each address got in a sliding window, so nobody can flood a person's mailbox by starting
/// sign-ins for their address. Kept in memory, like <see cref="SignInMailQueue"/>: each process counts on its own, and
/// a restart starts the counts afresh.
/// </summary>
/// <remarks>
/// Only addresses that may sign in are counted, since only they get emails; the page answers the same either way, so
/// the count tells nobody which addresses exist. An address is forgotten once its last email is a window old.
/// </remarks>
internal sealed class SignInMailLimit
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _sent = new(StringComparer.Ordinal);
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
    /// True, and counted, when <paramref name="email"/> got fewer than <paramref name="max"/> emails in the
    /// <paramref name="window"/> before <paramref name="now"/>; false, and not counted, when it got that many already.
    /// </summary>
    public bool TryTake(string email, DateTimeOffset now, int max, TimeSpan window)
    {
        var since = now - window;
        lock (_lock)
        {
            Sweep(now, since, window);
            if (!_sent.TryGetValue(email, out var sent))
            {
                sent = new Queue<DateTimeOffset>(max);
                _sent.Add(email, sent);
            }

            Forget(sent, since);
            if (sent.Count >= max)
            {
                return false;
            }

            sent.Enqueue(now);
            return true;
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

    /// <summary>Drops the emails of <paramref name="sent"/> that are a window old.</summary>
    private static void Forget(Queue<DateTimeOffset> sent, DateTimeOffset since)
    {
        while (sent.TryPeek(out var oldest) && oldest <= since)
        {
            sent.Dequeue();
        }
    }
}
