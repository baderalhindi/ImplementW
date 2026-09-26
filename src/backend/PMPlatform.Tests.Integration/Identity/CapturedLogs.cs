using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>Every log line the API writes — message, scopes and exception — so a test can search them for credentials (CTL-27).</summary>
public sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public string Text => string.Join('\n', _lines);

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<string> lines) : ILogger
    {
        private readonly AsyncLocal<List<object?>> _scopes = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            List<object?> scopes = _scopes.Value ??= [];
            scopes.Add(state);
            return new Scope(scopes);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            StringBuilder line = new($"{logLevel} {category}: {formatter(state, exception)}");
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                line.Append(" | ").AppendJoin(", ", values.Select(v => $"{v.Key}={v.Value}"));
            }

            foreach (object? scope in _scopes.Value ?? [])
            {
                line.Append(" | scope ").Append(scope is IEnumerable<KeyValuePair<string, object>> pairs
                    ? string.Join(", ", pairs.Select(p => $"{p.Key}={p.Value}"))
                    : scope?.ToString());
            }

            if (exception is not null)
            {
                line.Append(" | ").Append(exception);
            }

            lines.Enqueue(line.ToString());
        }

        private sealed class Scope(List<object?> scopes) : IDisposable
        {
            public void Dispose() => scopes.RemoveAt(scopes.Count - 1);
        }
    }
}
