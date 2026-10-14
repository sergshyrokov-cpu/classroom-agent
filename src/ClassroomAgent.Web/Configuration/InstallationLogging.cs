using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// DC-10 logging of the installation: one JSON object per line, a file per day, 30 days, a size cap; the
/// request id from the log scope, and the route of the request in place of its path (US-045); the console only in development. The same file receives the refusal to
/// start when a setting is wrong (US-005 spec FR-001).
/// </summary>
public static class InstallationLogging
{
    public const string DirectoryKey = "LogFile:Directory";

    private const string FileName = "installation-.log";

    /// <summary>
    /// The configured log directory, a relative value resolved against the content root (US-047 FR-004), or
    /// <c>logs</c> next to the application when unset.
    /// </summary>
    public static string Directory(IConfiguration configuration, string contentRoot) =>
        configuration[DirectoryKey] is { Length: > 0 } directory
            ? ConfiguredDirectory.Resolve(directory, contentRoot)
            : Path.Combine(AppContext.BaseDirectory, "logs");

    public static LoggerConfiguration Configure(
        LoggerConfiguration logger,
        string directory,
        bool isDevelopment,
        IHttpContextAccessor httpContextAccessor)
    {
        logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new RequestRouteEnricher(httpContextAccessor))
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(directory, FileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true);
        if (isDevelopment)
        {
            logger.WriteTo.Console();
        }

        return logger;
    }

    /// <summary>Writes the refusal to start — the setting and the rule, never the value — and flushes the file.</summary>
    public static void WriteStartupRefusal(string directory, InstallationSettingException exception, bool isDevelopment)
    {
        // Written before the host exists: outside any request, so no line carries a request path.
        using var logger = Configure(new LoggerConfiguration(), directory, isDevelopment, new HttpContextAccessor()).CreateLogger();
        logger.Fatal("The installation does not start: {Reason}", exception.Message);
    }
}
