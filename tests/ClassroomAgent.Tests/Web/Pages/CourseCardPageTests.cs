using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;
using Cc = ClassroomAgent.Tests.TestInfrastructure.CourseCardHostExtensions;
using Data = ClassroomAgent.Tests.TestInfrastructure.CourseCardTestData;
using Fields = ClassroomAgent.Tests.TestInfrastructure.CourseCardTestData.Fields;
using Keys = ClassroomAgent.Tests.TestInfrastructure.CourseCardTestData.Keys;
using Markup = ClassroomAgent.Tests.TestInfrastructure.CourseCardTestData.Markup;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-021 over HTTP: the details, the omitted details, the three sections with their counts, the owner mark, the leavers,
/// the name rule, the order, the links, the address and the language switcher, the 404 page, the empty sections, the
/// description as text and HTML encoding (AC-001 … AC-009, AC-014 … AC-016; FR-001 … FR-011; spec S-06, S-08). Rows are
/// seeded by SQL; the markup hooks are <see cref="CourseCardTestData.Markup"/>.
/// </summary>
public sealed class CourseCardPageTests(PostgreSqlFixture database)
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk");

    public static TheoryData<string> NotFoundValues
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var value in Data.MalformedMatchingTheRoute.Concat(Data.NotMatchingTheRoute).Append("8765432109"))
            {
                data.Add(value);
            }

            return data;
        }
    }

    private static string ShortDate(int year, int month, int day) =>
        new DateTime(year, month, day).ToString(Uk.DateTimeFormat.ShortDatePattern, Uk);

    private async Task<(InstallationTestHost Host, FormClient Client, CourseCardHostExtensions.SeededCard World)> StartSeededAsync(
        CancellationToken ct, Actor actor = Actor.Admin)
    {
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        var world = await host.SeedCourseCardAsync(ct);
        return (host, client, world);
    }

    /// <summary>AC-001, AC-014, FR-002: every shown detail with its label, Kyiv times, no hidden Google column.</summary>
    [Fact]
    public async Task TheCard_ShowsTheDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        NoStore.Assert(page, "the course card");
        Assert.Equal(Cc.PhysicsName, Cc.FieldText(page.Body, Fields.Name));
        Assert.Equal(Cc.PhysicsSection, Cc.FieldText(page.Body, Fields.Section));
        Assert.Equal(host.Text("Courses.Status.Active", "uk"), Cc.FieldText(page.Body, Fields.Status));
        Assert.Equal(Cc.PhysicsRoom, Cc.FieldText(page.Body, Fields.Room));
        Assert.Equal(Cc.PhysicsHeading, Cc.FieldText(page.Body, Fields.DescriptionHeading));
        Assert.Equal(
            Cc.PhysicsDescription,
            (Cc.FieldText(page.Body, Fields.Description) ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal));
        var created = Cc.FieldText(page.Body, Fields.CreatedAt) ?? string.Empty;
        Assert.Contains(ShortDate(2025, 9, 1), created, StringComparison.Ordinal);
        Assert.Contains("09:15", created, StringComparison.Ordinal);
        var updated = Cc.FieldText(page.Body, Fields.UpdatedAt) ?? string.Empty;
        Assert.Contains(ShortDate(2026, 4, 1), updated, StringComparison.Ordinal);
        Assert.Contains("01:30", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("22:30", updated, StringComparison.Ordinal);
        var text = Mc.VisibleText(page.Body);
        foreach (var key in new[]
                 {
                     Keys.FieldSection, Keys.FieldStatus, Keys.FieldRoom, Keys.FieldDescriptionHeading, Keys.FieldDescription,
                     Keys.FieldCreatedAt, Keys.FieldUpdatedAt,
                 })
        {
            Assert.Contains(host.Text(key, "uk"), text, StringComparison.Ordinal);
        }

        foreach (var hidden in new[]
                 {
                     Cc.AlternateLinkMarker, Cc.TeacherFolderMarker, Cc.CalendarMarker, "classroom.google.com",
                     CourseTestData.CourseId(1), CourseTestData.UserId(1),
                 })
        {
            Assert.DoesNotContain(hidden, page.Body, StringComparison.Ordinal);
        }

        Assert.Equal(0, host.GoogleCallCount());
    }

    /// <summary>AC-001, FR-002, I-1: a detail Google did not give — or gave blank — is omitted with its label.</summary>
    [Fact]
    public async Task AbsentDetails_AreOmitted_WithTheirLabels()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await CourseRows.InsertCourseAsync(host, ct, name: "Порожній курс", section: "   ", description: string.Empty);

        var page = await client.GetAsync(Data.Path(course), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        foreach (var field in Fields.Optional)
        {
            Assert.Null(Cc.FieldText(page.Body, field));
        }

        var text = Mc.VisibleText(page.Body);
        foreach (var key in Keys.OptionalFieldLabels)
        {
            Assert.DoesNotContain(host.Text(key, "uk"), text, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-002, FR-003: the current teachers and students and the leavers, each with its count; no other course's people.</summary>
    [Fact]
    public async Task TheSections_ShowTheCurrentTeachersAndStudents_WithTheirCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(2, Cc.Count(page.Body, Markup.TeachersId));
        Assert.Equal(7, Cc.Count(page.Body, Markup.StudentsId));
        Assert.Equal(3, Cc.Count(page.Body, Markup.LeaversId));
        var text = Mc.VisibleText(page.Body);
        foreach (var key in new[] { Keys.SectionTeachers, Keys.SectionStudents, Keys.SectionLeavers })
        {
            Assert.Contains(host.Text(key, "uk"), text, StringComparison.Ordinal);
        }

        var teachers = Cc.Rows(page.Body, Markup.TeachersId);
        Assert.Equal(2, teachers.Count);
        Assert.Contains(teachers, r => r.Contains(Cc.UkrainkaLabel, StringComparison.Ordinal));
        Assert.Contains(teachers, r => r.Contains(Cc.ShevchenkoLabel, StringComparison.Ordinal));
        Assert.DoesNotContain("Коваль", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Бондаренко", page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-003, FR-004: the owner is marked on the owner's teacher row only.</summary>
    [Fact]
    public async Task TheOwner_IsMarkedOnTheOwnersRowOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var texts = Cc.Rows(page.Body, Markup.TeachersId);
        var html = Cc.RowHtml(page.Body, Markup.TeachersId);
        var owner = texts.ToList().FindIndex(r => r.Contains(Cc.ShevchenkoLabel, StringComparison.Ordinal));
        var other = texts.ToList().FindIndex(r => r.Contains(Cc.UkrainkaLabel, StringComparison.Ordinal));
        Assert.True(owner >= 0 && other >= 0, "Both teacher rows must be present.");
        Assert.Equal(host.Text(Keys.Owner, "uk"), Cc.OwnerMark(html[owner]));
        Assert.Null(Cc.OwnerMark(html[other]));
        Assert.DoesNotContain(Markup.OwnerAttribute, Cc.Section(page.Body, Markup.StudentsId) ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Markup.OwnerAttribute, Cc.Section(page.Body, Markup.LeaversId) ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>AC-003: a course with no owner marks nobody.</summary>
    [Fact]
    public async Task ACourseWithNoOwner_MarksNoOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Chemistry), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Cc.KovalLabel, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Markup.OwnerAttribute, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-003: an owner who is only a leaver is not marked among the current teachers.</summary>
    [Fact]
    public async Task ACourseWhoseOwnerIsNotACurrentTeacher_MarksNoOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;
        var course = await CourseRows.InsertCourseAsync(
            host, ct, googleId: CourseTestData.CourseId(5), name: "Інший курс", ownerGoogleId: CourseTestData.UserId(3));
        await CourseRows.InsertMembershipAsync(host, course, world.Franko, ct, CourseTestData.Roles.Teacher, onRoster: false);
        await host.AddTeacherAsync(course, world.Ukrainka, ct);

        var page = await client.GetAsync(Data.Path(course), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(Cc.UkrainkaLabel, page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Markup.OwnerAttribute, page.Body, StringComparison.Ordinal);
    }

    /// <summary>AC-004, AC-014, FR-006: a leaver shows the role and the last-seen date in Kyiv; none is also current.</summary>
    [Fact]
    public async Task TheLeavers_ShowRoleAndLastSeenDate_AndAreNotCurrent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var leavers = Cc.Rows(page.Body, Markup.LeaversId);
        Assert.Equal(3, leavers.Count);
        Assert.Contains(Cc.MelnykLabel, leavers[0], StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.RoleStudent, "uk"), leavers[0], StringComparison.Ordinal);
        Assert.Contains(ShortDate(2026, 2, 10), leavers[0], StringComparison.Ordinal);
        Assert.Contains(Cc.FrankoLabel, leavers[1], StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.RoleTeacher, "uk"), leavers[1], StringComparison.Ordinal);
        Assert.Contains(ShortDate(2026, 4, 1), leavers[1], StringComparison.Ordinal);
        Assert.DoesNotContain(ShortDate(2026, 3, 31), leavers[1], StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.NoName, "uk"), leavers[2], StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.RoleStudent, "uk"), leavers[2], StringComparison.Ordinal);
        Assert.Contains(ShortDate(2026, 3, 15), leavers[2], StringComparison.Ordinal);
        foreach (var current in Cc.Rows(page.Body, Markup.TeachersId).Concat(Cc.Rows(page.Body, Markup.StudentsId)))
        {
            Assert.DoesNotContain(Cc.FrankoLabel, current, StringComparison.Ordinal);
            Assert.DoesNotContain(Cc.MelnykLabel, current, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-005, FR-005: a person is shown by profile name, else by email, else by the placeholder; the email is shown too.</summary>
    [Fact]
    public async Task APerson_IsShownByProfileName_OrEmail_WithTheEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var teacher = Cc.Rows(page.Body, Markup.TeachersId).Single(r => r.Contains(Cc.ShevchenkoLabel, StringComparison.Ordinal));
        Assert.Contains(Cc.ShevchenkoEmail, teacher, StringComparison.Ordinal);
        var students = Cc.Rows(page.Body, Markup.StudentsId);
        Assert.Contains(students, r => r.Contains(Cc.EmailOnlyStudent, StringComparison.Ordinal));
        Assert.Contains(host.Text(Keys.NoName, "uk"), students[^1], StringComparison.Ordinal);
    }

    /// <summary>AC-006, AC-015, FR-007, FR-008: each section is in name order on one page, without paging.</summary>
    [Fact]
    public async Task EachSection_IsInNameOrder_OnOnePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var students = Cc.Rows(page.Body, Markup.StudentsId);
        Assert.Equal(7, students.Count);
        for (var i = 0; i < Cc.StudentLabelsInOrder.Count; i++)
        {
            Assert.Contains(Cc.StudentLabelsInOrder[i], students[i], StringComparison.Ordinal);
        }

        Assert.Contains(Cc.EmailOnlyStudent, students[5], StringComparison.Ordinal);
        Assert.Contains(host.Text(Keys.NoName, "uk"), students[6], StringComparison.Ordinal);
        var teachers = Cc.Rows(page.Body, Markup.TeachersId);
        Assert.Contains(Cc.UkrainkaLabel, teachers[0], StringComparison.Ordinal);
        Assert.Contains(Cc.ShevchenkoLabel, teachers[1], StringComparison.Ordinal);
        Assert.DoesNotContain(Cc.Hrefs(page.Body), h => h.Contains("page=", StringComparison.Ordinal));
    }

    /// <summary>AC-007, FR-009: the card links to the journal, the Meet reports and the course list; no Classroom link.</summary>
    [Fact]
    public async Task TheCard_LinksToTheJournal_TheMeetReports_AndTheCourseList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Physics), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var hrefs = Cc.Hrefs(page.Body);
        Assert.Contains(Data.JournalLink(world.Physics), hrefs);
        Assert.Contains(Data.MeetReportsLink(world.Physics), hrefs);
        Assert.Contains(Data.ListPath, hrefs);
        var text = Mc.VisibleText(page.Body);
        foreach (var key in new[] { Keys.LinkJournal, Keys.LinkMeetReports, Keys.LinkCourses })
        {
            Assert.Contains(host.Text(key, "uk"), text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(hrefs, h => h.Contains("classroom.google.com", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>AC-007: the course list links each course name to its card.</summary>
    [Fact]
    public async Task TheCourseList_LinksEachCourseNameToItsCard()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(CourseListTestData.Url(status: "all"), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var hrefs = Cc.Hrefs(page.Body);
        Assert.Contains(Data.Path(world.Physics), hrefs);
        Assert.Contains(Data.Path(world.Chemistry), hrefs);
        Assert.Matches(
            new Regex(
                $@"<a[^>]*href=""/workspace/courses/{world.Physics}""[^>]*>\s*{Cc.PhysicsName}\s*</a>",
                RegexOptions.Singleline),
            page.Body);
    }

    /// <summary>FR-010, I-6, S-06: the address carries the id only; the switcher returns to the canonical card; nothing is echoed.</summary>
    [Fact]
    public async Task TheAddress_CarriesTheIdOnly_AndTheSwitcherReturnsToTheCard()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path("00" + Data.Id(world.Physics)) + "?evil=zz-echo-zz", ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(Data.Path(world.Physics), UiLanguageTestData.RenderedReturnPath(page.Body));
        Assert.DoesNotContain("zz-echo-zz", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", page.Body, StringComparison.Ordinal);
        foreach (var href in Cc.Hrefs(page.Body))
        {
            var decoded = Uri.UnescapeDataString(href);
            Assert.DoesNotContain("@", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Шевченко", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain("Українка", decoded, StringComparison.Ordinal);
            Assert.DoesNotContain(CourseTestData.UserId(1), decoded, StringComparison.Ordinal);
            Assert.DoesNotContain(CourseTestData.CourseId(1), decoded, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-008, FR-011: a malformed, unmatched or unknown id is the ordinary 404 page, with nothing of the course and no echo.</summary>
    [Theory]
    [MemberData(nameof(NotFoundValues))]
    public async Task AMalformedOrUnknownId_IsTheOrdinaryNotFoundPage(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        // Control: the card route exists, so the 404 below is the card's answer, not a missing route's.
        var control = await client.GetAsync(Data.Path(world.Physics), ct);
        Assert.Equal(HttpStatusCode.OK, control.Status);

        var page = await client.GetAsync(Data.Path(value), ct);

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.Contains(host.Text("Error.NotFound", "uk"), page.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Cc.PhysicsName, page.Body, StringComparison.Ordinal);
        if (value.Length >= 5)
        {
            Assert.DoesNotContain(value, page.Body, StringComparison.Ordinal);
        }
    }

    /// <summary>AC-009, FR-008: a course with nobody shows each section's own message and no table.</summary>
    [Fact]
    public async Task EmptySections_ShowTheirOwnMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await host.InsertEmptyCourseAsync(1, "Порожній курс", ct);

        var page = await client.GetAsync(Data.Path(course), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        foreach (var (section, key) in new[]
                 {
                     (Markup.TeachersId, Keys.EmptyTeachers), (Markup.StudentsId, Keys.EmptyStudents),
                     (Markup.LeaversId, Keys.EmptyLeavers),
                 })
        {
            Assert.Equal(host.Text(key, "uk"), Cc.EmptyMessage(page.Body, section));
            Assert.False(Cc.HasTable(page.Body, section));
            Assert.Equal(0, Cc.Count(page.Body, section));
        }
    }

    /// <summary>AC-009, FR-008: only the empty section shows the message; the others keep their tables.</summary>
    [Fact]
    public async Task OnlyTheEmptySection_ShowsItsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, world) = await StartSeededAsync(ct);
        await using var _host = host;

        var page = await client.GetAsync(Data.Path(world.Chemistry), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(host.Text(Keys.EmptyLeavers, "uk"), Cc.EmptyMessage(page.Body, Markup.LeaversId));
        Assert.False(Cc.HasTable(page.Body, Markup.LeaversId));
        foreach (var section in new[] { Markup.TeachersId, Markup.StudentsId })
        {
            Assert.True(Cc.HasTable(page.Body, section));
            Assert.Null(Cc.EmptyMessage(page.Body, section));
        }
    }

    /// <summary>AC-016, S-08: the description is text — markup is not interpreted and the line break is kept without a tag.</summary>
    [Fact]
    public async Task TheDescription_IsText_WithItsLineBreak()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        const string description = "<b>zz-bold</b>\nдругий рядок";
        var course = await CourseRows.InsertCourseAsync(host, ct, name: "Курс з описом", description: description);

        var page = await client.GetAsync(Data.Path(course), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("<b>zz-bold</b>", page.Body, StringComparison.Ordinal);
        var inner = Cc.FieldInnerHtml(page.Body, Fields.Description) ?? string.Empty;
        Assert.DoesNotContain("<br", inner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", inner, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            description,
            (Cc.FieldText(page.Body, Fields.Description) ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>S-08: a course name and a teacher name are rendered HTML-encoded, never as markup.</summary>
    [Fact]
    public async Task GoogleData_IsHtmlEncoded()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Admin, ct);
        await using var _host = host;
        await host.WaitForPurgeRunsAsync(1, ct);
        var course = await host.InsertCourseAsync(1, "<i>zz-name</i>", ct);
        await host.AddTeacherAsync(course, await host.InsertPersonAsync(1, "<script>zz</script>", "Ім'я", null, ct), ct);

        var page = await client.GetAsync(Data.Path(course), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.DoesNotContain("<i>zz-name</i>", page.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>zz</script>", page.Body, StringComparison.Ordinal);
        Assert.Contains("<i>zz-name</i>", page.Text, StringComparison.Ordinal);
    }
}
