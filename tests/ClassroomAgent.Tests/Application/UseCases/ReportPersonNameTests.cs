using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-042 AC-002, AC-003, AC-004, AC-011, AC-013 (spec FR-003, FR-006): the name each person of a report is shown with —
/// the same rule for the students of "Grading" and the teachers of the header and "Lesson topics" — and the order of
/// the students by that name. The name source comes from the template; the page parameter is covered by
/// <see cref="ReportNameSourceTests"/>.
/// </summary>
public sealed class ReportPersonNameTests
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    private static DateTimeOffset Noon(int day) => new(2026, 9, day, 9, 0, 0, TimeSpan.Zero);

    /// <summary>One lesson in the period, so the report has a Grading part.</summary>
    private static (ReportTemplateWorld World, long Course) WorldWithLesson()
    {
        var world = new ReportTemplateWorld();
        var course = world.Fields.AddCourse();
        world.Fields.AddLesson(course, Noon(10));
        return (world, course);
    }

    private static async Task<Report> ReportAsync(ReportTemplateWorld world, long course, ReportNameSource source)
    {
        // A created template without conversion, so a grade reads as raw points; its "names" setting is the source.
        var template = world.SeedTemplate("Test Template Names", ReportTemplateTestData.Settings(nameSource: source))
            .Id.ToString(CultureInfo.InvariantCulture);
        var request = new ReportRequest(
            [template],
            [course.ToString(CultureInfo.InvariantCulture)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText]);
        var result = await world.Report.ExecuteAsync(request, Uk, TestContext.Current.CancellationToken);
        Assert.NotNull(result.Page.Report);
        return result.Page.Report;
    }

    private static PersonName Student(Report report) => Assert.Single(report.Grading!.Rows).Student;

    /// <summary>The FR-003 table, one row per case, for a student.</summary>
    public static TheoryData<ReportNameSource, string?, string?, string?, string?, ReportNameKind> Fr003Rows => new()
    {
        // source, surname, given name, email, shown, kind
        { ReportNameSource.Profile, "Тестова", "Олена", "olena.t@school-one.example.test", "Тестова Олена", ReportNameKind.Profile },
        { ReportNameSource.Profile, "Тестова", null, "olena.t@school-one.example.test", "Тестова", ReportNameKind.Profile },
        { ReportNameSource.Profile, null, "Олена", "olena.t@school-one.example.test", "Олена", ReportNameKind.Profile },
        { ReportNameSource.Profile, null, null, "olena.t@school-one.example.test", "olena.t", ReportNameKind.EmailLocalPart },
        { ReportNameSource.Email, "Тестова", "Олена", "olena.t@school-one.example.test", "olena.t", ReportNameKind.EmailLocalPart },
        { ReportNameSource.Email, null, null, "olena.t@school-one.example.test", "olena.t", ReportNameKind.EmailLocalPart },
        { ReportNameSource.Profile, null, null, null, null, ReportNameKind.Unnamed },
        { ReportNameSource.Email, "Тестова", "Олена", null, null, ReportNameKind.Unnamed },
    };

    /// <summary>AC-002, AC-003, AC-013: every row of the FR-003 table, for a student of "Grading".</summary>
    [Theory]
    [MemberData(nameof(Fr003Rows))]
    public async Task AStudent_IsNamedByTheFr003Table(
        ReportNameSource source, string? surname, string? givenName, string? email, string? shown, ReportNameKind kind)
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, surname: surname, givenName: givenName, email: email);

        var report = await ReportAsync(world, course, source);

        Assert.Equal(new PersonName(shown, kind), Student(report));
        Assert.Equal(source, report.NameSource);
    }

    /// <summary>AC-002, AC-003, AC-013: the same table for a teacher — one rule for both roles (v86 "Правило одно").</summary>
    [Theory]
    [MemberData(nameof(Fr003Rows))]
    public async Task ATeacher_IsNamedByTheSameTable(
        ReportNameSource source, string? surname, string? givenName, string? email, string? shown, ReportNameKind kind)
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, ClassroomRole.Teacher, surname: surname, givenName: givenName, email: email);

        var report = await ReportAsync(world, course, source);

        Assert.Equal([new PersonName(shown, kind)], report.Header.Teachers);
        Assert.Equal(source, report.NameSource);
    }

    /// <summary>
    /// AC-003, spec I-5: the part before the <b>first</b> <c>@</c>, exactly as stored — nothing trimmed, re-cased or
    /// translated; an address with nothing before <c>@</c> or no <c>@</c> at all counts as no address.
    /// </summary>
    [Theory]
    [InlineData("first.last@school-one.example.test", "first.last")]
    [InlineData("o'neil-2@school-one.example.test", "o'neil-2")]
    [InlineData("a@b@school-one.example.test", "a")]
    [InlineData("@school-one.example.test", null)]
    [InlineData("no-at-sign", null)]
    public async Task TheEmailPart_IsTheStoredAddressUpToItsFirstAt(string email, string? shown)
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, surname: "Тестова", givenName: "Олена", email: email);

        var report = await ReportAsync(world, course, ReportNameSource.Email);

        Assert.Equal(
            shown is null ? new PersonName(null, ReportNameKind.Unnamed) : new PersonName(shown, ReportNameKind.EmailLocalPart),
            Student(report));
    }

    /// <summary>AC-011, spec FR-003: names are shown as Google holds them — Latin letters and case are not corrected.</summary>
    [Fact]
    public async Task AProfileName_IsShownAsGoogleHoldsIt()
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, surname: "TESTOVA", givenName: "olena", email: "olena.t@school-one.example.test");

        var report = await ReportAsync(world, course, ReportNameSource.Profile);

        Assert.Equal(new PersonName("TESTOVA olena", ReportNameKind.Profile), Student(report));
    }

    /// <summary>
    /// AC-004: with the profile source the students are ordered by the name shown — so by surname — in the Ukrainian
    /// collation, ties by internal id; a student shown by the email part takes its place by that text; the unnamed
    /// come last.
    /// </summary>
    [Fact]
    public async Task WithTheProfile_StudentsAreOrderedBySurname_ThenTheUnnamed()
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, surname: null, givenName: null, email: null);
        world.Fields.AddMember(course, surname: "Ярова", givenName: "Анна", email: "a.yarova@school-one.example.test");
        world.Fields.AddMember(course, surname: "Іваненко", givenName: "Петро", email: "p.ivanenko@school-one.example.test");
        world.Fields.AddMember(course, surname: "Бойко", givenName: "Тарас", email: "zz.boiko@school-one.example.test");
        world.Fields.AddMember(course, surname: null, givenName: null, email: "ґудзь@school-one.example.test");
        world.Fields.AddMember(course, surname: "Бойко", givenName: "Тарас", email: "b.first@school-one.example.test");

        var report = await ReportAsync(world, course, ReportNameSource.Profile);

        Assert.Equal(
            new string?[] { "Бойко Тарас", "Бойко Тарас", "ґудзь", "Іваненко Петро", "Ярова Анна", null },
            report.Grading!.Rows.Select(r => r.Student.DisplayName));
    }

    /// <summary>AC-004: ties on the shown name follow the internal participant id (the earlier-added student first).</summary>
    [Fact]
    public async Task EqualShownNames_FollowTheInternalId()
    {
        var (world, course) = WorldWithLesson();
        var lesson = world.Fields.Lessons.Single().Lesson.Id;
        var first = world.Fields.AddMember(course, surname: "Бойко", givenName: "Тарас", email: "first@school-one.example.test");
        var second = world.Fields.AddMember(course, surname: "Бойко", givenName: "Тарас", email: "second@school-one.example.test");
        world.Fields.AddSubmission(lesson, second, assignedGrade: 7m);
        world.Fields.AddSubmission(lesson, first, assignedGrade: 3m);

        var report = await ReportAsync(world, course, ReportNameSource.Profile);

        Assert.All(report.Grading!.Rows, r => Assert.Equal(new PersonName("Бойко Тарас", ReportNameKind.Profile), r.Student));
        Assert.Equal([3m, 7m], report.Grading!.Rows.Select(r => r.Cells.Single().Grade!.Points!.Value));
    }

    /// <summary>AC-004: with the email source the students are ordered by the email part, not by surname.</summary>
    [Fact]
    public async Task WithTheEmail_StudentsAreOrderedByTheEmailPart()
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, surname: "Аврам", givenName: "Ян", email: "zed@school-one.example.test");
        world.Fields.AddMember(course, surname: "Ярова", givenName: "Анна", email: "alpha@school-one.example.test");

        var report = await ReportAsync(world, course, ReportNameSource.Email);

        Assert.Equal(new string?[] { "alpha", "zed" }, report.Grading!.Rows.Select(r => r.Student.DisplayName));
    }

    /// <summary>
    /// AC-002, spec FR-006: the teachers of the header are named and ordered by the same rule, and "Lesson topics" takes
    /// its teacher column from the header (US-027 openapi <c>LessonTopicsPart</c>).
    /// </summary>
    [Fact]
    public async Task Teachers_AreNamedAndOrderedByTheSameRule()
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, ClassroomRole.Teacher, surname: "Шевчук", givenName: "Марія", email: "m.shevchuk@school-one.example.test");
        world.Fields.AddMember(course, ClassroomRole.Teacher, surname: null, givenName: null, email: "aa.teacher@school-one.example.test");
        world.Fields.AddMember(course, ClassroomRole.Teacher, surname: null, givenName: null, email: null);
        world.Fields.AddMember(course, ClassroomRole.Teacher, surname: "Бондар", givenName: null, email: "o.bondar@school-one.example.test");

        var report = await ReportAsync(world, course, ReportNameSource.Profile);

        Assert.Equal(
            new[]
            {
                new PersonName("aa.teacher", ReportNameKind.EmailLocalPart),
                new PersonName("Бондар", ReportNameKind.Profile),
                new PersonName("Шевчук Марія", ReportNameKind.Profile),
                new PersonName(null, ReportNameKind.Unnamed),
            },
            report.Header.Teachers);
    }

    /// <summary>Spec FR-006, §9: the name parts come with the members already read — the round trips are unchanged.</summary>
    [Fact]
    public async Task NamingReadsNothingMore()
    {
        var (world, course) = WorldWithLesson();
        world.Fields.AddMember(course, surname: "Тестова", givenName: "Олена", email: "olena.t@school-one.example.test");

        var report = await ReportAsync(world, course, ReportNameSource.Profile);

        Assert.Equal("Тестова Олена", Student(report).DisplayName);
        Assert.Equal(
            ["GetCoursesAsync", "GetLessonsAsync", "GetMembersAsync", "GetLessonSubmissionsAsync"],
            world.Fields.Calls);
    }
}
