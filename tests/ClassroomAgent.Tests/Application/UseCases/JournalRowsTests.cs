using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-025 AC-002, AC-003 (OD-008), AC-008 (spec FR-002, FR-008, I-1): who has a row in the journal, how a row is
/// named, and the order of the rows in the Ukrainian and English cultures.
/// </summary>
public sealed class JournalRowsTests
{
    private static readonly DateTimeOffset StartUtc = JournalTestData.Period.StartUtc;

    private static readonly DateTimeOffset EndUtc = JournalTestData.Period.EndUtc;

    public static TheoryData<string[], string[]> UkrainianOrder => new()
    {
        {
            ["Ярослав Тест", "Іван Тест", "Євген Тест", "Андрій Тест"],
            ["Андрій Тест", "Євген Тест", "Іван Тест", "Ярослав Тест"]
        },
    };

    public static TheoryData<string[], string[]> EnglishOrder => new()
    {
        {
            ["charlie Test", "Bravo Test", "alpha Test"],
            ["alpha Test", "Bravo Test", "charlie Test"]
        },
    };

    private static GetJournalQuery QueryOver(FakeJournalSource source) =>
        new(source, new SchoolTimeZone(JournalTestData.Kyiv), new ManualTimeProvider(InstallationTestHost.DefaultStart));

    private static JournalRequest Request(long courseId) =>
        new(
            [courseId.ToString(CultureInfo.InvariantCulture)],
            [JournalTestData.Period.FromText],
            [JournalTestData.Period.ToText],
            []);

    /// <summary>The rows of the journal; an empty list when the journal is empty.</summary>
    private static async Task<IReadOnlyList<JournalRow>> RowsAsync(FakeJournalSource source, long courseId, string culture = "uk")
    {
        var result = await QueryOver(source).ExecuteAsync(
            Request(courseId), CultureInfo.GetCultureInfo(culture), TestContext.Current.CancellationToken);

        Assert.Equal(JournalPageOutcome.Shown, result.Outcome);
        return result.Page.Journal?.Rows ?? [];
    }

    private static (FakeJournalSource Source, long Course, long Item) Course()
    {
        var source = new FakeJournalSource();
        var course = source.AddCourse("Course");
        var item = source.AddItem(course, JournalTestData.Period.Early, maxPoints: 10m);
        return (source, course, item);
    }

    /// <summary>AC-002: a student on the roster for the whole period has a row.</summary>
    [Fact]
    public async Task OnRosterWholePeriod_HasRow()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Student Test");

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: a student who left during the period keeps the row.</summary>
    [Fact]
    public async Task LeftDuringPeriod_HasRow()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Student Test", onRoster: false, lastSeenAt: JournalTestData.Period.Early);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: a student who left before the period and has no submission has no row.</summary>
    [Fact]
    public async Task LeftBeforePeriod_WithoutSubmission_HasNoRow()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Stays Test");
        source.AddMember(course, "Gone Test", onRoster: false, lastSeenAt: StartUtc.AddDays(-1));

        var rows = await RowsAsync(source, course);

