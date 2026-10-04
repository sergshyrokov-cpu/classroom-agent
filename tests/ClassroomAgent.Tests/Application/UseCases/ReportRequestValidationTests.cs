using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 AC-007, AC-009 (spec FR-011, VR-007, §6): the report request — the built-in default, every malformed
/// template reference, the US-025 course and period rules, the order of the messages, the not-found outcome, nothing
/// read before validation, the return path built from validated values only, the default period and the absence of
/// any audit row.
/// </summary>
public sealed class ReportRequestValidationTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public static TheoryData<string> MalformedTemplates =>
        new("Academic-Journal", "0", "-1", "1.0", " 1", "abc", "99999999999999999999");

    public static TheoryData<string, string, ReportMessageKey> Us025Rules
    {
        get
        {
            var data = new TheoryData<string, string, ReportMessageKey>();
            foreach (var value in new[] { "abc", "-1", "+1", " 1", "1 ", "0", "1.0", "9223372036854775808", "١" })
            {
                data.Add("courseId", value, ReportMessageKey.CourseMalformed);
            }

            foreach (var value in new[]
            {
                "2026-9-1", "2026-09-31", "2027-02-29", "01.09.2026", "1999-12-31", "2101-01-01", " 2026-09-01", "2026-09-01T00:00",
            })
            {
                data.Add("from", value, ReportMessageKey.FromMalformed);
                data.Add("to", value, ReportMessageKey.ToMalformed);
            }

            return data;
        }
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static Task<ReportPageResult> RunAsync(ReportTemplateWorld world, ReportRequest request) =>
        world.Report.ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);

    private static ReportRequest Request(
        string? template, long? course, string? from = JournalTestData.Period.FromText, string? to = JournalTestData.Period.ToText) =>
        new(
            template is null ? [] : [template],
            course is null ? [] : [Id(course.Value)],
            from is null ? [] : [from],
            to is null ? [] : [to]);

    private static void AssertNothingRead(ReportTemplateWorld world)
    {
        Assert.DoesNotContain("GetLessonsAsync", world.Fields.Calls);
        Assert.DoesNotContain("GetMembersAsync", world.Fields.Calls);
        Assert.DoesNotContain("GetLessonSubmissionsAsync", world.Fields.Calls);
        Assert.DoesNotContain("GetAsync", world.Templates.Calls);
    }

    /// <summary>AC-009: with the template absent or empty the built-in template is selected and its report is built.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AnAbsentOrEmptyTemplate_SelectsTheBuiltIn(string? template)
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        var result = await RunAsync(world, Request(template, course));

        Assert.Equal(ReportPageOutcome.Shown, result.Outcome);
        Assert.Equal(ReportTemplateTestData.BuiltInKey, result.Page.SelectedTemplate);
        Assert.Empty(result.Page.MessageKeys);
        Assert.NotNull(result.Page.Report);
        Assert.True(result.Page.Report.Header.TemplateIsBuiltIn);
    }

    /// <summary>AC-009: without a course only the form is shown.</summary>
    [Fact]
    public async Task NoCourseId_ShowsTheFormOnly()
    {
        var world = new ReportTemplateWorld();
        world.Fields.AddCourse();

        var result = await RunAsync(world, Request(null, null));

        Assert.Equal(ReportPageOutcome.Shown, result.Outcome);
        Assert.Null(result.Page.Report);
        Assert.Null(result.Page.SelectedCourseId);
        Assert.Single(result.Page.Courses);
        AssertNothingRead(world);
    }

    /// <summary>AC-009: every malformed template reference is refused, falls back to the built-in and reads nothing.</summary>
    [Theory]
    [MemberData(nameof(MalformedTemplates))]
    public async Task AMalformedTemplateReference_IsRefused(string template)
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        var result = await RunAsync(world, Request(template, course));

        Assert.Equal(ReportPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { ReportMessageKey.TemplateMalformed }, result.Page.MessageKeys);
        Assert.Equal(ReportTemplateTestData.BuiltInKey, result.Page.SelectedTemplate);
        Assert.Null(result.Page.Report);
        AssertNothingRead(world);
    }

    /// <summary>AC-009: a repeated template parameter is malformed.</summary>
    [Fact]
    public async Task ARepeatedTemplate_IsMalformed()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var request = new ReportRequest(
            [ReportTemplateTestData.BuiltInKey, ReportTemplateTestData.BuiltInKey],
            [Id(course)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText]);

        var result = await RunAsync(world, request);

        Assert.Equal(ReportPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { ReportMessageKey.TemplateMalformed }, result.Page.MessageKeys);
    }

    /// <summary>AC-009: the course and period rules of US-025 apply to the report request unchanged.</summary>
    [Theory]
    [MemberData(nameof(Us025Rules))]
    public async Task TheQueryRulesOfUs025_Apply(string parameter, string value, ReportMessageKey expected)
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var request = new ReportRequest(
            [],
            [parameter == "courseId" ? value : Id(course)],
            [parameter == "from" ? value : JournalTestData.Period.FromText],
            [parameter == "to" ? value : JournalTestData.Period.ToText]);

        var result = await RunAsync(world, request);

        Assert.Equal(ReportPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { expected }, result.Page.MessageKeys);
        Assert.Null(result.Page.Report);
    }

    /// <summary>AC-009: from after to is an inverted period, reported last.</summary>
    [Fact]
    public async Task AnInvertedPeriod_IsRefused_AndKeepsBothDates()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        var result = await RunAsync(world, Request(null, course, "2026-09-30", "2026-09-01"));

        Assert.Equal(ReportPageOutcome.Invalid, result.Outcome);
        Assert.Equal(new[] { ReportMessageKey.PeriodInverted }, result.Page.MessageKeys);
        Assert.Equal(new DateOnly(2026, 9, 30), result.Page.From);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Page.To);
    }

    /// <summary>AC-009: the messages come in the order template, courseId, from, to, then the inverted period.</summary>
    [Fact]
    public async Task TheMessages_AreInParameterOrder()
    {
        var world = new ReportTemplateWorld();
        world.Fields.AddCourse();

        var all = await RunAsync(
            world, new ReportRequest(["abc"], ["abc"], ["x"], ["y"]));
        var inverted = await RunAsync(
            world, new ReportRequest(["abc"], [Id(1)], ["2026-09-30"], ["2026-09-01"]));

        Assert.Equal(
            new[]
            {
                ReportMessageKey.TemplateMalformed,
                ReportMessageKey.CourseMalformed,
                ReportMessageKey.FromMalformed,
                ReportMessageKey.ToMalformed,
            },
            all.Page.MessageKeys);
        Assert.Equal(new[] { ReportMessageKey.TemplateMalformed, ReportMessageKey.PeriodInverted }, inverted.Page.MessageKeys);
    }

    /// <summary>AC-009: a well-formed template id that is not stored is not found; the report is not built.</summary>
    [Fact]
    public async Task AnUnknownTemplateOrCourse_IsNotFound()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        var template = await RunAsync(world, Request("999", course));
        var unknownCourse = await RunAsync(world, Request(null, course + 1_000_000));
        var both = await RunAsync(world, Request("999", course + 1_000_000));

        Assert.Equal(ReportPageOutcome.NotFound, template.Outcome);
        Assert.Equal(new[] { ReportMessageKey.TemplateNotFound }, template.Page.MessageKeys);
        Assert.Null(template.Page.Report);
        Assert.Equal(ReportPageOutcome.NotFound, unknownCourse.Outcome);
        Assert.Equal(new[] { ReportMessageKey.CourseUnknown }, unknownCourse.Page.MessageKeys);
        Assert.Equal(ReportPageOutcome.NotFound, both.Outcome);
        Assert.Equal(new[] { ReportMessageKey.TemplateNotFound, ReportMessageKey.CourseUnknown }, both.Page.MessageKeys);
    }

    /// <summary>AC-009: once the query is refused no lesson, member, submission or template is read.</summary>
    [Fact]
    public async Task NothingIsReadForTheReport_WhenTheQueryIsInvalid()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        await RunAsync(world, Request("abc", course));
        await RunAsync(world, Request("1", course, "2026-9-1"));
        await RunAsync(world, new ReportRequest([], ["abc"], [], []));

        AssertNothingRead(world);
    }

    /// <summary>AC-009: the return path holds the validated values; a malformed value is never echoed.</summary>
    [Fact]
    public async Task TheReturnPath_CarriesOnlyValidatedValues()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var template = world.SeedTemplate("Test Template One");

        var result = await RunAsync(world, Request(Id(template.Id), course, JournalTestData.Period.FromText, "zz-bad-zz"));

        Assert.StartsWith("/reports", result.Page.ReturnPath, StringComparison.Ordinal);
        Assert.Contains(Id(template.Id), result.Page.ReturnPath, StringComparison.Ordinal);
        Assert.Contains(Id(course), result.Page.ReturnPath, StringComparison.Ordinal);
        Assert.Contains(JournalTestData.Period.FromText, result.Page.ReturnPath, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-bad-zz", result.Page.ReturnPath, StringComparison.Ordinal);
    }

    /// <summary>AC-009: with no dates the period is the current month in Kyiv, read as that UTC interval.</summary>
    [Fact]
    public async Task TheDefaultPeriod_IsTheCurrentMonthInKyiv()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        var result = await RunAsync(world, Request(null, course, null, null));

        Assert.Equal(ReportPageOutcome.Shown, result.Outcome);
        Assert.Equal(JournalTestData.Period.From, result.Page.From);
        Assert.Equal(JournalTestData.Period.To, result.Page.To);
        Assert.NotEmpty(world.Fields.Intervals);
        Assert.All(
            world.Fields.Intervals,
            i => Assert.Equal((JournalTestData.Period.StartUtc, JournalTestData.Period.EndUtc), i));
    }

    /// <summary>AC-007: viewing a report writes no audit row and commits nothing.</summary>
    [Fact]
    public async Task TheReport_WritesNoAuditRow()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();

        await RunAsync(world, Request(null, course));
        await RunAsync(world, Request("abc", course));
        await RunAsync(world, Request("999", course));

        Assert.Empty(world.Audit.Written);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>AC-009: the template drop-down lists the built-in first, then the created templates by name.</summary>
    [Fact]
    public async Task TheTemplateDropDown_ListsTheBuiltInFirst_ThenCreatedByName()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        var beta = world.SeedTemplate("Бета");
        var alpha = world.SeedTemplate("Альфа");

        var result = await RunAsync(world, Request(null, course));

        Assert.Equal(
            new[]
            {
                new ReportTemplateOption(ReportTemplateTestData.BuiltInKey, true, null),
                new ReportTemplateOption(Id(alpha.Id), false, "Альфа"),
                new ReportTemplateOption(Id(beta.Id), false, "Бета"),
            },
            result.Page.Templates);
    }
}
