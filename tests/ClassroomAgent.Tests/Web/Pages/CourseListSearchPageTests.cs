using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-070 over HTTP, the course list on the shared matching rules: words in any order over name and section, case and
/// apostrophe forms ignored, the search ANDed with the status and teacher filters, the refusals of VR-001 and the field's
/// attributes (AC-002, AC-007, AC-010; openapi <c>getCourseList</c>).
/// </summary>
public sealed class CourseListSearchPageTests(PostgreSqlFixture database)
{
    private async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(CancellationToken ct)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await host.WaitForPurgeRunsAsync(1, ct);
        return (host, client);
    }

    /// <summary>AC-002, FR-002: the words may come in any order, in any case, across name and section.</summary>
    [Theory]
    [InlineData("10-б математ")]
    [InlineData("математ 10-б")]
    [InlineData("МАТЕМАТИКА")]
    public async Task TheWords_AreMatchedInAnyOrderAndCase_OverNameAndSection(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        var math = await host.InsertCourseAsync(1, "Математика", ct, section: "10-Б");
        await host.InsertCourseAsync(2, "Хімія", ct, section: "11-А");

        var page = await client.GetAsync(CourseListTestData.Url(q: text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([math], Cl.RowIds(page.Body));
    }

    /// <summary>AC-002, FR-002: every word must match; a word that matches nothing leaves an empty list and the no-match message.</summary>
    [Theory]
    [InlineData("математика 11")]
    [InlineData("матемтика")]
    public async Task AWordThatMatchesNothing_ListsNoCourse_AndShowsTheNoMatchMessage(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.InsertCourseAsync(1, "Математика", ct, section: "10-Б");
        await host.InsertCourseAsync(2, "Хімія", ct, section: "11-А");

        var page = await client.GetAsync(CourseListTestData.Url(q: text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(Cl.RowIds(page.Body));
        Assert.Equal(host.Text(Ls.Keys.CoursesNoMatch, "uk"), Html.ElementText(page.Body, CourseListTestData.Markup.EmptyId));
    }

    /// <summary>AC-002, FR-002: the typographic apostrophe forms find a course stored with the plain one.</summary>
    [Theory]
    [InlineData("об’єкти")]
    [InlineData("обʼєкти")]
    public async Task ApostropheForms_AreOne(string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        var objects = await host.InsertCourseAsync(1, "Об'єкти", ct);
        await host.InsertCourseAsync(2, "Хімія", ct);

        var page = await client.GetAsync(CourseListTestData.Url(q: text), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal([objects], Cl.RowIds(page.Body));
    }

    /// <summary>AC-002, FR-002: the status and teacher filters apply together with the search, by AND.</summary>
    [Fact]
    public async Task TheStatusAndTeacherFilters_AreAndedWithTheSearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        var taught = await host.InsertCourseAsync(1, "Фізика", ct);
        var archived = await host.InsertCourseAsync(2, "Фізика", ct, CourseTestData.States.Archived);
        await host.InsertCourseAsync(3, "Фізика", ct);
        await host.InsertCourseAsync(4, "Хімія", ct);
        var teacher = await host.InsertPersonAsync(1, "Коваль", "Олена", CourseTestData.Email("zz.teacher"), ct);
        await host.AddTeacherAsync(taught, teacher, ct);

        var byStatus = await client.GetAsync(CourseListTestData.Url(status: "archived", q: "фізика"), ct);
        var byTeacher = await client.GetAsync(CourseListTestData.Url(teacherId: CourseListTestData.Id(teacher), q: "фізика"), ct);
        var byTeacherOther = await client.GetAsync(CourseListTestData.Url(teacherId: CourseListTestData.Id(teacher), q: "хімія"), ct);

        Assert.Equal([archived], Cl.RowIds(byStatus.Body));
        Assert.Equal([taught], Cl.RowIds(byTeacher.Body));
        Assert.Empty(Cl.RowIds(byTeacherOther.Body));
    }

    /// <summary>AC-007, VR-001: a refused text answers 400 with the message, no list, an empty field, and the text nowhere in the page.</summary>
    [Theory]
    [MemberData(nameof(Ls.RefusedTexts), MemberType = typeof(Ls))]
    public async Task ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed(string fragment, string key)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.InsertCourseAsync(1, "Математика", ct);

        var page = await client.GetAsync(CourseListTestData.Path + "?" + fragment, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Contains(host.Text(key, "uk"), Cl.ErrorMessages(page.Body));
        Assert.Empty(Cl.RowIds(page.Body));
        Assert.Null(Ls.FieldValue(page.Body, Ls.CourseField));
        Assert.DoesNotContain(Ls.Needle, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-010, FR-007: the field in both languages: label from the translation, type, length limit, no remembering.</summary>
    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    public async Task TheField_HasItsLabelAndAttributes(string language)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await StartAsync(ct);
        await using var _host = host;
        await host.InsertCourseAsync(1, "Математика", ct);
        if (language == UiLanguageTestData.English)
        {
            await client.ChooseLanguageAsync(CourseListTestData.Path, UiLanguageTestData.English, ct);
        }

        var page = await client.GetAsync(CourseListTestData.Path, ct);

        var tag = Ls.TagOf(page.Body, Ls.CourseField);
        Assert.NotNull(tag);
        Assert.Equal("off", Ls.AttributeOf(tag, "autocomplete"));
        Assert.Equal("search", Ls.AttributeOf(tag, "type"));
        Assert.Equal("100", Ls.AttributeOf(tag, "maxlength"));
        Assert.Equal(host.Text(Ls.Keys.CoursesSearch, language), Ls.LabelOf(page.Body, Ls.CourseField));
    }
}