        Assert.Equal("Stays Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: a student who came during the period has a row.</summary>
    [Fact]
    public async Task CameDuringPeriod_HasRow()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Student Test", firstSeenAt: JournalTestData.Period.Early);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: a student who came after the period, or exactly at its end, has no row.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CameAtOrAfterEnd_HasNoRow(int daysAfterEnd)
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Present Test");
        source.AddMember(course, "Late Test", firstSeenAt: EndUtc.AddDays(daysAfterEnd));

        var rows = await RowsAsync(source, course);

        Assert.Equal("Present Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: <c>LastSeenAt</c> exactly at the start counts (<c>&gt;= start</c>).</summary>
    [Fact]
    public async Task LastSeenExactlyAtStart_OffRoster_HasRow()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Student Test", onRoster: false, lastSeenAt: StartUtc);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002 / I-1: still on the roster but last seen before the start (sync paused) has a row.</summary>
    [Fact]
    public async Task OnRoster_LastSeenBeforeStart_HasRow()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Student Test", onRoster: true, lastSeenAt: StartUtc.AddDays(-5));

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: an off-roster student with a submission to a column has a row.</summary>
    [Fact]
    public async Task OffRoster_WithSubmissionToColumn_HasRow()
    {
        var (source, course, item) = Course();
        var member = source.AddMember(course, "Student Test", onRoster: false, lastSeenAt: StartUtc.AddDays(-5));
        source.AddSubmission(item, member, SubmissionState.TurnedIn);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: an off-roster student whose only submission is to an item outside the period has no row.</summary>
    [Fact]
    public async Task OffRoster_SubmissionOnlyOutsidePeriod_HasNoRow()
    {
        var (source, course, _) = Course();
        var outside = source.AddItem(course, EndUtc.AddDays(10), maxPoints: 10m);
        source.AddMember(course, "Present Test");
        var member = source.AddMember(course, "Gone Test", onRoster: false, lastSeenAt: StartUtc.AddDays(-5));
        source.AddSubmission(outside, member, SubmissionState.TurnedIn);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Present Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-002: a teacher membership never gives a row, even with a submission.</summary>
    [Fact]
    public async Task Teacher_WithSubmission_HasNoRow()
    {
        var (source, course, item) = Course();
        source.AddMember(course, "Student Test");
        var teacher = source.AddMember(course, "Teacher Test", role: ClassroomRole.Teacher);
        source.AddSubmission(item, teacher, SubmissionState.TurnedIn);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-003 / OD-008: an off-roster student with two submissions to one item is one row.</summary>
    [Fact]
    public async Task OffRoster_WithTwoSubmissions_IsOneRow()
    {
        var (source, course, item) = Course();
        var member = source.AddMember(course, "Student Test", onRoster: false, lastSeenAt: StartUtc.AddDays(-5));
        source.AddSubmission(item, member, SubmissionState.TurnedIn, updateTime: JournalTestData.Period.Early, id: 4000);
        source.AddSubmission(item, member, SubmissionState.Returned, updateTime: JournalTestData.Period.Late, id: 5000);

        var rows = await RowsAsync(source, course);

        Assert.Equal("Student Test", Assert.Single(rows).DisplayName);
    }

    /// <summary>AC-008: a participant with a name is shown by the full name.</summary>
    [Fact]
    public async Task Named_IsFullName()
    {
        var (source, course, _) = Course();
        source.AddMember(course, "Student Test", email: "student@example.test");

        var row = Assert.Single(await RowsAsync(source, course));

        Assert.Equal("Student Test", row.DisplayName);
        Assert.Equal(JournalNameKind.FullName, row.NameKind);
    }

    /// <summary>AC-008: without a name the email is shown.</summary>
    [Fact]
    public async Task NoName_WithEmail_IsEmail()
    {
        var (source, course, _) = Course();
        source.AddMember(course, null, email: "student@example.test");

        var row = Assert.Single(await RowsAsync(source, course));

        Assert.Equal("student@example.test", row.DisplayName);
        Assert.Equal(JournalNameKind.Email, row.NameKind);
    }

    /// <summary>AC-008: with neither name nor email the row is unnamed and has no display name.</summary>
    [Fact]
    public async Task NoNameNoEmail_IsUnnamed()
    {
        var (source, course, _) = Course();
        source.AddMember(course, null);

        var row = Assert.Single(await RowsAsync(source, course));

        Assert.Null(row.DisplayName);
        Assert.Equal(JournalNameKind.Unnamed, row.NameKind);
    }

    /// <summary>AC-008: rows are ordered by display value in the Ukrainian culture (ordinal order differs).</summary>
    [Theory]
    [MemberData(nameof(UkrainianOrder))]
    public async Task Order_Ukrainian_IsCultureAware(string[] inserted, string[] expected)
    {
        var (source, course, _) = Course();
        foreach (var name in inserted)
        {
            source.AddMember(course, name);
        }

        var rows = await RowsAsync(source, course, "uk");

        Assert.Equal(expected, rows.Select(r => r.DisplayName));
    }

    /// <summary>AC-008: rows are ordered by display value in the English culture.</summary>
    [Theory]
    [MemberData(nameof(EnglishOrder))]
    public async Task Order_English_IsCultureAware(string[] inserted, string[] expected)
    {
        var (source, course, _) = Course();
        foreach (var name in inserted)
        {
            source.AddMember(course, name);
        }

        var rows = await RowsAsync(source, course, "en");

        Assert.Equal(expected, rows.Select(r => r.DisplayName));
    }

    /// <summary>AC-008: equal display values are ordered by participant id.</summary>
    [Fact]
    public async Task Order_EqualNames_ByParticipantId()
    {
        var (source, course, item) = Course();
        var higher = source.AddMember(course, "Same Test", participantId: 9002);
        var lower = source.AddMember(course, "Same Test", participantId: 9001);
        source.AddSubmission(item, higher, SubmissionState.TurnedIn, assignedGrade: 2m);
        source.AddSubmission(item, lower, SubmissionState.TurnedIn, assignedGrade: 1m);

        var rows = await RowsAsync(source, course);

        Assert.Equal([1m, 2m], rows.Select(r => r.Cells[0].Points));
    }

    /// <summary>AC-008: unnamed rows come after the named ones, ordered by participant id.</summary>
    [Fact]
    public async Task Order_UnnamedLast_ByParticipantId()
    {
        var (source, course, item) = Course();
        var unnamedHigh = source.AddMember(course, null, participantId: 9002);
        var unnamedLow = source.AddMember(course, null, participantId: 9001);
        source.AddMember(course, "Zed Test");
        source.AddMember(course, null, email: "adam@example.test");
        source.AddSubmission(item, unnamedHigh, SubmissionState.TurnedIn, assignedGrade: 2m);
        source.AddSubmission(item, unnamedLow, SubmissionState.TurnedIn, assignedGrade: 1m);

        var rows = await RowsAsync(source, course);

        Assert.Equal(4, rows.Count);
        Assert.Equal(["adam@example.test", "Zed Test", null, null], rows.Select(r => r.DisplayName));
        Assert.Equal([1m, 2m], rows.Skip(2).Select(r => r.Cells[0].Points));
    }
}
