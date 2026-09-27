using System.Security.Claims;
using System.Text.Json;
using ClassroomAgent.Application.Authorization;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The installation's security baseline (US-008 spec FR-002, FR-004, FR-005, FR-006, FR-015): the persisted Data
/// Protection key ring, cookie authentication for the session, Google OAuth as the Admin's external login, the
/// deny-by-default fallback policy and the antiforgery cookie. Written once here and reused by every later screen.
/// </summary>
public static class InstallationSecurityServices
{
    /// <summary>The Google scheme name; the handler's own default, kept explicit because the tests address it.</summary>
    public const string GoogleScheme = GoogleDefaults.AuthenticationScheme;

    public static IServiceCollection AddInstallationSecurity(
        this IServiceCollection services,
        InstallationSettings settings)
    {
        services.AddDataProtection()
            .SetApplicationName("ClassroomAgent.Installation")
            .PersistKeysToFileSystem(new DirectoryInfo(settings.DataProtectionKeyDirectory));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(ConfigureSessionCookie)
            .AddGoogle(options => ConfigureGoogle(options, settings));

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                InstallationPolicies.ViewLegitimacyStatus,
                policy => policy.RequireAuthenticatedUser().RequireRole(
                    InstallationSession.RoleName(AppRole.Admin),
                    InstallationSession.RoleName(AppRole.Dean)));
            options.AddPolicy(
                InstallationPolicies.AuthenticatedUser,
                policy => policy.RequireAuthenticatedUser());

            // US-009 spec FR-010: the connection settings are the Admin's cell of the §2 matrix, and no other
            // cell is implemented speculatively (SC-1).
            options.AddPolicy(
                InstallationPolicies.ConfigureWorkspaceConnection,
                policy => policy.RequireAuthenticatedUser().RequireRole(
                    InstallationSession.RoleName(AppRole.Admin)));

            // US-010 spec FR-011: its own cell of the §2 matrix, not US-009's. The two rows are separate in the
            // requirements because read-only mode blocks one and permits the other (spec I-7).
            options.AddPolicy(
                InstallationPolicies.ViewConnectionInstruction,
                policy => policy.RequireAuthenticatedUser().RequireRole(
                    InstallationSession.RoleName(AppRole.Admin)));

            // US-011 spec FR-011: "Проверить доступ" is its own cell of the §2 matrix (spec I-9).
            options.AddPolicy(
                InstallationPolicies.RunAccessCheck,
                policy => policy.RequireAuthenticatedUser().RequireRole(
                    InstallationSession.RoleName(AppRole.Admin)));

            // US-012 spec FR-016: managing Dean accounts is the Admin's cell of the §2 matrix (v64).
            options.AddPolicy(
                InstallationPolicies.ManageDeanAccounts,
                policy => policy.RequireAuthenticatedUser().RequireRole(
                    InstallationSession.RoleName(AppRole.Admin)));

            // US-012 spec FR-016: changing one's own password is the Dean's cell — an Admin has no password
            // at all, so this policy refuses one (SC-2, spec S-07).
            options.AddPolicy(
                InstallationPolicies.ChangeOwnPassword,
                policy => policy.RequireAuthenticatedUser().RequireRole(
                    InstallationSession.RoleName(AppRole.Dean)));

            // US-012 spec FR-006: only the session created at step 5 of the sequence may reach the forced
            // change form (api-design §2.6).
            options.AddPolicy(
                InstallationPolicies.CompleteTemporaryPasswordChange,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(InstallationClaimTypes.PasswordIsTemporary, "true"));

            // Deny by default: an endpoint whose author forgot an attribute closes rather than opens (SC-4, API-9).
            options.FallbackPolicy = options.GetPolicy(InstallationPolicies.AuthenticatedUser);
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = InstallationSession.AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });

        return services;
    }

    /// <summary>
    /// SC-2 fixes these per host: <c>SameSite=Lax</c> is required, not preferred — <c>Strict</c> would leave the
    /// first page after the return from Google without a session (<c>trebovaniya.md</c> §8, v64).
    /// </summary>
    private static void ConfigureSessionCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = InstallationSession.SessionCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = InstallationSession.IdleTimeout;
        options.SlidingExpiration = true;
        options.LoginPath = SignInRoutes.SignInPage;
        options.AccessDeniedPath = "/error/403";
        options.Events = new CookieAuthenticationEvents
        {
            // No return URL is emitted or honoured: it is an open-redirect surface for no gain (api-design §2.6).
            OnRedirectToLogin = context =>
            {
                context.Response.Redirect(SignInRoutes.SignInPage);
                return Task.CompletedTask;
            },

            // A denied signed-in request gets 403 with the error page, not a redirect (SC-4 v66).
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            OnValidatePrincipal = InstallationSession.ValidatePrincipalAsync,
        };
    }

    /// <summary>
    /// The Admin's external login (spec FR-006, FR-021; OD-002, OD-003). The scope set is stated explicitly rather
    /// than inherited, so a future package default cannot silently widen what a school's Admin consents to (S-06).
    /// </summary>
    private static void ConfigureGoogle(GoogleOptions options, InstallationSettings settings)
    {
        options.ClientId = settings.OAuthClientId;
        options.ClientSecret = settings.OAuthClientSecret;
        options.CallbackPath = SignInRoutes.Callback;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        // Identity scopes only. A Classroom or Reports scope here is a Critical finding (NFR-021, SC-8, S-06).
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");

        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SaveTokens = false;

        options.Events = new OAuthEvents
        {
            OnTicketReceived = GoogleSignInEvents.OnTicketReceivedAsync,
            OnRemoteFailure = GoogleSignInEvents.OnRemoteFailureAsync,
            OnCreatingTicket = context =>
            {
                // Google does not map email_verified by default; the decision needs it (VR-005, spec I-6).
                if (context.User.TryGetProperty("email_verified", out var verified)
                    && verified.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && context.Identity is { } identity)
                {
                    identity.AddClaim(new Claim(
                        InstallationClaimTypes.EmailVerified,
                        verified.GetBoolean() ? "true" : "false"));
                }

                return Task.CompletedTask;
            },

            // OD-002: the account-picker hint carries the school's domain when LegitimacyState knows it, and none
            // while no legitimacy check has ever succeeded. It is never the access decision (S-07).
            OnRedirectToAuthorizationEndpoint = async context =>
            {
                var domain = await SignInRoutes.KnownDomainAsync(context.HttpContext);
                context.Response.Redirect(
                    domain is null
                        ? context.RedirectUri
                        : QueryHelpers.AddQueryString(context.RedirectUri, "hd", domain));
            },
        };
    }
}
