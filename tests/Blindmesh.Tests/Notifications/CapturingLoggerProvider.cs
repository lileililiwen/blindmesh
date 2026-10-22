using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// The logger that keeps the lines of the worker of the test run, so a test can
/// prove that the log states the uncertainty of a failed attempt and that a
/// dead letter is named without a provider detail.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    /// <summary>The lines that the worker wrote, in the order they arrived.</summary>
    public IReadOnlyList<string> Lines => _lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            lines.Enqueue(formatter(state, exception));
        }
    }
}
