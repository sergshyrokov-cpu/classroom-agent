using System.Net;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-011 AC-004: a failure says what is not configured. Each outcome of the closed list of spec FR-005 renders its
/// own translated message; a forgotten scope is named; run-wide causes stop early; a read waits for its own scope;
/// Owner-side causes reveal nothing about the key; a failed step is a result (200), not an error (spec FR-001,
/// FR-002, FR-003, FR-005, I-2, I-5; api-design §2.3).
/// </summary>
public sealed class AccessCheckFailureTests(PostgreSqlFixture database)
{
    private static readonly string CoursesScope = AccessCheckTestData.Scopes[0];
    private static readonly string ReportsScope = AccessCheckTestData.Scopes[5];
    private static readonly string RostersScope = AccessCheckTestData.Scopes[1];

    /// <summary>Spec §3.2: a forgotten scope is named, with its full URI, and only that step fails.</summary>
    [Fact]
    public async Task AForgottenScope_IsNamed_AndOnlyItsStepFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[RostersScope] = AccessCheckStepOutcome.ScopeNotAuthorized;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(AccessCheckTestData.Verdict.NotConfigured, AccessCheckHostExtensions.VerdictOf(page));
        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        var failed = Assert.Single(steps, s => s.Outcome != nameof(AccessCheckStepOutcome.Succeeded));
        Assert.Equal(RostersScope, failed.Scope);
        Assert.Equal(nameof(AccessCheckStepOutcome.ScopeNotAuthorized), failed.Outcome);
        Assert.Contains(RostersScope, failed.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.OutcomeScopeNotAuthorized, "uk"), failed.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.VerdictNotConfigured, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>Spec FR-003: a read runs only if its own scope's delegation succeeded; otherwise it is NotAttempted.</summary>
    [Fact]
    public async Task WhenTheCoursesScopeIsMissing_TheClassroomReadIsNotAttempted_ButTheReportsReadRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[CoursesScope] = AccessCheckStepOutcome.ScopeNotAuthorized;

