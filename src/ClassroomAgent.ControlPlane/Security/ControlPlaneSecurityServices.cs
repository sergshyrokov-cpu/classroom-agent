using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace ClassroomAgent.ControlPlane.Security;

/// <summary>
/// The Control Plane's security baseline: cookie authentication for the Owner, the
/// deny-by-default fallback policy, antiforgery cookie, and the persisted Data Protection
/// key ring (FR-010, FR-014, FR-015, FR-017; SC-2, SC-4, SC-7).
/// </summary>
public static class ControlPlaneSecurityServices
{
    public const string SessionCookieName = "__Host-cp-session";

    public const string AntiforgeryCookieName = "__Host-cp-antiforgery";

    public static IServiceCollection AddControlPlaneSecurity(this IServiceCollection services, string keyDirectory)
    {
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("ClassroomAgent.ControlPlane")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));

        // US-049 FR-001, FR-002: every key is written encrypted with DPAPI bound to the Windows account the host runs
        // under, never to the machine (VR-001). Off Windows the host refuses to start before this point (FR-005); the
        // throw keeps any other path from writing a plain key.
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(Program.NotOnWindowsReason);
        }

        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: false);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = OwnerSession.IdleTimeout;
                options.SlidingExpiration = true;
                options.LoginPath = "/sign-in";
                options.AccessDeniedPath = "/error/403";
                options.Events = new CookieAuthenticationEvents
                {
                    // No return URL is emitted or honoured (spec I-6).
                    OnRedirectToLogin = context =>
                    {
                        context.Response.Redirect("/sign-in");
                        return Task.CompletedTask;
                    },

                    // A denied signed-in request gets 403 and the error page, not a redirect (SC-4 v66).
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    },
                    OnValidatePrincipal = OwnerSession.ValidatePrincipalAsync,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(OwnerSession.OwnerPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(OwnerSession.OwnerRole));
            options.FallbackPolicy = options.GetPolicy(OwnerSession.OwnerPolicy);
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        return services;
    }
}
