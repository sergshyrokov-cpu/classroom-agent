using System.Globalization;
using System.Resources;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ClassroomAgent.ControlPlane.Persistence;
using ClassroomAgent.ControlPlane.Security;
using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.WebEncoders;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

[assembly: NeutralResourcesLanguage("uk", UltimateResourceFallbackLocation.Satellite)]

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var connectionString = RequiredSetting(configuration, "ConnectionStrings:ControlPlane");
var keyDirectory = RequiredSetting(configuration, "DataProtection:KeyDirectory");
var logDirectory = RequiredSetting(configuration, "LogFile:Directory");
var compatibilityPolicy = CompatibilityPolicy.FromConfiguration(configuration);

// DC-10: one JSON object per line, a file per day, 30 days, size cap; request id from the log scope.
builder.Services.AddSerilog(
    (services, logger) =>
    {
        logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
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
builder.Services.AddHostedService<SetupCodeStartup>();

builder.Services.AddControlPlaneSecurity(keyDirectory);
builder.Services.AddExceptionHandler<ControlPlaneExceptionHandler>();
builder.Services.AddLocalization();
builder.Services.Configure<WebEncoderOptions>(options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddControllersWithViews(options => options.Filters.Add<GlobalAntiforgeryFilter>());

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/error/500" });
app.UseStatusCodePagesWithReExecute("/error/{0}");
app.UseStaticFiles();
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
app.MapFallback(context =>
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    })
    .AllowAnonymous()
    .WithMetadata(new SetupGateExemptAttribute());

app.Run();

static string RequiredSetting(IConfiguration configuration, string key) =>
    configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"The Control Plane setting '{key}' is not configured.");

/// <summary>Entry point, exposed to <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
