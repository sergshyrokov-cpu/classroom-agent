using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-027 db-design §2, §5.3, §7 (and AC-002, AC-009, AC-014): the template aggregate over real PostgreSQL (TC-2) —
/// round trip with ordered scale rows, normalized-name uniqueness, the last-change time, replacing scale rows,
/// cascade on delete, and a template that outlives its author's account.
/// </summary>
public sealed class ReportTemplateRepositoryTests(PostgreSqlFixture database)
{
    private static ReportTemplateSettings OwnMarks() => ReportTemplateTestData.Settings(
        view: ReportView.Short,
        hideMaterials: true,
        hours: 3,
        scale: ReportTemplateTestData.TwelvePoint,
        marks: new Dictionary<ReportCellState, ReportMark>
        {
            [ReportCellState.NotAssigned] = new(ReportMarkKind.Own, "—"),
            [ReportCellState.Returned] = new(ReportMarkKind.Empty, null),
        },
        late: new ReportLateMark(ReportLateMarkKind.Own, "late"));

    private async Task<(InstallationTestHost Host, long Author)> StartWithAuthorAsync(CancellationToken ct)
    {
        var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        return (host, await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct));
    }

    [Fact]
    public async Task ATemplate_RoundTrips_WithItsMarksAndOrderedScale()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        long ranges = 0;
        long none = 0;

        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var a = ReportTemplate.Create("  Test Ranges  ", OwnMarks(), author);
            var b = ReportTemplate.Create("Test None", ReportTemplateTestData.Settings(), author);
            repo.Add(a);
            repo.Add(b);
            await work.SaveChangesAsync(ct);
            ranges = a.Id;
            none = b.Id;
        });

        var rows = await host.TemplateRowsAsync(ct);
        var root = Assert.Single(rows, r => r.Id == ranges);
        Assert.Equal("Test Ranges", root.Name);
        Assert.Equal("TEST RANGES", root.NormalizedName);
        Assert.Equal("short", root.View);
        Assert.True(root.HideMaterials);
        Assert.Equal(3, root.HoursPerLesson);
        Assert.Equal("ranges", root.ScaleMode);
        Assert.Equal("own", root.LateMarkKind);
        Assert.Equal("late", root.LateMarkText);
        Assert.Equal(author, root.AuthorId);
        Assert.Equal(ReportTemplateTestData.TwelvePoint, await host.ScaleRowsAsync(ranges, ct));
        var marks = await host.MarkRowsAsync(ranges, ct);
        Assert.Equal(9, marks.Count);
        Assert.Contains(("not_assigned", "own", "—"), marks);
        Assert.Contains(("returned", "empty", null), marks);
        Assert.Contains(("turned_in", "program", null), marks);
        var noneRoot = Assert.Single(rows, r => r.Id == none);
        Assert.Equal("none", noneRoot.ScaleMode);
        Assert.Empty(await host.ScaleRowsAsync(none, ct));
        Assert.Equal(9, (await host.MarkRowsAsync(none, ct)).Count);

        await host.WithTemplateRepositoryAsync(async (repo, _) =>
        {
            var loaded = await repo.GetAsync(ranges, forUpdate: false, ct);

            Assert.NotNull(loaded);
            Assert.Equal("Test Ranges", loaded.Name);
            Assert.Equal(author, loaded.AuthorId);
            Assert.Equal(ReportTemplateTestData.TwelvePoint, loaded.ScaleRows.Select(r => (r.FromPercent, r.ToPercent, r.Label)).ToArray());
            var settings = loaded.ToSettings();
            Assert.Equal(ReportView.Short, settings.View);
            Assert.Equal(ReportScaleMode.Ranges, settings.ScaleMode);
            Assert.Equal(new ReportMark(ReportMarkKind.Own, "—"), settings.Marks[ReportCellState.NotAssigned]);
            Assert.Equal(new ReportLateMark(ReportLateMarkKind.Own, "late"), settings.LateMark);

            var withoutScale = await repo.GetAsync(none, forUpdate: false, ct);
            Assert.NotNull(withoutScale);
            Assert.Empty(withoutScale.ScaleRows);
            Assert.Null(await repo.GetAsync(none + 1000, forUpdate: false, ct));
        });
    }

    [Fact]
    public async Task TwoNamesDifferingOnlyInCaseOrSpaces_ViolateTheUniqueIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        await host.InsertTemplateAsync(ct, "Test Alpha", author);

        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            repo.Add(ReportTemplate.Create("  test ALPHA ", ReportTemplateTestData.Settings(), author));

            await Assert.ThrowsAsync<UniqueReportTemplateNameViolationException>(() => work.SaveChangesAsync(ct));
        });

        Assert.Single(await host.TemplateRowsAsync(ct));
    }

    [Fact]
    public async Task NameExists_ExcludesTheTemplateItself()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        var alpha = await host.InsertTemplateAsync(ct, "Test Alpha", author);
        var beta = await host.InsertTemplateAsync(ct, "Test Beta", author);

        await host.WithTemplateRepositoryAsync(async (repo, _) =>
        {
            var normalized = ReportTemplate.Normalize("Test Alpha");

            Assert.True(await repo.NameExistsAsync(normalized, null, ct));
            Assert.False(await repo.NameExistsAsync(normalized, alpha, ct));
            Assert.True(await repo.NameExistsAsync(normalized, beta, ct));
            Assert.False(await repo.NameExistsAsync(ReportTemplate.Normalize("Test Gamma"), null, ct));
        });
    }

    [Fact]
    public async Task ChangingOnlyOneMark_AdvancesTheLastChangeTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        var id = await host.InsertTemplateAsync(ct, "Test Alpha", author);
        var before = Assert.Single(await host.TemplateRowsAsync(ct));
        host.Time.Advance(TimeSpan.FromMinutes(30));

        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var template = await repo.GetAsync(id, forUpdate: true, ct);
            Assert.NotNull(template);
            var changed = ReportTemplateTestData.Settings(marks: new Dictionary<ReportCellState, ReportMark>
            {
                [ReportCellState.NotTurnedIn] = new(ReportMarkKind.Own, "missed"),
            });

            template.Change("Test Alpha", changed);
            repo.MarkChanged(template);
            await work.SaveChangesAsync(ct);
        });

        var after = Assert.Single(await host.TemplateRowsAsync(ct));
        Assert.True(after.UpdatedAt > before.UpdatedAt);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Contains(("not_turned_in", "own", "missed"), await host.MarkRowsAsync(id, ct));
    }

    [Fact]
    public async Task ReplacingScaleRows_WithTheSameBounds_Saves()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        var id = await host.InsertTemplateAsync(ct, "Test Alpha", author, scale: ReportTemplateTestData.TwoRow);

        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var template = await repo.GetAsync(id, forUpdate: true, ct);
            Assert.NotNull(template);
            var relabelled = ReportTemplateTestData.Settings(scale: [(0, 49, "bad"), (50, 100, "good")]);

            template.Change("Test Alpha", relabelled);
            repo.MarkChanged(template);
            await work.SaveChangesAsync(ct);
        });

        Assert.Equal([(0, 49, "bad"), (50, 100, "good")], await host.ScaleRowsAsync(id, ct));
    }

    [Fact]
    public async Task Deleting_RemovesTheMarksAndScaleRows_AndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        var doomed = await host.InsertTemplateAsync(ct, "Test Alpha", author, scale: ReportTemplateTestData.TwelvePoint);
        var kept = await host.InsertTemplateAsync(ct, "Test Beta", author, scale: ReportTemplateTestData.TwoRow);
        var accounts = await host.ScalarAsync<long>("SELECT count(*) FROM app_user", ct);

        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var template = await repo.GetAsync(doomed, forUpdate: true, ct);
            Assert.NotNull(template);

            repo.Remove(template);
            await work.SaveChangesAsync(ct);
        });

        Assert.Equal([kept], (await host.TemplateRowsAsync(ct)).Select(r => r.Id).ToArray());
        Assert.Empty(await host.MarkRowsAsync(doomed, ct));
        Assert.Empty(await host.ScaleRowsAsync(doomed, ct));
        Assert.Equal(9, (await host.MarkRowsAsync(kept, ct)).Count);
        Assert.Equal(ReportTemplateTestData.TwoRow, await host.ScaleRowsAsync(kept, ct));
        Assert.Equal(accounts, await host.ScalarAsync<long>("SELECT count(*) FROM app_user", ct));
    }

    [Fact]
    public async Task ATemplate_SurvivesTheDeletionOfItsAuthor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, author) = await StartWithAuthorAsync(ct);
        await using var _host = host;
        long id = 0;
        await host.WithTemplateRepositoryAsync(async (repo, work) =>
        {
            var template = ReportTemplate.Create("Test Alpha", ReportTemplateTestData.Settings(), author);
            repo.Add(template);
            await work.SaveChangesAsync(ct);
            id = template.Id;
        });
        await host.ExecuteAsync("DELETE FROM app_user WHERE id = @id", ct, ("id", author));

        await host.WithTemplateRepositoryAsync(async (repo, _) =>
        {
            var list = await repo.ListAsync(ct);

            var listed = Assert.Single(list);
            Assert.Equal(id, listed.Id);
            Assert.Equal("Test Alpha", listed.Name);
            Assert.Null(listed.AuthorEmail);
            var loaded = await repo.GetAsync(id, forUpdate: false, ct);
            Assert.NotNull(loaded);
            Assert.Equal(author, loaded.AuthorId);
        });
    }
}
