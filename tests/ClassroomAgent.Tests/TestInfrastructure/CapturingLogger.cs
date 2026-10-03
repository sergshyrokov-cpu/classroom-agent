using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps every entry it receives, with its level, event id and rendered
/// message, so a test can assert what a component logged and what it left out.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = [];

    public IReadOnlyList<Entry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        lock (_gate)
        {
            _entries.Add(new Entry(logLevel, eventId, formatter(state, exception), exception));
        }
    }

    public sealed record Entry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}