        var page = await client.RunAccessCheckAsync(ct);

        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Equal(nameof(AccessCheckStepOutcome.NotAttempted), steps[6].Outcome);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.OutcomeNotAttempted, "uk"), steps[6].Text, StringComparison.Ordinal);
        Assert.Equal(nameof(AccessCheckStepOutcome.Succeeded), steps[7].Outcome);
        Assert.DoesNotContain(probe.RequestCalls, c => c.Kind == ProbeCallKind.Courses);
        Assert.Single(probe.RequestCalls, c => c.Kind == ProbeCallKind.MeetActivity);
    }

    [Fact]
    public async Task WhenTheReportsScopeIsMissing_TheReportsReadIsNotAttempted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[ReportsScope] = AccessCheckStepOutcome.ScopeNotAuthorized;

        var page = await client.RunAccessCheckAsync(ct);

        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Equal(nameof(AccessCheckStepOutcome.Succeeded), steps[6].Outcome);
        Assert.Equal(nameof(AccessCheckStepOutcome.NotAttempted), steps[7].Outcome);
        Assert.DoesNotContain(probe.RequestCalls, c => c.Kind == ProbeCallKind.MeetActivity);
    }

    public static TheoryData<AccessCheckStepOutcome> RunWideCauses => new(
        AccessCheckStepOutcome.KeyUnavailable,
        AccessCheckStepOutcome.KeyRejected,
        AccessCheckStepOutcome.TechnicalAccountUnknown);

    /// <summary>
    /// Spec FR-002, I-5: a run-wide cause found by the first delegation stops the run — one call, the cause on the
    /// first step, every other step NotAttempted, the verdict NotConfigured.
    /// </summary>
    [Theory]
    [MemberData(nameof(RunWideCauses))]
    public async Task ARunWideCause_StopsTheRunAfterTheFirstCall(AccessCheckStepOutcome cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        foreach (var scope in AccessCheckTestData.Scopes)
        {
            probe.Delegation[scope] = cause;
        }

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Single(probe.RequestCalls);
        Assert.Equal(AccessCheckTestData.Verdict.NotConfigured, AccessCheckHostExtensions.VerdictOf(page));
        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Equal(cause.ToString(), steps[0].Outcome);
        Assert.All(steps.Skip(1), s => Assert.Equal(nameof(AccessCheckStepOutcome.NotAttempted), s.Outcome));
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.Of(cause), "uk"), steps[0].Text, StringComparison.Ordinal);
    }

    public static TheoryData<AccessCheckStepOutcome> ReadCauses => new(
        AccessCheckStepOutcome.TechnicalAccountCannotRead,
        AccessCheckStepOutcome.ApiNotEnabled);

    /// <summary>Spec FR-005: the read-only causes of a read render their own message on that read's step.</summary>
    [Theory]
    [MemberData(nameof(ReadCauses))]
    public async Task AReadFailure_IsShownOnThatRead_WithItsMessage(AccessCheckStepOutcome cause)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.MeetActivity = cause;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(AccessCheckTestData.Verdict.NotConfigured, AccessCheckHostExtensions.VerdictOf(page));
        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Equal(cause.ToString(), steps[7].Outcome);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.Of(cause), "uk"), steps[7].Text, StringComparison.Ordinal);
        Assert.All(steps.Take(7), s => Assert.Equal(nameof(AccessCheckStepOutcome.Succeeded), s.Outcome));
    }

    /// <summary>Spec FR-001: with no configuration failure but Google not answering, the verdict is Inconclusive.</summary>
    [Fact]
    public async Task GoogleUnavailableAlone_MakesTheVerdictInconclusive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Courses = AccessCheckStepOutcome.GoogleUnavailable;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(AccessCheckTestData.Verdict.Inconclusive, AccessCheckHostExtensions.VerdictOf(page));
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.VerdictInconclusive, "uk"), page.Text, StringComparison.Ordinal);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.OutcomeGoogleUnavailable, "uk"), page.Text, StringComparison.Ordinal);
    }

    /// <summary>Spec FR-001: a configuration failure outranks an unavailable step — the verdict is NotConfigured.</summary>
    [Fact]
    public async Task AConfigurationFailure_OutranksGoogleUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Courses = AccessCheckStepOutcome.GoogleUnavailable;
        probe.MeetActivity = AccessCheckStepOutcome.TechnicalAccountCannotRead;

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(AccessCheckTestData.Verdict.NotConfigured, AccessCheckHostExtensions.VerdictOf(page));
    }

    /// <summary>Spec FR-005: GoogleUnavailable is not retried within a check (each call made once).</summary>
    [Fact]
    public async Task GoogleUnavailable_IsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[RostersScope] = AccessCheckStepOutcome.GoogleUnavailable;

        await client.RunAccessCheckAsync(ct);

        Assert.Single(probe.RequestCalls, c => c.Scope == RostersScope);
        Assert.Equal(8, probe.RequestCalls.Count);
    }

    /// <summary>S-09: a permission failure is not retried either.</summary>
    [Fact]
    public async Task APermissionFailure_IsNotRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.Delegation[RostersScope] = AccessCheckStepOutcome.ScopeNotAuthorized;
        probe.MeetActivity = AccessCheckStepOutcome.TechnicalAccountCannotRead;

        await client.RunAccessCheckAsync(ct);

        Assert.Single(probe.RequestCalls, c => c.Scope == RostersScope);
        Assert.Single(probe.RequestCalls, c => c.Kind == ProbeCallKind.MeetActivity);
    }

    /// <summary>
    /// Spec FR-005, S-07: an Owner-side message names the Owner and reveals nothing about the key — not its
    /// reference, not the setting that holds it.
    /// </summary>
    [Fact]
    public async Task AnOwnerSideCause_RevealsNothingAboutTheKey()
    {
        const string reference = "installation-google-service-account-key";
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct, keyReference: reference);
        await using var _host = host;
        foreach (var scope in AccessCheckTestData.Scopes)
        {
            probe.Delegation[scope] = AccessCheckStepOutcome.KeyUnavailable;
        }

        var page = await client.RunAccessCheckAsync(ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(host.Text(AccessCheckTestData.TextKeys.OutcomeKeyUnavailable, "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(reference, page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AccessCheckTestData.KeyReferenceSetting, page.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_key", page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Spec I-2: a run is bounded by 30 seconds whatever Google does. With Google never answering, advancing the
    /// installation's clock past the limit ends the run with GoogleUnavailable and the verdict Inconclusive.
    /// </summary>
    [Fact]
    public async Task WhenGoogleNeverAnswers_TheRunEndsAtTheTimeLimit_AsInconclusive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, probe) = await AccessCheckHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        probe.NeverAnswers = true;

        var running = client.RunAccessCheckAsync(ct);
        await probe.FirstCall.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);
        host.Time.Advance(TimeSpan.FromSeconds(31));
        var page = await running.WaitAsync(ManualTimeProvider.RealTimeLimit, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(AccessCheckTestData.Verdict.Inconclusive, AccessCheckHostExtensions.VerdictOf(page));
        var steps = AccessCheckHostExtensions.StepsOf(page);
        Assert.Equal(8, steps.Count);
        Assert.Equal(nameof(AccessCheckStepOutcome.GoogleUnavailable), steps[0].Outcome);
        Assert.All(steps, s => Assert.NotEqual(nameof(AccessCheckStepOutcome.Succeeded), s.Outcome));
    }
}
