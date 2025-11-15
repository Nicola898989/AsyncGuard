using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace AsyncGuard.Tests.Infrastructure;

internal sealed class TestLogger : ILogger
{
    private readonly List<TestLogEntry> _entries = new();

    public IReadOnlyList<TestLogEntry> Entries => _entries;

    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        return NullScope.Instance;
    }

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _entries.Add(new TestLogEntry(logLevel, message, exception, state));
    }

    private sealed record NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}

internal sealed record TestLogEntry(LogLevel Level, string Message, Exception? Exception, object? State);
