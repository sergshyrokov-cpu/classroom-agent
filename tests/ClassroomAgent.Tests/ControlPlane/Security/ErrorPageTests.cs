using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.ControlPlane.Security;

/// <summary>AC-010: the error page, the 404 catch-all and 500 without internals (FR-016, SC-10).</summary>
public sealed class ErrorPageTests(PostgreSqlFixture database)
{
    private static readonly string[] Internals =
    [
        "Exception",
        "   at ",
        "Npgsql",
        "ClassroomAgent.",
        "Microsoft.",
        "SELECT ",
        "INSERT ",
        "relation",
        ".cs:line",
        @":\",
    ];

    [Theory]
    [InlineData(400, HttpStatusCode.BadRequest, "Error.PageExpired")]
    [InlineData(403, HttpStatusCode.Forbidden, "Error.Forbidden")]
    [InlineData(404, HttpStatusCode.NotFound, "Error.NotFound")]
    [InlineData(500, HttpStatusCode.InternalServerError, "Error.Internal")]
    [InlineData(418, HttpStatusCode.NotFound, "Error.NotFound")]
    public async Task ErrorPage_KnownAndUnknownCodes(int code, HttpStatusCode expectedStatus, string expectedKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();

        var response = await anonymous.GetAsync($"/error/{code}", ct);

        Assert.Equal(expectedStatus, response.Status);
        Assert.Null(response.Location);
        Assert.Contains(host.Text(expectedKey, "uk"), response.Text, StringComparison.Ordinal);
        AssertNoInternals(response);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnmatchedRequest_AnyMethod_Returns404(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        using var owner = await host.CreateOwnerAsync(ct);
        using var anonymous = host.CreateClient();
        var notFound = host.Text("Error.NotFound", "uk");

        var asAnonymous = await anonymous.SendAsync(new HttpMethod(method), "/nope", null, ct);
        var asOwner = await owner.SendAsync(new HttpMethod(method), "/nope/deeper", null, ct);

        Assert.Equal(HttpStatusCode.NotFound, asAnonymous.Status);
        Assert.Null(asAnonymous.Location);
        Assert.Equal(HttpStatusCode.NotFound, asOwner.Status);
        Assert.Null(asOwner.Location);
        if (method != "HEAD")
        {
            Assert.Contains(notFound, asAnonymous.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UnhandledException_ShowsErrorPageWithoutInternals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ControlPlaneTestHost.StartAsync(database, ct);
        var internalError = host.Text("Error.Internal", "uk");
        using var client = host.CreateClient();
        await client.GetAsync("/setup", ct);

        // Breaking the schema under the running host turns the next database access into an unhandled exception.
        await host.ExecuteAsync("DROP TABLE owner CASCADE", ct);
        var response = await client.PostFormAsync("/setup", TestData.SetupFields(), ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.Status);
        Assert.Contains(internalError, response.Text, StringComparison.Ordinal);
        AssertNoInternals(response);
    }

    private static void AssertNoInternals(PageResponse response)
    {
        foreach (var fragment in Internals)
        {
            Assert.DoesNotContain(fragment, response.Text, StringComparison.Ordinal);
        }
    }
}
