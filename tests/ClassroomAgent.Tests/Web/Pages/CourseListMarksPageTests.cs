using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.DataMarksHostExtensions;
using Card = ClassroomAgent.Tests.TestInfrastructure.CourseCardHostExtensions;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using W = ClassroomAgent.Tests.TestInfrastructure.DataMarksWorld;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-060 on the course list and the course card over HTTP and PostgreSQL (TC-2): the course mark next to the stored
/// status (FR-001) and "no teacher in Classroom" in place of the teachers (FR-002), Ukrainian and English (FR-009). The
/// four courses of <see cref="DataMarksHostExtensions.SeedListCoursesAsync"/>, all active.
/// </summary>
public sealed class CourseListMarksPageTests(PostgreSqlFixture database)
{
    public static TheoryData<string> Languages => new("uk", "en");

    private static string RowText(PageResponse page, long courseId)
    {
        var index = Cl.RowIds(page.Body).ToList().IndexOf(courseId);
        Assert.True(index >= 0, $"Course {courseId} is not listed.");
        return Cl.RowTexts(page.Body)[index];
    }

    /// <summary>
    /// AC-001, AC-009, FR-001.1 … FR-001.4: in the list the marked courses show the mark with its Kyiv date after their
    /// status and before their teacher; the unmarked one only its status.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheCourseList_ShowsEachCoursesMark_NextToItsStatus(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var courses = await host.SeedListCoursesAsync(ct);
        var texts = DataMarksTexts.Of(language);
        var active = host.Text(CourseListTestData.Keys.StatusActive, language);
        var notIn = texts.With(texts.CourseNotInClassroomSince, W.D1);
        var notVisible = texts.With(texts.CourseNotVisibleSince, W.D2);

        var page = await client.GetAsync(CourseListTestData.Url(status: "all"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        AssertBetween(RowText(page, courses.Absent), active, notIn, PresentTeacherName);
        AssertBetween(RowText(page, courses.Hidden), active, notVisible, PresentTeacherName);
        var plain = RowText(page, courses.Plain);
        Assert.Contains(active, plain, StringComparison.Ordinal);
        Assert.DoesNotContain(MarkStem(texts.CourseNotInClassroomSince), plain, StringComparison.Ordinal);
        Assert.DoesNotContain(MarkStem(texts.CourseNotVisibleSince), plain, StringComparison.Ordinal);
        Assert.DoesNotContain(notIn, RowText(page, courses.Hidden), StringComparison.Ordinal);
        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-001, AC-009, FR-001.2: the card shows the same mark in its status field; the unmarked card only the status.</summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task TheCourseCard_ShowsTheCoursesMark_InItsStatusField(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var courses = await host.SeedListCoursesAsync(ct);
        var texts = DataMarksTexts.Of(language);
        var active = host.Text(CourseListTestData.Keys.StatusActive, language);

        var absent = await client.GetAsync(CourseCardTestData.Path(courses.Absent), ct);
        var hidden = await client.GetAsync(CourseCardTestData.Path(courses.Hidden), ct);
        var plain = await client.GetAsync(CourseCardTestData.Path(courses.Plain), ct);

        Assert.Equal(HttpStatusCode.OK, absent.Status);
        AssertBetween(Status(absent), active, texts.With(texts.CourseNotInClassroomSince, W.D1), null);
        AssertBetween(Status(hidden), active, texts.With(texts.CourseNotVisibleSince, W.D2), null);
        Assert.Equal(active, Status(plain));
    }

    /// <summary>
    /// AC-002, AC-009, FR-002.1, BR-069: the course whose only teacher left shows "no teacher in Classroom" in place of the
    /// teachers and not the former teacher; the course with a teacher on the roster shows the teacher's name.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public async Task ACourseWithoutATeacherOnTheRoster_SaysSo_InPlaceOfTheTeachers(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(database, language, ct);
        await using var _host = host;
        var courses = await host.SeedListCoursesAsync(ct);
        var texts = DataMarksTexts.Of(language);

        var page = await client.GetAsync(CourseListTestData.Url(status: "all"), ct);

        var teacherless = RowText(page, courses.Teacherless);
        var plain = RowText(page, courses.Plain);
        Assert.Contains(texts.NoTeacher, teacherless, StringComparison.Ordinal);
        Assert.DoesNotContain(FormerTeacherName, teacherless, StringComparison.Ordinal);
        Assert.Contains(PresentTeacherName, plain, StringComparison.Ordinal);
        Assert.DoesNotContain(texts.NoTeacher, plain, StringComparison.Ordinal);
    }

    private static string Status(PageResponse card)
    {
        var inner = Card.FieldInnerHtml(card.Body, "status");
        Assert.NotNull(inner);
        return Text(inner);
    }

    /// <summary>The mark stands after the status and, when given, before <paramref name="after"/>.</summary>
    private static void AssertBetween(string text, string status, string mark, string? after)
    {
        var s = text.IndexOf(status, StringComparison.Ordinal);
        var m = text.IndexOf(mark, StringComparison.Ordinal);
        Assert.True(s >= 0, $"No status «{status}» in «{text}».");
        Assert.True(m > s, $"The mark «{mark}» must follow the status in «{text}».");
        if (after is not null)
        {
            Assert.True(text.IndexOf(after, StringComparison.Ordinal) > m, $"The mark must stand before «{after}» in «{text}».");
        }
    }

    private static string MarkStem(string format) => format[..format.IndexOf("{0}", StringComparison.Ordinal)].TrimEnd();
}
