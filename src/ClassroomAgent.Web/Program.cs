using System.Globalization;
using ClassroomAgent.Web.Configuration;
using ClassroomAgent.Web.Security;
using Serilog;

namespace ClassroomAgent.Web;

/// <summary>
/// The installation host (US-005 spec FR-014). A named class in a namespace, not top-level statements, so the
/// test project can reference both hosts without two global <c>Program</c> types. The host starts only with
/// valid settings (FR-001), serves liveness and readiness on the private port, maps nothing on the public port
/// (I-13) and never applies migrations (PC-2).
/// </summary>
public sealed class Program
{
    private Program()
    {
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var logDirectory = InstallationLogging.Directory(builder.Configuration);
        var isDevelopment = builder.Environment.IsDevelopment();

        InstallationSettings settings;
        try
        {
            settings = InstallationSettingsReader.Read(builder.Configuration);
        }
        catch (InstallationSettingException exception)
        {
            InstallationLogging.WriteStartupRefusal(logDirectory, exception, isDevelopment);
            throw;
        }

        builder.Services.AddSerilog(
            (_, logger) => InstallationLogging.Configure(logger, logDirectory, isDevelopment),
            preserveStaticLogger: true);

        // A second endpoint for the private port, plain HTTP (DC-6); the public endpoints stay as configured.
        builder.WebHost.UseUrls(
        [
            .. InstallationSettingsReader.PublicUrls(builder.Configuration),
            string.Create(CultureInfo.InvariantCulture, $"http://*:{settings.PrivatePort}"),
        ]);

        builder.Services.AddInstallation(settings);

        var app = builder.Build();
        app.UseMiddleware<PublicPortMiddleware>(settings.PrivatePort);
        app.UseRouting();
        app.MapPrivateEndpoints(settings.PrivatePort);
        app.Run();
    }
}
