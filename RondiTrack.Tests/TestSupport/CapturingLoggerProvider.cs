using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RondiTrack.Tests.TestSupport;

// Keeps every log entry in memory so a test can assert on what was (and wasn't) written.
// The exception text is captured too, because exceptions are logged in full at Error.
public sealed record CapturedLog(LogLevel Level, string Category, string Message, string? ExceptionText);

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyList<CapturedLog> Entries => _entries.ToArray();

    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            sink.Enqueue(new CapturedLog(logLevel, category, formatter(state, exception), exception?.ToString()));
    }
}