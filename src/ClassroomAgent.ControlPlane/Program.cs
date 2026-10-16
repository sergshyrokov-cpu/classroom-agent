using System.Globalization;
using System.Resources;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Push;
using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.WebEncoders;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

[assembly: NeutralResourcesLanguage("uk", UltimateResourceFallbackLocation.Satellite)]

// US-047 FR-001: the process decides once, at start-up, whether the Windows Service Control Manager started it — no
// setting, variable or argument selects the mode (VR-002).
Program.BuildApplication(args, WindowsServiceHelpers.IsWindowsService(), OperatingSystem.IsWindows()).Run();

/// <summary>Entry point, exposed to <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program
{
    /// <summary>
    /// US-049 spec FR-005: builds the Control Plane host with the operating-system decision supplied, the way
    /// <paramref name="runsAsService"/> is, so a test can drive the refusal outside Windows; never a setting, an
    /// environment variable or an argument (VR-003).
    /// </summary>
    public static WebApplication BuildApplication(string[] args, bool runsAsService, bool runsOnWindows)
    {
        // US-049 FR-005, I-3: refused before anything can write a key, through the start-up failure path a missing
        // required setting uses — the exception's message gives the reason.
        if (!runsOnWindows)
        {
            throw new PlatformNotSupportedException(NotOnWindowsReason);
        }

        return Build(args, runsAsService);
    }

    /// <summary>
    /// US-049 spec FR-005: why the Control Plane refuses to start outside Windows. It names the cause and carries no
    /// path or key material (S-02).
    /// </summary>
    internal const string NotOnWindowsReason =
        "The Data Protection keys are encrypted with Windows DPAPI, which this operating system does not provide; " +
        "the Control Plane runs only on Windows.";

    /// <summary>
    /// Builds the Control Plane host without running it (US-047 spec FR-001 … FR-004, I-1). <paramref name="runsAsService"/>
    /// is the detection result of FR-001, passed in so a test can drive either branch; it is never a setting, an
    /// environment variable or an argument (VR-002).
    /// </summary>
    public static WebApplication BuildApplication(string[] args, bool runsAsService) =>
        BuildApplication(args, runsAsService, OperatingSystem.IsWindows());

    private static WebApplication Build(string[] args, bool runsAsService)
    {
        var builder = CreateBuilder(args, runsAsService);
        var configuration = builder.Configuration;
        var contentRoot = builder.Environment.ContentRootPath;
        var connectionString = RequiredSetting(configuration, "ConnectionStrings:ControlPlane");

        // US-047 FR-004: a relative directory resolves against the content root, never the working directory.
        var keyDirectory = ResolveDirectory(RequiredSetting(configuration, "DataProtection:KeyDirectory"), contentRoot);
        var logDirectory = ResolveDirectory(RequiredSetting(configuration, "LogFile:Directory"), contentRoot);
        var compatibilityPolicy = CompatibilityPolicy.FromConfiguration(configuration);

        // DC-10: one JSON object per line, a file per day, 30 days, size cap; request id from the log scope, and the route of
        // the request in place of its path (US-045).
        builder.Services.AddSerilog(
            (services, logger) =>
            {
                logger
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .Enrich.With(new RequestRouteEnricher(services.GetRequiredService<IHttpContextAccessor>()))
                    .WriteTo.File(
                        new CompactJsonFormatter(),
                        Path.Combine(logDirectory, "control-plane-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        fileSizeLimitBytes: 50 * 1024 * 1024,
                        rollOnFileSizeLimit: true);
                if (builder.Environment.IsDevelopment())
                {
                    logger.WriteTo.Console();
                }
            },
            preserveStaticLogger: true);

        // US-045: the log enricher reads the endpoint of the request in progress.
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TimestampInterceptor>();
        builder.Services.AddDbContext<ControlPlaneDbContext>((services, options) =>
        {
            ControlPlaneDbContextOptions.Configure(options, connectionString);
            options.AddInterceptors(services.GetRequiredService<TimestampInterceptor>());
        });

        builder.Services.AddSingleton<IPasswordHasher<Owner>, PasswordHasher<Owner>>();
        builder.Services.AddSingleton<ILookupNormalizer, UpperInvariantLookupNormalizer>();
        builder.Services.AddSingleton<ISetupCodeGenerator, SetupCodeGenerator>();
        builder.Services.AddSingleton<IOperatorConsole, StandardOutputOperatorConsole>();
        builder.Services.AddSingleton<SetupCodeState>();
        builder.Services.AddSingleton<OwnerExistenceCache>();
        builder.Services.AddScoped<OwnerSessionService>();
        builder.Services.AddScoped<FirstRunSetupService>();
        builder.Services.AddScoped<OwnerSignInService>();
        builder.Services.AddScoped<InstallationRegistry>();
        builder.Services.AddScoped<AllowedAdminRegistry>();
        builder.Services.AddScoped<InstallationStatusService>();
        builder.Services.AddSingleton(compatibilityPolicy);
        builder.Services.AddScoped<LegitimacyCheckService>();
        builder.Services.AddScoped<AdminLoginCheckService>();

        // The status-change push (US-006 spec FR-006; api-design §6): plain HTTP to the school's private port,
        // no redirects and no cookies; the 10-second attempt timeout and the retry pauses are in the sender.
        builder.Services.AddHttpClient(StatusPushClient.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        builder.Services.AddSingleton<IStatusPushClient, StatusPushClient>();
        builder.Services.AddSingleton<StatusPushDispatcher>();
        builder.Services.AddHostedService<SetupCodeStartup>();

        builder.Services.AddControlPlaneSecurity(keyDirectory);
        builder.Services.AddExceptionHandler<ControlPlaneExceptionHandler>();
        builder.Services.AddLocalization();
        builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
        builder.Services.AddControllersWithViews(options => options.Filters.Add<GlobalAntiforgeryFilter>());

        var app = builder.Build();

        // US-040: first of all, so the no-store rule sees every response of the host (SC-14).
        app.UseMiddleware<NoStoreMiddleware>();
        // US-050: beside it, so the browser security headers reach every response of the host (SC-15).
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/error/500" });
        app.UseStatusCodePagesWithReExecute("/error/{0}");
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = NoStoreMiddleware.MarkStaticFile });
        app.UseRouting();
        app.UseMiddleware<SetupGateMiddleware>();
        app.UseAuthentication();
        app.UseRequestLocalization(options =>
        {
            CultureInfo[] cultures = [CultureInfo.GetCultureInfo("uk"), CultureInfo.GetCultureInfo("en")];
            options.DefaultRequestCulture = new RequestCulture("uk");
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.RequestCultureProviders = [new OwnerAccountCultureProvider()];
        });
        app.UseAuthorization();

        app.MapControllers();

        // SC-4 v66, US-041: an unmatched request, file-like or not, answers 404 to anyone; the explicit pattern drops the
        // default "nonfile" constraint of MapFallback.
        app.MapFallback("{*path}", context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            })
            .AllowAnonymous()
            .WithMetadata(new SetupGateExemptAttribute(), new UnmatchedRequestMetadata());

        return app;
    }

    /// <summary>
    /// As a service, the content root is the application folder — a service starts in the Windows system folder — and
    /// the host runs with the Windows service lifetime (US-047 FR-002, FR-003). Outside a service the builder is exactly
    /// <c>WebApplication.CreateBuilder(args)</c> (AC-004).
    /// </summary>
    /// <remarks>
    /// The lifetime is registered here rather than through the package's <c>AddWindowsService</c>: that helper
    /// re-checks the detection itself, and it also registers a Windows Event Log logging provider, which the
    /// application must not have (FR-007, I-4).
    /// </remarks>
    private static WebApplicationBuilder CreateBuilder(string[] args, bool runsAsService)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = runsAsService ? AppContext.BaseDirectory : null,
        });
        if (runsAsService && OperatingSystem.IsWindows())
        {
            builder.Services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
        }

        return builder;
    }

    /// <summary>
    /// US-047 FR-004, VR-001: an absolute directory is used as given; any other value — drive-relative
    /// (<c>C:logs</c>) and root-relative (<c>\logs</c>) ones included — drops its drive or root prefix and lands under
    /// the content root (I-5).
    /// </summary>
    private static string ResolveDirectory(string configured, string contentRoot)
    {
        if (Path.IsPathFullyQualified(configured))
        {
            return configured;
        }

        var relative = configured[Path.GetPathRoot(configured.AsSpan()).Length..];
        return Path.GetFullPath(Path.Join(contentRoot, relative));
    }

    private static string RequiredSetting(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"The Control Plane setting '{key}' is not configured.");
}
