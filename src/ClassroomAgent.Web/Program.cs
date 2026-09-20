using System.Globalization;
using System.Resources;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Secrets;
using ClassroomAgent.Web.Configuration;
using ClassroomAgent.Web.Exceptions;
using ClassroomAgent.Web.Security;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.WebEncoders;
using Serilog;

[assembly: NeutralResourcesLanguage("uk", UltimateResourceFallbackLocation.Satellite)]

namespace ClassroomAgent.Web;

/// <summary>
/// The installation host (US-005 spec FR-014, US-008 spec FR-002). A named class in a namespace, not top-level
/// statements, so the test project can reference both hosts without two global <c>Program</c> types. The host starts
/// only with valid settings (FR-001), serves liveness, readiness and the status push on the private port, serves the
/// public-port surface of US-008, and never applies migrations (PC-2).
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

        // OD-004: the OAuth client secret is resolved from the configured store before anything else needs it.
        ISecretStore secretStore = new EnvironmentSecretStore();

        InstallationSettings settings;
        try
        {
            settings = InstallationSettingsReader.Read(builder.Configuration, secretStore);
        }
        catch (InstallationSettingException exception)
        {
            InstallationLogging.WriteStartupRefusal(logDirectory, exception, isDevelopment);
            throw;
        }

        builder.Services.AddSerilog(
            (_, logger) => InstallationLogging.Configure(logger, logDirectory, isDevelopment),
            preserveStaticLogger: true);

        // A second endpoint for the private port, plain HTTP (DC-6), bound to the configured address only
        // (US-006 spec FR-001); the public endpoints stay as configured.
        builder.WebHost.UseUrls(
        [
            .. InstallationSettingsReader.PublicUrls(builder.Configuration),
            string.Create(CultureInfo.InvariantCulture, $"http://{settings.PrivateAddress}:{settings.PrivatePort}"),
        ]);

        builder.Services.AddSingleton(secretStore);
        builder.Services.AddInstallation(settings);
        builder.Services.AddInstallationSecurity(settings);
        builder.Services.AddExceptionHandler<InstallationExceptionHandler>();
        builder.Services.AddLocalization();
        builder.Services.Configure<WebEncoderOptions>(
            options => options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
        builder.Services.AddControllersWithViews(options => options.Filters.Add<GlobalAntiforgeryFilter>());

        // SC-2 v64: every cookie this host sets is Secure and httpOnly. The TempData cookie carries the refusal
        // category from the callback to the sign-in page (api-design §2.2), protected by the key ring of FR-001.
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
        });

        // The public port redirects HTTP to HTTPS on the configured address's port, never a guessed one (SC-2).
        builder.Services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
            options.HttpsPort = settings.PublicBaseAddress.IsDefaultPort ? 443 : settings.PublicBaseAddress.Port;
        });

        var app = builder.Build();

        // The pipeline order of spec FR-002; several of its guarantees depend on it.
        app.UseMiddleware<PublicPortMiddleware>(settings.PrivatePort);

        // Everything in this branch is the public port's alone. The private port keeps exactly what US-005 and
        // US-006 gave it: plain HTTP, no HSTS, no redirection, no error page — HTTPS rules applied to that port
        // would break the status push (SC-2, SC-9, DC-6).
        app.UseWhen(
            context => context.Connection.LocalPort != settings.PrivatePort,
            publicPort =>
            {
                if (!isDevelopment)
                {
                    publicPort.UseHsts();
                }

                publicPort.UseHttpsRedirection();
                publicPort.UseMiddleware<PublicBaseAddressMiddleware>(settings.PublicBaseAddress);
                publicPort.UseMiddleware<CallbackMethodMiddleware>();
                publicPort.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/error/500" });
                publicPort.UseStatusCodePagesWithReExecute("/error/{0}");
            });

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseRequestLocalization(options =>
        {
            CultureInfo[] cultures = [CultureInfo.GetCultureInfo("uk"), CultureInfo.GetCultureInfo("en")];
            var schoolDefault = InstallationSession.LanguageCode(settings.DefaultUiLanguage);
            options.DefaultRequestCulture = new RequestCulture(schoolDefault);
            options.SupportedCultures = cultures;
            options.SupportedUICultures = cultures;
            options.RequestCultureProviders = [new AccountCultureProvider(schoolDefault)];
        });
        app.UseAuthorization();

        app.MapPrivateEndpoints(settings.PrivatePort);
        app.MapControllers();

        // SC-4 v66: an unmatched request answers 404 to anyone. Without a matched endpoint the fallback policy would
        // challenge an anonymous request and send it to sign-in; the re-execution turns this into the error page.
        app.MapFallback(context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            })
            .AllowAnonymous();

        app.Run();
    }
}
