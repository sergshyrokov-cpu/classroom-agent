using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Hosting;

/// <summary>
/// US-049 AC-006 (spec FR-005, OD-003 a, S-02, I-3): a host whose start-up decision is told it is not on Windows
/// refuses to start through its existing start-up failure path, names DPAPI as the reason, and writes nothing to its
/// key directory. The decision is supplied to <c>BuildApplication</c> the way US-047 supplies <c>runsAsService</c>.
/// </summary>
public sealed class NonWindowsStartRefusalTests
{
    private const string Reason = "DPAPI";

    [Fact]
    public async Task TheInstallation_OffWindows_RefusesToStart_WithAFatalLogLine_AndWritesNoKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");

        var exception = Assert.ThrowsAny<Exception>(
            () => BuiltHost.BuildOffWindows(BuiltHostKind.Installation, folders.Absolute("logs"), keys));

        Assert.IsNotType<NotImplementedException>(exception);
        var events = LogEvent.Parse(await BuiltHost.ReadFilesAsync(folders.Absolute("logs"), ct));
        Assert.Contains(events, e => e.Level == "Fatal"
            && (e.Property("Reason") ?? string.Empty).Contains(Reason, StringComparison.Ordinal));
        Assert.Empty(HostFolders.Files(keys, "*"));
    }

    [Fact]
    public void TheControlPlane_OffWindows_RefusesToStart_WithTheReasonInTheException_AndWritesNoKey()
    {
        using var folders = new HostFolders();
        var keys = folders.Absolute("keys");

        var exception = Assert.ThrowsAny<Exception>(
            () => BuiltHost.BuildOffWindows(BuiltHostKind.ControlPlane, folders.Absolute("logs"), keys));

        Assert.IsNotType<NotImplementedException>(exception);
        Assert.Contains(Reason, ExceptionText(exception), StringComparison.Ordinal);
        Assert.Empty(HostFolders.Files(keys, "*"));
    }

    private static string ExceptionText(Exception exception)
    {
        var parts = new List<string>();
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            parts.Add(e.Message);
        }

        return string.Join(Environment.NewLine, parts);
    }
}
