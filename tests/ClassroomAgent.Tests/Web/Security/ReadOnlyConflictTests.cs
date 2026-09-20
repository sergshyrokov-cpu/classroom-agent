using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>
/// US-008 AC-012: a read-only refusal that reaches HTTP answers <c>409</c> — the API-6 body under
/// <c>/api/v1</c> and the error page elsewhere — with the reason named in the user's language. This closes
/// US-007 OD-001, so API-5 is satisfied end to end from this Story onwards (spec FR-014; S-19; API-5, API-6).
/// </summary>
public sealed class ReadOnlyConflictTests(PostgreSqlFixture database)
{
    public static TheoryData<LegitimacyModeReason, string> Reasons => new()
    {
        { LegitimacyModeReason.NotYetConfirmed, SignInTestData.TextKeys.RefusedNotYetConfirmed },
        { LegitimacyModeReason.SuspendedByOwner, SignInTestData.TextKeys.RefusedSuspendedByOwner },
        { LegitimacyModeReason.GracePeriodExpired, SignInTestData.TextKeys.RefusedGracePeriodExpired },
    };

    [Theory]
    [MemberData(nameof(Reasons))]
    public async Task UnderApiV1_TheRefusalIs409WithTheApi6Body(LegitimacyModeReason reason, string textKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        var lastCheck = reason == LegitimacyModeReason.NotYetConfirmed
            ? (DateTimeOffset?)null
            : InstallationTestHost.DefaultStart - TimeSpan.FromDays(8);

        var refusal = await ReadOnlyRefusalOverHttp.HandleAsync(
            host,
            "/api/v1/courses",
            reason,
            lastCheck,
            "uk",
            ct);

        Assert.True(refusal.Handled, "The host did not handle a read-only refusal reaching HTTP (spec FR-014).");
        Assert.Equal(409, refusal.StatusCode);
        var body = refusal.ApiError();
        Assert.Equal(
            new[] { "error", "message", "path", "status", "timestamp" },
            body.Keys.Where(k => k != "fieldErrors").Order(StringComparer.Ordinal));
        Assert.Equal(409, body["status"].GetInt32());
        Assert.Equal("/api/v1/courses", body["path"].GetString());
        Assert.Equal(host.Text(textKey, "uk"), body["message"].GetString());
    }

    /// <summary>AC-012, NFR-073: the message is in the requesting user's language (TC-3).</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheRefusalMessage_IsInTheRequestingUsersLanguage(string culture)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var refusal = await ReadOnlyRefusalOverHttp.HandleAsync(
            host,
            "/api/v1/courses",
            LegitimacyModeReason.SuspendedByOwner,
            InstallationTestHost.DefaultStart,
            culture,
            ct);

        Assert.Equal(409, refusal.StatusCode);
        Assert.Equal(
            host.Text(SignInTestData.TextKeys.RefusedSuspendedByOwner, culture),
            refusal.ApiError()["message"].GetString());
    }

    /// <summary>AC-012, NFR-073: the two languages differ, so neither is a copy of the other.</summary>
    [Fact]
    public async Task TheUkrainianAndEnglishMessages_Differ()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var ukrainian = await ReadOnlyRefusalOverHttp.HandleAsync(
            host, "/api/v1/courses", LegitimacyModeReason.GracePeriodExpired, InstallationTestHost.DefaultStart, "uk", ct);
        var english = await ReadOnlyRefusalOverHttp.HandleAsync(
            host, "/api/v1/courses", LegitimacyModeReason.GracePeriodExpired, InstallationTestHost.DefaultStart, "en", ct);

        Assert.NotEqual(
            ukrainian.ApiError()["message"].GetString(),
            english.ApiError()["message"].GetString());
    }

    /// <summary>AC-012: outside /api/v1 the same refusal is 409 as well — the error page carries the reason.</summary>
    [Fact]
    public async Task OutsideApiV1_TheRefusalIsAlso409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var refusal = await ReadOnlyRefusalOverHttp.HandleAsync(
            host,
            "/courses/sync",
            LegitimacyModeReason.SuspendedByOwner,
            InstallationTestHost.DefaultStart,
            "uk",
            ct);

        Assert.True(refusal.Handled, "The host did not handle a read-only refusal on a page path (spec FR-014).");
        Assert.Equal(409, refusal.StatusCode);
    }

    /// <summary>AC-012, S-19: the refusal body leaks no internals — no stack trace, SQL, type name or path.</summary>
    [Fact]
    public async Task TheRefusalBody_LeaksNoInternals()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var refusal = await ReadOnlyRefusalOverHttp.HandleAsync(
            host,
            "/api/v1/courses",
            LegitimacyModeReason.GracePeriodExpired,
            InstallationTestHost.DefaultStart,
            "uk",
            ct);

        Assert.DoesNotContain("ReadOnlyModeException", refusal.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClassroomAgent.", refusal.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", refusal.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", refusal.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("D:\\", refusal.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("test.probe", refusal.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-012, API-6: the body is JSON under /api/v1, not HTML.</summary>
    [Fact]
    public async Task UnderApiV1_TheBodyIsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var refusal = await ReadOnlyRefusalOverHttp.HandleAsync(
            host,
            "/api/v1/courses",
            LegitimacyModeReason.NotYetConfirmed,
            null,
            "uk",
            ct);

        Assert.NotNull(refusal.ContentType);
        Assert.StartsWith("application/json", refusal.ContentType!, StringComparison.OrdinalIgnoreCase);
    }
}
