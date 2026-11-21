using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MagicLink.Tests;

/// <summary>Keeps what every logger of a host logs, with its category, so a test can read it.</summary>
internal sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(string Category, LogLevel Level, string Message)> _entries = new();

    /// <summary>Everything logged so far, in order.</summary>
    public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Logger(ListLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            provider._entries.Enqueue((category, logLevel, formatter(state, exception)));
    }
}
