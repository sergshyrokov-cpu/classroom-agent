using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Hosting;

/// <summary>
/// US-047 AC-001, AC-002, AC-004, AC-007 (spec FR-001 … FR-003, FR-007, I-1, I-2, I-4): both hosts, built as a
/// service and from the console. Starting under the Service Control Manager itself is the manual check of OD-001 a).
/// </summary>
public sealed class WindowsServiceHostingTests
{
    public static TheoryData<BuiltHostKind> Hosts => new() { BuiltHostKind.Installation, BuiltHostKind.ControlPlane };

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AsAService_TheHostRunsWithTheWindowsServiceLifetime(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        await using var app = BuiltHost.Build(host, runsAsService: true, folders.Absolute("logs"), folders.Absolute("keys"));

        Assert.Equal(BuiltHost.WindowsServiceLifetime, BuiltHost.LifetimeType(app));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task FromTheConsole_TheHostKeepsTheConsoleLifetime(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        await using var app = BuiltHost.Build(host, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys"));

        Assert.Equal(BuiltHost.ConsoleLifetime, BuiltHost.LifetimeType(app));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AsAService_TheContentRootIsTheApplicationFolder(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        await using var app = BuiltHost.Build(host, runsAsService: true, folders.Absolute("logs"), folders.Absolute("keys"));

        HostFolders.AssertSamePath(HostFolders.ApplicationFolder, app.Environment.ContentRootPath);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task FromTheConsole_TheContentRootIsTheWorkingDirectory(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        await using var app = BuiltHost.Build(host, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys"));

        HostFolders.AssertSamePath(HostFolders.WorkingDirectory, app.Environment.ContentRootPath);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task FromTheConsole_AContentRootArgumentIsHonouredAsBefore(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        await using var app = BuiltHost.Build(
            host, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys"), contentRoot: folders.Root);

        HostFolders.AssertSamePath(folders.Root, app.Environment.ContentRootPath);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AsAService_TheConfigurationSourcesAreThoseOfAConsoleStart(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        IReadOnlyList<string> console;
        await using (var app = BuiltHost.Build(host, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys")))
        {
            console = BuiltHost.ConfigurationSources(app);
        }

        await using var service = BuiltHost.Build(host, runsAsService: true, folders.Absolute("logs"), folders.Absolute("keys"));

        Assert.Equal(console, BuiltHost.ConfigurationSources(service));
        Assert.Contains(console, source => source.EndsWith(":appsettings.json", StringComparison.Ordinal));
        Assert.Contains("CommandLineConfigurationProvider", console);
        Assert.Contains("EnvironmentVariablesConfigurationProvider", console);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AsAService_LoggingGoesThroughSerilogAlone_AndNoProviderIsAdded(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        IReadOnlyList<string> consoleProviders;
        await using (var app = BuiltHost.Build(host, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys")))
        {
            Assert.Equal(BuiltHost.SerilogLoggerFactory, BuiltHost.LoggerFactoryType(app));
            consoleProviders = BuiltHost.LoggerProviderTypes(app);
        }

        await using var service = BuiltHost.Build(host, runsAsService: true, folders.Absolute("logs"), folders.Absolute("keys"));

        Assert.Equal(BuiltHost.SerilogLoggerFactory, BuiltHost.LoggerFactoryType(service));
        Assert.Equal(consoleProviders, BuiltHost.LoggerProviderTypes(service));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AsAService_LogEventsGoToTheRollingFile(BuiltHostKind host)
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var logDirectory = folders.Absolute("logs");
        string probe;
        await using (var app = BuiltHost.Build(host, runsAsService: true, logDirectory, folders.Absolute("keys")))
        {
            probe = BuiltHost.WriteProbeEvent(app);
        }

        Assert.Single(HostFolders.Files(logDirectory, BuiltHost.LogFilePattern(host)));
        var files = await BuiltHost.ReadFilesAsync(logDirectory, ct);
        Assert.Contains(files, content => content.Contains(probe, StringComparison.Ordinal));
    }
}
