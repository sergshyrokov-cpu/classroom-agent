using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-042 AC-006, AC-007, AC-010, AC-014 (spec FR-004, FR-005, VR-002; api-design §2.2, §2.4, §2.5): the effective
/// name source of a report request — the page parameter over the template's setting — the switch and its links, the
/// language-switcher return path, the refusal of a malformed parameter, and that none of it writes anything, in
/// read-only mode too.
/// </summary>
public sealed class ReportNameSourceTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private const string StudentEmail = "olena.t@school-one.example.test";

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static ReportRequest Request(string template, long? course, params string?[]? names) =>
        new(
            [template],
            course is null ? [] : [Id(course.Value)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText],
            names);

    private static Task<ReportPageResult> RunAsync(ReportTemplateWorld world, ReportRequest request) =>
        world.Report.ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);

    /// <summary>A course with one lesson and one student who has both a profile name and an address.</summary>
    private static (ReportTemplateWorld World, long Course) Seeded(bool readOnly = false)
    {
        var world = new ReportTemplateWorld(readOnly);
        var course = world.Fields.AddCourse();
        world.Fields.AddLesson(course, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero));
        world.Fields.AddMember(course, surname: "Тестова", givenName: "Олена", email: StudentEmail);
        return (world, course);
    }

    private static string StudentName(ReportPageResult result) =>
        Assert.Single(result.Page.Report!.Grading!.Rows).Student.DisplayName!;

    /// <summary>The query of a page path as name → values, order-insensitive.</summary>
    private static Dictionary<string, string[]> QueryOf(string path)
    {
        Assert.StartsWith(ReportTemplateTestData.ReportPath + "?", path, StringComparison.Ordinal);
        return path[(path.IndexOf('?', StringComparison.Ordinal) + 1)..]
            .Split('&')
            .Select(p => p.Split('=', 2))
            .GroupBy(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]))
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    private static void AssertNothingWritten(ReportTemplateWorld world)
    {
        Assert.Empty(world.Templates.Added);
        Assert.Empty(world.Templates.MarkedChanged);
        Assert.Empty(world.Templates.Removed);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>AC-005, spec FR-004: without the parameter the built-in template's "profile" applies.</summary>
    [Fact]
    public async Task WithoutTheParameter_TheBuiltInUsesTheProfile()
    {
        var (world, course) = Seeded();

        var result = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, course));

        Assert.Equal(ReportPageOutcome.Shown, result.Outcome);
        Assert.Equal("Тестова Олена", StudentName(result));
        Assert.Equal(ReportNameSource.Profile, result.Page.Report!.NameSource);
        Assert.Equal(NameSourceOrigin.Template, result.Page.Report.NameSourceOrigin);
    }

    /// <summary>Spec FR-004: without the parameter a created template's own setting applies.</summary>
    [Fact]
    public async Task WithoutTheParameter_ACreatedTemplatesSettingApplies()
    {
        var (world, course) = Seeded();
        var template = world.SeedTemplate(settings: ReportTemplateTestData.Settings(nameSource: ReportNameSource.Email));

        var result = await RunAsync(world, Request(Id(template.Id), course));

        Assert.Equal("olena.t", StudentName(result));
        Assert.Equal(ReportNameSource.Email, result.Page.Report!.NameSource);
        Assert.Equal(NameSourceOrigin.Template, result.Page.Report.NameSourceOrigin);
    }

    /// <summary>
    /// AC-006: the parameter overrides the template both ways; the template is read, never changed, and nothing is
    /// audited.
    /// </summary>
    [Theory]
    [InlineData(ReportNameSource.Profile, "email", "olena.t", ReportNameSource.Email)]
    [InlineData(ReportNameSource.Email, "profile", "Тестова Олена", ReportNameSource.Profile)]
    public async Task TheParameter_OverridesTheTemplate_AndChangesNothing(
        ReportNameSource setting, string parameter, string shown, ReportNameSource effective)
    {
        var (world, course) = Seeded();
        var template = world.SeedTemplate(settings: ReportTemplateTestData.Settings(nameSource: setting));

        var result = await RunAsync(world, Request(Id(template.Id), course, parameter));

        Assert.Equal(ReportPageOutcome.Shown, result.Outcome);
        Assert.Equal(shown, StudentName(result));
        Assert.Equal(effective, result.Page.Report!.NameSource);
        Assert.Equal(NameSourceOrigin.Page, result.Page.Report.NameSourceOrigin);
        Assert.Equal(setting, template.NameSource);
        AssertNothingWritten(world);
        Assert.Equal(["ListAsync", "GetAsync"], world.Templates.Calls);
    }

    /// <summary>
    /// AC-006, spec FR-005, api-design §2.2: with a report shown the switch offers profile then email, the effective one
    /// current, each a link to the same template, course and period with that source and nothing else.
    /// </summary>
    [Fact]
    public async Task TheSwitch_LinksBothSources_WithTheEffectiveOneCurrent()
    {
        var (world, course) = Seeded();
        var template = world.SeedTemplate(settings: ReportTemplateTestData.Settings(nameSource: ReportNameSource.Email));

        var result = await RunAsync(world, Request(Id(template.Id), course));

        var options = Assert.IsType<NameSourceSwitch>(result.Page.NameSwitch).Options;
        Assert.Equal([ReportNameSource.Profile, ReportNameSource.Email], options.Select(o => o.Source));
        Assert.Equal([false, true], options.Select(o => o.IsCurrent));
        foreach (var (option, value) in options.Zip(new[] { "profile", "email" }))
        {
            var query = QueryOf(option.Path);
            Assert.Equal(["courseId", "from", "names", "template", "to"], query.Keys.Order(StringComparer.Ordinal));
            Assert.Equal([Id(template.Id)], query["template"]);
            Assert.Equal([Id(course)], query["courseId"]);
            Assert.Equal([JournalTestData.Period.FromText], query["from"]);
            Assert.Equal([JournalTestData.Period.ToText], query["to"]);
            Assert.Equal([value], query["names"]);
        }
    }

    /// <summary>AC-006: a page parameter moves the current mark; the built-in template's switch carries its key.</summary>
    [Fact]
    public async Task TheSwitch_MarksThePageParameter()
    {
        var (world, course) = Seeded();

        var result = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, course, "email"));

        var options = result.Page.NameSwitch!.Options;
        Assert.Equal([false, true], options.Select(o => o.IsCurrent));
        Assert.All(options, o => Assert.Equal([ReportTemplateTestData.BuiltInKey], QueryOf(o.Path)["template"]));
    }

    /// <summary>Spec FR-005: no report — form only, or a 404 — means no switch.</summary>
    [Fact]
    public async Task WithoutAReport_ThereIsNoSwitch()
    {
        var (world, course) = Seeded();

        var formOnly = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, null, "email"));
        var unknown = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, 987654, "email"));
        var shown = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, course, "email"));

        Assert.Equal(ReportPageOutcome.Shown, formOnly.Outcome);
        Assert.Null(formOnly.Page.Report);
        Assert.Null(formOnly.Page.NameSwitch);
        Assert.Equal(ReportPageOutcome.NotFound, unknown.Outcome);
        Assert.Null(unknown.Page.NameSwitch);
        Assert.NotNull(shown.Page.NameSwitch);
    }

    /// <summary>AC-014, api-design §2.4: a valid parameter is carried into the language-switcher return path.</summary>
    [Fact]
    public async Task TheReturnPath_KeepsAValidParameter()
    {
        var (world, course) = Seeded();

        var result = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, course, "email"));

        var query = QueryOf(result.Page.ReturnPath);
        Assert.Equal(["email"], query["names"]);
        Assert.Equal([Id(course)], query["courseId"]);
    }

    /// <summary>
    /// Api-design §2.4, spec I-4: without the parameter the return path carries none — the template's own setting stays
    /// in charge after a language change.
    /// </summary>
    [Fact]
    public async Task TheReturnPath_CarriesNoSourceWhenTheAddressHadNone()
    {
        var (world, course) = Seeded();
        var template = world.SeedTemplate(settings: ReportTemplateTestData.Settings(nameSource: ReportNameSource.Email));

        var result = await RunAsync(world, Request(Id(template.Id), course));

        Assert.Equal(ReportNameSource.Email, result.Page.Report!.NameSource);
        var query = QueryOf(result.Page.ReturnPath);
        Assert.Equal([Id(template.Id)], query["template"]);
        Assert.False(query.ContainsKey("names"));
    }

    /// <summary>VR-002 values that are not exactly <c>profile</c> or <c>email</c> — case, spaces, empty, other words.</summary>
    public static TheoryData<string[]> MalformedNames => new()
    {
        new[] { "" },
        new[] { "Email" },
        new[] { "PROFILE" },
        new[] { " email" },
        new[] { "email " },
        new[] { "full" },
        new[] { "fullName" },
        new[] { "zz-bad-zz" },
        new[] { "email", "email" },
        new[] { "profile", "email" },
    };

    /// <summary>
    /// AC-010, VR-002, api-design §2.5: a malformed parameter is refused before any report data is read, its key comes
    /// after every US-027 key, nothing is echoed into the return path and nothing is written.
    /// </summary>
    [Theory]
    [MemberData(nameof(MalformedNames))]
    public async Task AMalformedParameter_IsRefused_BeforeAnythingIsRead(string[] names)
    {
        var (world, course) = Seeded();

        var result = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, course, names));

        Assert.Equal(ReportPageOutcome.Invalid, result.Outcome);
        Assert.Equal([ReportMessageKey.NameSourceMalformed], result.Page.MessageKeys);
        Assert.Null(result.Page.Report);
        Assert.Null(result.Page.NameSwitch);
        Assert.DoesNotContain("GetLessonsAsync", world.Fields.Calls);
        Assert.DoesNotContain("GetMembersAsync", world.Fields.Calls);
        Assert.DoesNotContain("names=", result.Page.ReturnPath, StringComparison.Ordinal);
        AssertNothingWritten(world);
    }

    /// <summary>Api-design §2.5: the name-source key comes last, after the period pair.</summary>
    [Fact]
    public async Task TheNameSourceKey_ComesAfterThePeriodKeys()
    {
        var (world, course) = Seeded();
        var request = new ReportRequest(
            [ReportTemplateTestData.BuiltInKey], [Id(course)], ["2026-09-30"], ["2026-09-01"], ["bad"]);

        var result = await RunAsync(world, request);

        Assert.Equal([ReportMessageKey.PeriodInverted, ReportMessageKey.NameSourceMalformed], result.Page.MessageKeys);
    }

    /// <summary>Api-design §2.5: a malformed parameter is reported even with no course chosen (form only).</summary>
    [Fact]
    public async Task AMalformedParameter_IsReported_WithoutACourse()
    {
        var (world, _) = Seeded();

        var result = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, null, "bad"));

        Assert.Equal(ReportPageOutcome.Invalid, result.Outcome);
        Assert.Equal([ReportMessageKey.NameSourceMalformed], result.Page.MessageKeys);
    }

    /// <summary>
    /// AC-007, spec FR-005, FR-008: in read-only mode the switch works exactly as outside it, and the guard is not
    /// asked — while in the same world a template save is refused by it (the control).
    /// </summary>
    [Fact]
    public async Task InReadOnlyMode_TheSwitchWorks_WhileASaveIsRefused()
    {
        var (world, course) = Seeded(readOnly: true);

        var result = await RunAsync(world, Request(ReportTemplateTestData.BuiltInKey, course, "email"));

        Assert.Equal(ReportPageOutcome.Shown, result.Outcome);
        Assert.Equal("olena.t", StudentName(result));
        Assert.NotNull(result.Page.NameSwitch);
        Assert.Empty(world.ReadOnly.Operations);
        AssertNothingWritten(world);

        await Assert.ThrowsAsync<ClassroomAgent.Application.Exceptions.ReadOnlyModeException>(() =>
            world.Save.CreateAsync(
                ReportTemplateWorld.DeanId,
                AppRole.Dean,
                ReportTemplateFormBuilder.Valid().Set(ReportTemplateTestData.NamesField, "email").Input(),
                "req-1",
                TestContext.Current.CancellationToken));
        Assert.NotEmpty(world.ReadOnly.Operations);
    }
}
