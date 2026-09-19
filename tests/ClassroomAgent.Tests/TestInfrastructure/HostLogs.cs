namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Reads a started host's Serilog directory (DC-10) while the host is still running. The file sink is
/// unbuffered, so a line is on disk as soon as the host has written it, and the files are opened with
/// <see cref="FileShare.ReadWrite"/> — reading never has to stop the host first.
/// </summary>
/// <remarks>
/// Waiting for the log line itself is the only reliable synchronisation for a background activity.
/// Waiting for the network stub instead returns while the sender is still classifying the answer, and the
/// stop that a test's log read performs then cancels the sender before it logs — a cancelled push is
/// deliberately silent (US-006 spec I-9), so the assertion sees an empty journal.
/// </remarks>
internal static class HostLogs
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>The text of every file in the directory.</summary>
    public static async Task<IReadOnlyList<string>> ReadFilesAsync(string directory, CancellationToken cancellationToken)
    {
        var contents = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            await using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            contents.Add(await reader.ReadToEndAsync(cancellationToken));
        }

        return contents;
    }

    /// <summary>Every log event written so far, without stopping the host.</summary>
    public static async Task<IReadOnlyList<LogEvent>> ReadEventsAsync(string directory, CancellationToken cancellationToken) =>
        LogEvent.Parse(await ReadFilesAsync(directory, cancellationToken));

    /// <summary>
    /// Waits (real time, bounded) until the directory holds at least <paramref name="count"/> events named
    /// <paramref name="eventName"/>, then returns every event written so far.
    /// </summary>
    public static async Task<IReadOnlyList<LogEvent>> WaitForEventAsync(
        string directory,
        string eventName,
        int count,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        var seen = 0;
        while (true)
        {
            // A line can be torn: the host may be midway through writing it when this read happens.
            IReadOnlyList<LogEvent> events;
            try
            {
                events = await ReadEventsAsync(directory, cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                events = [];
            }

            seen = events.Count(e => e.EventName == eventName);
            if (seen >= count)
            {
                return events;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out waiting for {count} '{eventName}' log event(s); {seen} written.");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }
}
