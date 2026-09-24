using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Inventory.IntegrationTests;

/// <summary>Captures a host's warnings, so a test can assert what startup told the operator.</summary>
public sealed class LogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<string> warnings = new();

    public IReadOnlyCollection<string> Warnings => warnings;

    public ILogger CreateLogger(string categoryName) => new Collector(warnings);

    public void Dispose()
    {
    }

    private sealed class Collector(ConcurrentQueue<string> warnings) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                warnings.Enqueue(formatter(state, exception));
            }
        }
    }
}
