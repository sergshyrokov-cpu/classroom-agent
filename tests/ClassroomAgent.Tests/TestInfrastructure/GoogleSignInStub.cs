using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the installation's <em>real</em> Google authentication handler without a network
/// (US-008 test strategy 2.2; TC-4). The handler keeps its own behaviour — the OAuth <c>state</c>
/// parameter, the correlation cookie, the code exchange — which is exactly what AC-003 asks to be proven;
/// only its three endpoints and its backchannel are replaced by local, scripted ones.
/// </summary>
/// <remarks>
/// The options type is resolved <b>by name</b>, so the test project keeps no reference to
/// <c>Microsoft.AspNetCore.Authentication.Google</c>: OD-003 added that package to
/// <c>ClassroomAgent.Web</c> and to no other project. This is the only place in the suite that uses
/// reflection; every assertion elsewhere is ordinary behaviour. Before the handler exists,
/// <see cref="Apply"/> fails with a message naming the missing production behaviour.
/// </remarks>
public sealed class GoogleSignInStub
{
    public const string SchemeName = "Google";

    /// <summary>Local stand-ins for Google's endpoints; no request ever leaves the process.</summary>
    public const string AuthorizationEndpoint = "https://accounts.google.test/o/oauth2/v2/auth";

    public const string TokenEndpoint = "https://oauth2.googleapis.test/token";

    public const string UserInformationEndpoint = "https://openidconnect.googleapis.test/v1/userinfo";

    private const string OptionsTypeName =
        "Microsoft.AspNetCore.Authentication.Google.GoogleOptions, Microsoft.AspNetCore.Authentication.Google";

    private const string MissingHandlerMessage =
        "The Google authentication handler is not part of the host yet: "
        + "'Microsoft.AspNetCore.Authentication.Google' is not referenced by ClassroomAgent.Web "
        + "(spec FR-006, FR-021; OD-003).";

    public GoogleSignInStub() => Backchannel = new ScriptedHttpHandler(RespondAsync);

    /// <summary>The address Google reports for the signed-in account. Mixed case is deliberate in some tests (BR-079).</summary>
    public string Email { get; set; } = SignInTestData.AdminEmail;

    /// <summary>Google's <c>email_verified</c> flag; false must refuse the callback (VR-005, spec I-6).</summary>
    public bool EmailVerified { get; set; } = true;

    /// <summary>When false the userinfo document carries no <c>email</c> property at all (VR-005).</summary>
    public bool IncludeEmail { get; set; } = true;

    /// <summary>Google's stable subject identifier. Nothing may store it (S-09).</summary>
    public string Subject { get; set; } = "109876543210987654321";

    public string DisplayName { get; set; } = "Ivan Petrenko";

    /// <summary>Every request the handler made to Google — asserted empty where no exchange may happen.</summary>
    public ScriptedHttpHandler Backchannel { get; }

    /// <summary>Points the started host's Google handler at this stub. Fails when the handler is not configured.</summary>
    public void Apply(IServiceProvider services)
    {
        var optionsType = Type.GetType(OptionsTypeName);
        if (optionsType is null)
        {
            Assert.Fail(MissingHandlerMessage);
        }

        var monitorType = typeof(IOptionsMonitor<>).MakeGenericType(optionsType);
        var monitor = services.GetService(monitorType);
        Assert.NotNull(monitor);

        var options = monitorType.GetMethod("Get")!.Invoke(monitor, [SchemeName]);
        Assert.NotNull(options);

        Set(options, "AuthorizationEndpoint", AuthorizationEndpoint);
        Set(options, "TokenEndpoint", TokenEndpoint);
        Set(options, "UserInformationEndpoint", UserInformationEndpoint);
        Set(options, "Backchannel", new HttpClient(Backchannel));
    }

    /// <summary>The client id the host sent to Google, read back from the handler's options.</summary>
    public static string ClientIdOf(IServiceProvider services) => OptionProperty(services, "ClientId");

    /// <summary>
    /// The client secret the host resolved out of the secret store, read back from the handler's options
    /// (S-08; OD-004: the reference names an environment variable).
    /// </summary>
    public static string ClientSecretOf(IServiceProvider services) => OptionProperty(services, "ClientSecret");

    private static string OptionProperty(IServiceProvider services, string property)
    {
        var options = Options(services);
        var value = options.GetType().GetProperty(property)!.GetValue(options);
        Assert.NotNull(value);
        return (string)value;
    }

    private static object Options(IServiceProvider services)
    {
        var optionsType = Type.GetType(OptionsTypeName);
        if (optionsType is null)
        {
            Assert.Fail(MissingHandlerMessage);
        }

        var monitorType = typeof(IOptionsMonitor<>).MakeGenericType(optionsType);
        var monitor = services.GetService(monitorType);
        Assert.NotNull(monitor);
        var options = monitorType.GetMethod("Get")!.Invoke(monitor, [SchemeName]);
        Assert.NotNull(options);
        return options;
    }

    private static void Set(object target, string property, object value)
    {
        var info = target.GetType().GetProperty(property);
        Assert.NotNull(info);
        info.SetValue(target, value);
    }

    private Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? string.Empty;
        if (uri == TokenEndpoint)
        {
            return Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, TokenJson()));
        }

        if (uri == UserInformationEndpoint)
        {
            return Task.FromResult(ScriptedHttpHandler.JsonResponse(HttpStatusCode.OK, UserInformationJson()));
        }

        Assert.Fail($"The Google handler called an endpoint no test scripted: {uri}.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private static string TokenJson() =>
        JsonSerializer.Serialize(new
        {
            access_token = "synthetic-access-token",
            token_type = "Bearer",
            expires_in = 3599,
        });

    private string UserInformationJson()
    {
        var document = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = Subject,
            ["name"] = DisplayName,
            ["email_verified"] = EmailVerified,
        };
        if (IncludeEmail)
        {
            document["email"] = Email;
        }

        return JsonSerializer.Serialize(document);
    }
}
