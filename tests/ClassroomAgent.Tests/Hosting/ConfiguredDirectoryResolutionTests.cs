using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Configuration;

namespace ClassroomAgent.Tests.Hosting;

/// <summary>
/// US-047 AC-006 (spec FR-003, FR-004, VR-001): a relative <c>LogFile:Directory</c> or
/// <c>DataProtection:KeyDirectory</c> resolves against the host's content root, an absolute one is used as given.
/// A console build names a content root apart from the working directory, so the console cases tell the content root
/// from the working directory; a service's content root is the application folder.
/// </summary>
public sealed class ConfiguredDirectoryResolutionTests
{
    public static TheoryData<BuiltHostKind> Hosts => new() { BuiltHostKind.Installation, BuiltHostKind.ControlPlane };

    public static TheoryData<BuiltHostKind, bool> HostsInBothModes => new()
    {
        { BuiltHostKind.Installation, true },
        { BuiltHostKind.Installation, false },
        { BuiltHostKind.ControlPlane, true },
        { BuiltHostKind.ControlPlane, false },
    };

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task FromTheConsole_ARelativeLogDirectory_ResolvesAgainstTheContentRoot(BuiltHostKind host)
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var relative = folders.Relative("logs");
        string probe;
        await using (var app = BuiltHost.Build(
            host, runsAsService: false, relative, folders.Absolute("keys"), contentRoot: folders.Root))
        {
            probe = BuiltHost.WriteProbeEvent(app);
        }

        var files = await BuiltHost.ReadFilesAsync(Path.Combine(folders.Root, relative), ct);
        Assert.Contains(files, content => content.Contains(probe, StringComparison.Ordinal));
        Assert.Empty(HostFolders.Files(Path.Combine(HostFolders.WorkingDirectory, relative), "*"));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task FromTheConsole_ARelativeKeyDirectory_ResolvesAgainstTheContentRoot(BuiltHostKind host)
    {
        using var folders = new HostFolders();
        var relative = folders.Relative("keys");
        await using (var app = BuiltHost.Build(
            host, runsAsService: false, folders.Absolute("logs"), relative, contentRoot: folders.Root))
        {
            BuiltHost.CreateDataProtectionKey(app);
        }

        Assert.NotEmpty(HostFolders.Files(Path.Combine(folders.Root, relative), "key-*.xml"));
        Assert.Empty(HostFolders.Files(Path.Combine(HostFolders.WorkingDirectory, relative), "*"));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task AsAService_RelativeDirectories_ResolveIntoTheApplicationFolder(BuiltHostKind host)
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var logs = folders.Relative("logs");
        var keys = folders.Relative("keys");
        string probe;
        await using (var app = BuiltHost.Build(host, runsAsService: true, logs, keys))
        {
            probe = BuiltHost.WriteProbeEvent(app);
            BuiltHost.CreateDataProtectionKey(app);
        }

        var files = await BuiltHost.ReadFilesAsync(Path.Combine(HostFolders.ApplicationFolder, logs), ct);
        Assert.Contains(files, content => content.Contains(probe, StringComparison.Ordinal));
        Assert.NotEmpty(HostFolders.Files(Path.Combine(HostFolders.ApplicationFolder, keys), "key-*.xml"));
    }

    [Theory]
    [MemberData(nameof(HostsInBothModes))]
    public async Task AbsoluteDirectories_AreUsedAsGiven(BuiltHostKind host, bool runsAsService)
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var logs = folders.Absolute("logs");
        var keys = folders.Absolute("keys");
        string probe;
        await using (var app = BuiltHost.Build(host, runsAsService, logs, keys))
        {
            probe = BuiltHost.WriteProbeEvent(app);
            BuiltHost.CreateDataProtectionKey(app);
        }

        var files = await BuiltHost.ReadFilesAsync(logs, ct);
        Assert.Contains(files, content => content.Contains(probe, StringComparison.Ordinal));
        Assert.NotEmpty(HostFolders.Files(keys, "key-*.xml"));
    }

    [Fact]
    public async Task FromTheConsole_TheInstallationRefusalRecord_GoesToTheResolvedLogDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        using var folders = new HostFolders();
        var relative = folders.Relative("logs");

        Assert.Throws<InstallationSettingException>(() => BuiltHost.Build(
            BuiltHostKind.Installation, runsAsService: false, relative, keyDirectory: null, contentRoot: folders.Root));

        var files = await BuiltHost.ReadFilesAsync(Path.Combine(folders.Root, relative), ct);
        Assert.Contains(files, content => content.Contains(InstallationConfigurationKeys.DataProtectionKeyDirectory, StringComparison.Ordinal));
        Assert.Empty(HostFolders.Files(Path.Combine(HostFolders.WorkingDirectory, relative), "*"));
    }
}
