using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-047: what a host returned by <c>BuildApplication</c> is wired with, read without starting it — a test process is
/// never started by the Service Control Manager, and the Windows service lifetime cannot start outside one (OD-001 a).
/// </summary>
public static class BuiltHost
{
    /// <summary>The lifetime of <c>Microsoft.Extensions.Hosting.WindowsServices</c>, named so no test compiles against it.</summary>
    public const string WindowsServiceLifetime = "Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceLifetime";

    /// <summary>The lifetime of an ordinary console start.</summary>
    public const string ConsoleLifetime = "Microsoft.Extensions.Hosting.Internal.ConsoleLifetime";

    /// <summary>The logger factory <c>AddSerilog</c> puts in place of the default one (DC-10).</summary>
    public const string SerilogLoggerFactory = "Serilog.Extensions.Logging.SerilogLoggerFactory";

    /// <summary>The category of the event a test writes to prove where log events go.</summary>
    public const string ProbeCategory = "ClassroomAgent.Tests.US047";

    /// <summary>
    /// Builds a host through its <c>BuildApplication</c>, as a service or not, with the given directories; a console
    /// build may name its content root the way a console start can (<c>--contentRoot</c>).
    /// </summary>
    public static WebApplication Build(
        BuiltHostKind host,
        bool runsAsService,
        string? logDirectory,
        string? keyDirectory,
        string? contentRoot = null,
        string connectionString = HostBuildArguments.UnreachedConnectionString)
    {
        var settings = host == BuiltHostKind.Installation
            ? HostBuildArguments.Installation(logDirectory, keyDirectory)
            : HostBuildArguments.ControlPlane(logDirectory, keyDirectory, connectionString);
        if (contentRoot is not null)
        {
            settings["contentRoot"] = contentRoot;
        }

        var args = HostBuildArguments.From(settings);
        return host == BuiltHostKind.Installation
            ? ClassroomAgent.Web.Program.BuildApplication(args, runsAsService)
            : Program.BuildApplication(args, runsAsService);
    }

    /// <summary>The rolling file name pattern of a host's log (DC-10).</summary>
    public static string LogFilePattern(BuiltHostKind host) =>
        host == BuiltHostKind.Installation ? "installation-*.log" : "control-plane-*.log";

    public static string LifetimeType(WebApplication app) =>
        app.Services.GetRequiredService<IHostLifetime>().GetType().FullName!;

    public static string LoggerFactoryType(WebApplication app) =>
        app.Services.GetRequiredService<ILoggerFactory>().GetType().FullName!;

    /// <summary>Every registered logging provider, by type, in registration order.</summary>
    public static IReadOnlyList<string> LoggerProviderTypes(WebApplication app) =>
        [.. app.Services.GetServices<ILoggerProvider>().Select(provider => provider.GetType().FullName!)];

    /// <summary>
    /// The configuration sources an operator can fill, in order: the provider type, and for a file the file name.
    /// In-memory providers are left out — they carry host defaults such as the content root itself, which differs
    /// between a service and a console start by design (spec FR-003).
    /// </summary>
    public static IReadOnlyList<string> ConfigurationSources(WebApplication app) =>
        [.. ((IConfigurationRoot)app.Configuration).Providers
            .Where(provider => provider is not MemoryConfigurationProvider)
            .Select(provider => provider is JsonConfigurationProvider json
            ? $"{provider.GetType().Name}:{json.Source.Path}"
            : provider.GetType().Name)];

    /// <summary>Writes one information event through the host's logging and returns its unique text.</summary>
    public static string WriteProbeEvent(WebApplication app)
    {
        var text = "us047-probe-" + Guid.NewGuid().ToString("N");
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(ProbeCategory);
#pragma warning disable CA1848, CA2254 // A one-off probe line, not production logging.
        logger.LogInformation(text);
#pragma warning restore CA1848, CA2254
        return text;
    }

    /// <summary>Protects a value, so the key ring writes its first key into the key directory.</summary>
    public static void CreateDataProtectionKey(WebApplication app) =>
        app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("US-047").Protect("probe");

    /// <summary>The text of every file in a directory, or none when it does not exist.</summary>
    public static async Task<IReadOnlyList<string>> ReadFilesAsync(string directory, CancellationToken cancellationToken) =>
        Directory.Exists(directory) ? await HostLogs.ReadFilesAsync(directory, cancellationToken) : [];
}
