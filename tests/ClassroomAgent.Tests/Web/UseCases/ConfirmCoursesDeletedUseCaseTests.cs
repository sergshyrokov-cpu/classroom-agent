using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using static ClassroomAgent.Tests.TestInfrastructure.DataHealthHostExtensions;
using Cause = ClassroomAgent.Tests.TestInfrastructure.ReadOnlyModeHost.Cause;
using Dtos = ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-062 FR-006, VR-001, VR-002, AC-010, AC-011, AC-017 in the Application layer (TC-1, TC-5: read-only mode is proven
/// on the use case, not on a hidden button): <see cref="ConfirmCoursesDeletedUseCase"/> resolved from a running
/// installation over real PostgreSQL (TC-2) — the read-only guard first (with the refused audit row of api-design §2.5),
/// then the shape, then one transaction that is all-or-nothing (I-5). Every course is synthetic (TC-4).
/// </summary>
public sealed class ConfirmCoursesDeletedUseCaseTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset D1 = new(2026, 9, 10, 21, 30, 0, TimeSpan.Zero);

    private sealed record World(InstallationTestHost Host, long AdminId, long First, long Second, long Archived);

    /// <summary>The data of AC-009: three courses "not visible to the technical account", the third archived.</summary>
    private static async Task<World> StartAsync(PostgreSqlFixture database, CancellationToken ct, Cause cause = Cause.NotReadOnly)
    {
        var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        await host.ReadyToSeedAsync(ct);
        await host.RecentSuccessAsync(ct);
        await host.InsertAppUserAsync(ct);
        var admin = await host.AccountIdAsync(SignInTestData.AdminEmail, ct);
        var first = await host.CourseAsync(1, "Test Hidden One", ct);
        var second = await host.CourseAsync(2, "Test Hidden Two", ct);
        var archived = await host.CourseAsync(3, "Test Hidden Archived", ct, state: CourseTestData.States.Archived);
        foreach (var id in new[] { first, second, archived })
        {
            await host.MarkCourseAsync(id, "not_visible_to_technical_account", D1, ct);
        }

        return new World(host, admin, first, second, archived);
    }

    private static async Task<CourseDeletionConfirmationResult> ConfirmAsync(World world, CancellationToken ct, params string[]? ids)
    {
        using var scope = world.Host.CreateScope();
        var form = new CourseDeletionConfirmationForm { CourseIds = ids };
        return await scope.ServiceProvider.GetRequiredService<ConfirmCoursesDeletedUseCase>()
            .ExecuteAsync(world.AdminId, form, "request-62", ct);
    }

    private static string Id(long id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// AC-010, FR-006.2 steps 4 and 5, FR-006.4: two of the three confirmed — both "not in Classroom" since the
    /// confirmation, one succeeded audit row by the Admin with exactly their two ids and no name, the third untouched.
    /// </summary>
    [Fact]
    public async Task ConfirmingTwo_MarksThemNotInClassroom_AuditsTheirIds_AndLeavesTheThird()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct);
        await using var _host = world.Host;
        var now = world.Host.Time.GetUtcNow();

        var result = await ConfirmAsync(world, ct, Id(world.Second), Id(world.Archived));

        Assert.Equal(new CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome.Confirmed, 2), result);
        Assert.Equal(("not_in_classroom", (DateTimeOffset?)now), await world.Host.MarkOfAsync(world.Second, ct));
        Assert.Equal(("not_in_classroom", (DateTimeOffset?)now), await world.Host.MarkOfAsync(world.Archived, ct));
        Assert.Equal(("not_visible_to_technical_account", (DateTimeOffset?)D1), await world.Host.MarkOfAsync(world.First, ct));
        var row = Assert.Single(await world.Host.AuditRowsAsync(ct));
        Assert.Equal(
            ("courses_confirmed_deleted", "succeeded", "app_user", (long?)world.AdminId, "admin", (string?)null, (long?)null),
            (row.Action, row.Outcome, row.ActorType, row.ActorId, row.ActorRole, row.TargetType, row.TargetId));
        Assert.Equal("request-62", row.RequestId);
        Assert.Equal([new[] { world.Second, world.Archived }.Order().ToArray()], await world.Host.ConfirmedIdsAsync(ct));
        Assert.All(
            await world.Host.AuditRowsAsJsonAsync(ct),
            json => Assert.DoesNotContain("Test Hidden", json, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC-010, FR-006.3: after the confirmation the active course counts as a teaching problem T4 and the archived one is
    /// listed apart; X5 stays for the third course only.
    /// </summary>
    [Fact]
    public async Task ConfirmedCourses_CountAsNotInClassroom_UnlessArchived()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct);
        await using var _host = world.Host;

        await ConfirmAsync(world, ct, Id(world.Second), Id(world.Archived));

        using var scope = world.Host.CreateScope();
        var counts = await scope.ServiceProvider.GetRequiredService<GetDataHealthCountsQuery>().ExecuteAsync(AppRole.Admin, ct);
        var page = await scope.ServiceProvider.GetRequiredService<GetDataHealthQuery>().ExecuteAsync(AppRole.Admin, ct);
        Assert.Equal(new Dtos.DataHealthBannerModel(1, 1), counts);
        Assert.NotNull(Assert.Single(page.Teaching, c => c.Course.CourseId == world.Second).NotInClassroomSince);
        Assert.NotNull(Assert.Single(page.Archived, c => c.Course.CourseId == world.Archived).NotInClassroomSince);
        var x5 = Assert.Single(page.Technical);
        Assert.Equal([world.First], x5.Details!.NotVisibleCourses!.Select(c => c.CourseId));
    }

    /// <summary>VR-001, I-6: duplicates are removed before counting; the audit row holds each id once.</summary>
    [Fact]
    public async Task DuplicateIds_AreConfirmedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct);
        await using var _host = world.Host;

        var result = await ConfirmAsync(world, ct, Id(world.First), Id(world.First), Id(world.Second));

        Assert.Equal(new CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome.Confirmed, 2), result);
        Assert.Equal([new[] { world.First, world.Second }.Order().ToArray()], await world.Host.ConfirmedIdsAsync(ct));
    }

    /// <summary>
    /// AC-011, FR-006.2 step 1, AD-6, api-design §2.4 and §2.5: in read-only mode, for every cause, the use case refuses
    /// with the read-only refusal before anything else — even for a malformed form — no mark changes and exactly one
    /// refused audit row is written, without course ids.
    /// </summary>
    [Theory]
    [InlineData(Cause.NeverConfirmed, false)]
    [InlineData(Cause.Suspended, false)]
    [InlineData(Cause.GracePeriodExpired, false)]
    [InlineData(Cause.Suspended, true)]
    public async Task InReadOnlyMode_TheConfirmationIsRefused_AndOnlyTheRefusalIsAudited(Cause cause, bool malformed)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct, cause);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);

        await Assert.ThrowsAsync<ReadOnlyModeException>(
            () => malformed ? ConfirmAsync(world, ct, "not-a-number") : ConfirmAsync(world, ct, Id(world.First)));

        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        var row = Assert.Single(await world.Host.AuditRowsAsync(ct));
        Assert.Equal(
            ("courses_confirmed_deleted", "refused", "read_only_mode", (long?)world.AdminId, "admin"),
            (row.Action, row.Outcome, row.RefusalCategory, row.ActorId, row.ActorRole));
        Assert.Equal([(long[]?)null], await world.Host.ConfirmedIdsAsync(ct));
        Assert.Equal(0, world.Host.GoogleCallCount());
    }

    /// <summary>
    /// VR-001: no list, an empty list, an id that is not a positive 64-bit integer, or more than 1000 distinct ids — the
    /// outcome is Invalid, nothing changes and nothing is audited.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    [InlineData(" 5")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    [InlineData("1001 distinct")]
    public async Task AnInvalidForm_IsRefused_AndChangesNothing(string? value)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct);
        await using var _host = world.Host;
        var before = await world.Host.CourseFingerprintAsync(ct);
        string[]? ids = value switch
        {
            null => null,
            "" => [],
            "1001 distinct" => Enumerable.Range(1, 1001).Select(i => Id(i)).ToArray(),
            _ => [Id(world.First), value],
        };

        var result = await ConfirmAsync(world, ct, ids);

        Assert.Equal(CourseDeletionConfirmationOutcome.Invalid, result.Outcome);
        Assert.Equal(0, result.ConfirmedCount);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        Assert.Empty(await world.Host.AuditRowsAsync(ct));
    }

    /// <summary>
    /// VR-001, I-6: the bound is on distinct ids — 1000 distinct, and 1001 entries that are 1000 distinct, pass the shape
    /// check (they then fail VR-002 because the ids are unknown, so the outcome is ListChanged, not Invalid).
    /// </summary>
    [Theory]
    [InlineData(1000, 0)]
    [InlineData(1000, 1)]
    public async Task UpTo1000DistinctIds_PassTheShapeCheck(int distinct, int duplicates)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct);
        await using var _host = world.Host;
        var ids = Enumerable.Range(1_000_000, distinct).Select(i => Id(i)).Concat(Enumerable.Repeat(Id(1_000_000), duplicates)).ToArray();

        var result = await ConfirmAsync(world, ct, ids);

        Assert.Equal(CourseDeletionConfirmationOutcome.ListChanged, result.Outcome);
    }

    /// <summary>
    /// AC-017, VR-002, FR-006.2 step 3, I-5: a selected course no longer "not visible" (already confirmed, listed again,
    /// or unknown) refuses the whole request — the other selected course is not confirmed either, and no audit row.
    /// </summary>
    [Theory]
    [InlineData("not_in_classroom")]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task AStaleSelection_ChangesNothing_AndWritesNoAudit(string? secondNow)
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await StartAsync(database, ct);
        await using var _host = world.Host;
        var second = Id(world.Second);
        if (secondNow == "unknown")
        {
            second = "987654321";
        }
        else
        {
            await world.Host.ExecuteAsync(
                "UPDATE course SET absence_mark = @mark, absent_since = @since WHERE id = @id",
                ct,
                ("mark", secondNow),
                ("since", secondNow is null ? null : (DateTimeOffset?)D1),
                ("id", world.Second));
        }

        var before = await world.Host.CourseFingerprintAsync(ct);

        var result = await ConfirmAsync(world, ct, Id(world.First), second);

        Assert.Equal(new CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome.ListChanged, 0), result);
        Assert.Equal(before, await world.Host.CourseFingerprintAsync(ct));
        Assert.Empty(await world.Host.AuditRowsAsync(ct));
    }
}
