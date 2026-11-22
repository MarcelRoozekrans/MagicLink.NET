namespace MagicLink;

/// <summary>
/// Counts events per key in a sliding window, such as the sign-in emails one address got. Kept in memory, like
/// <see cref="SignInMailQueue"/>: each process counts on its own, and a restart starts the counts afresh.
/// </summary>
/// <remarks>
/// The clock is read under the lock, so each key's times are in order. A key is forgotten once its last event is a
/// window old.
/// </remarks>
internal abstract class SlidingWindowLimit(TimeProvider clock)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _sent = new(StringComparer.Ordinal);
    private DateTimeOffset _lastSweep = DateTimeOffset.MinValue;

    /// <summary>How many keys are counted now; tests read it to see that old ones are forgotten.</summary>
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
    /// True when <paramref name="key"/> had fewer than <paramref name="max"/> events in the last
    /// <paramref name="window"/>; this one is then counted, at <paramref name="at"/>, until it is released. False, and
    /// nothing counted, when the key had that many already.
    /// </summary>
    public bool TryReserve(string key, int max, TimeSpan window, out DateTimeOffset at)
    {
        lock (_lock)
        {
            at = clock.GetUtcNow();
            var since = at - window;
            Sweep(at, since, window);
            if (!_sent.TryGetValue(key, out var sent))
            {
                sent = [];
                _sent.Add(key, sent);
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
    /// Stops counting the event reserved for <paramref name="key"/> at <paramref name="at"/>, because it did not happen
    /// after all; nothing happens when it is no longer counted.
    /// </summary>
    public void Release(string key, DateTimeOffset at)
    {
        lock (_lock)
        {
            if (_sent.TryGetValue(key, out var sent))
            {
                sent.Remove(at);
            }
        }
    }

    /// <summary>Once per window, forgets the keys whose last event is a window old.</summary>
    private void Sweep(DateTimeOffset now, DateTimeOffset since, TimeSpan window)
    {
        if (now - _lastSweep < window)
        {
            return;
        }

        _lastSweep = now;
        List<string>? old = null;
        foreach (var (key, sent) in _sent)
        {
            Forget(sent, since);
            if (sent.Count == 0)
            {
                (old ??= []).Add(key);
            }
        }

        if (old is not null)
        {
            foreach (var key in old)
            {
                _sent.Remove(key);
            }
        }
    }

    /// <summary>Drops the events of <paramref name="sent"/>, oldest first, that are a window old.</summary>
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
