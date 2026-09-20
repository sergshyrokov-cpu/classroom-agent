using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the installation sign-in over HTTP (US-008 openapi <c>/sign-in/google</c> and
/// <c>/signin-google</c>): open the page for a token, POST the start, read <c>state</c> out of the
/// redirect to Google, then return through the callback with the correlation cookie the client kept.
/// </summary>
public static class InstallationSignInExtensions
{
    public const string AuthorizationCode = "synthetic-authorization-code";

    /// <summary>Opens the sign-in page and posts the start form with its antiforgery token.</summary>
    public static async Task<PageResponse> StartGoogleSignInAsync(
        this FormClient client,
        CancellationToken cancellationToken,
        bool withToken = true)
    {
        await client.GetAsync(SignInTestData.SignInPath, cancellationToken);
        return await client.PostFormAsync(SignInTestData.StartPath, [], cancellationToken, withToken);
    }

    /// <summary>A whole successful-shaped sign-in: start, then the callback with the <c>state</c> Google echoes back.</summary>
    public static async Task<(FormClient Client, PageResponse Callback)> SignInWithGoogleAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        FormClient? client = null)
    {
        host.UseGoogleStub();
        var browser = client ?? host.CreateClient();
        var start = await browser.StartGoogleSignInAsync(cancellationToken);
        Assert.True(
            start.Status == HttpStatusCode.Redirect,
            $"Expected the start to redirect to Google, got {(int)start.Status}.");
        var callback = await browser.CompleteGoogleCallbackAsync(StateOf(start), cancellationToken);
        return (browser, callback);
    }

    /// <summary>Calls the callback with a given <c>state</c> and the code the stub accepts.</summary>
    public static Task<PageResponse> CompleteGoogleCallbackAsync(
        this FormClient client,
        string? state,
        CancellationToken cancellationToken,
        string? code = AuthorizationCode)
    {
        var query = new List<string>();
        if (code is not null)
        {
            query.Add("code=" + Uri.EscapeDataString(code));
        }

        if (state is not null)
        {
            query.Add("state=" + Uri.EscapeDataString(state));
        }

        var path = SignInTestData.CallbackPath + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));
        return client.GetAsync(path, cancellationToken);
    }

    /// <summary>The <c>state</c> parameter of the redirect to Google; fails when the redirect carries none.</summary>
    public static string StateOf(PageResponse start)
    {
        var state = QueryOf(start).TryGetValue("state", out var value) ? value : null;
        Assert.False(
            string.IsNullOrEmpty(state),
            $"The authorization request carries no 'state' parameter: {start.Location}");
        return state!;
    }

    /// <summary>The query parameters of the redirect to Google, decoded.</summary>
    public static IReadOnlyDictionary<string, string> QueryOf(PageResponse response)
    {
        Assert.NotNull(response.Location);
        var query = new Uri(response.Location!, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(response.Location!).Query
            : response.Location![response.Location!.IndexOf('?', StringComparison.Ordinal)..];
        return QueryHelpers.ParseQuery(query)
            .ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);
    }

    /// <summary>The names of the cookies a response set, for the correlation-cookie assertions.</summary>
    public static IReadOnlyList<string> SetCookieNames(PageResponse response) =>
        response.SetCookies
            .Select(header => header[..header.IndexOf('=', StringComparison.Ordinal)])
            .ToList();
}
