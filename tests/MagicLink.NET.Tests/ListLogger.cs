using Microsoft.Extensions.Logging;

namespace MagicLink.Tests;

/// <summary>Keeps what is logged, also from background services, so a test can read it or wait for it.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly Lock _lock = new();
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>Waits until an entry at <paramref name="level"/> was logged, and returns the first.</summary>
    public async Task<(LogLevel Level, string Message, Exception? Exception)> WaitForAsync(LogLevel level)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            foreach (var entry in Entries)
            {
                if (entry.Level == level)
                {
                    return entry;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lock)
        {
            _entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
