using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-020 AC-005, AC-016 (spec FR-005, FR-007, I-2, I-3; entity model §4): a teacher is "Прізвище Ім'я" from the profile,
/// the one part present, the full email, or "no name" — exactly as stored, in the rows and in the teacher choice alike;
/// two choices with the same label carry their emails.
/// </summary>
public sealed class CourseListPersonNameTests
{
    private static Task<CourseListPageResult> RunAsync(FakeCourseListSource source) =>
        new GetCourseListQuery(source, new SchoolTimeZone(CourseListTestData.Kyiv)).ExecuteAsync(
            new CourseListRequest([], [], [], []),
            TestContext.Current.CancellationToken);

    /// <summary>One course taught by one person with these parts; returns the name in the row and in the choice.</summary>
    private static async Task<(CourseListPersonName Row, CourseListPersonName Choice)> NameOfAsync(
        string? surname, string? givenName, string? email)
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        source.AddTeacher(course, source.AddPerson(surname, givenName, email));

        var result = await RunAsync(source);

        var row = Assert.Single(Assert.Single(Assert.IsType<CourseListResult>(result.Page.List).Rows).Teachers);
        var choice = Assert.Single(result.Page.TeacherChoice).Name;
        return (row, choice);
    }

    public static TheoryData<string?, string?, string?, CourseListPersonNameKind, string?> Cases => new()
    {
        // FR-007 rule 1: both parts, surname first, one space.
        { "Шевченко", "Тарас", "t.shevchenko@school.example", CourseListPersonNameKind.ProfileName, "Шевченко Тарас" },

        // Rule 1: one part alone.
        { "Шевченко", null, "t.shevchenko@school.example", CourseListPersonNameKind.ProfileName, "Шевченко" },
        { null, "Тарас", "t.shevchenko@school.example", CourseListPersonNameKind.ProfileName, "Тарас" },

        // Entity model §4: an empty part is absent.
        { "", "Тарас", null, CourseListPersonNameKind.ProfileName, "Тарас" },

        // Rule 2: the full stored email (I-2), not its local part.
        { null, null, "t.shevchenko@school.example", CourseListPersonNameKind.Email, "t.shevchenko@school.example" },
        { "", "", "T.Shevchenko@School.Example", CourseListPersonNameKind.Email, "T.Shevchenko@School.Example" },

        // Rule 3: nothing — no text; the view renders "teacher without a name".
        { null, null, null, CourseListPersonNameKind.NoName, null },
        { "", "", "", CourseListPersonNameKind.NoName, null },

        // NFR-073, §3 v83: never corrected, re-cased or translated.
        { "де ла Крус", "марія", null, CourseListPersonNameKind.ProfileName, "де ла Крус марія" },
        { "O'Neil", "Ґанна", null, CourseListPersonNameKind.ProfileName, "O'Neil Ґанна" },
    };

    /// <summary>AC-005, FR-007: the rule, identical in the row and in the choice.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheName_FollowsTheRule_InTheRowAndTheChoice(
        string? surname, string? givenName, string? email, CourseListPersonNameKind kind, string? text)
    {
        var (row, choice) = await NameOfAsync(surname, givenName, email);

        var expected = new CourseListPersonName(kind, text);
        Assert.Equal(expected, row);
        Assert.Equal(expected, choice);
    }

    /// <summary>FR-005: the choice keeps the port's order and lists a teacher of several courses once.</summary>
    [Fact]
    public async Task TheChoice_ListsEachTeacherOnce_InThePortsOrder()
    {
        var source = new FakeCourseListSource();
        var physics = source.AddCourse("Фізика");
        var chemistry = source.AddCourse("Хімія");
        var shevchenko = source.AddPerson("Шевченко", "Тарас");
        var ukrainka = source.AddPerson("Українка", "Леся");
        var noName = source.AddPerson();
        source.AddTeacher(physics, shevchenko);
        source.AddTeacher(chemistry, shevchenko);
        source.AddTeacher(chemistry, ukrainka);
        source.AddTeacher(chemistry, noName);

        var result = await RunAsync(source);

        Assert.Equal([ukrainka, shevchenko, noName], result.Page.TeacherChoice.Select(t => t.ParticipantId));
    }

    /// <summary>I-3: two choices with the same label each carry their email; a unique label carries none.</summary>
    [Fact]
    public async Task SameLabelledChoices_CarryTheirEmails()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        var first = source.AddPerson("Коваль", "Олена", "o.koval1@school.example");
        var second = source.AddPerson("Коваль", "Олена", "o.koval2@school.example");
        var unique = source.AddPerson("Шевченко", "Тарас", "t.shevchenko@school.example");
        foreach (var teacher in new[] { first, second, unique })
        {
            source.AddTeacher(course, teacher);
        }

        var result = await RunAsync(source);

        var choice = result.Page.TeacherChoice.ToDictionary(t => t.ParticipantId);
        Assert.Equal("o.koval1@school.example", choice[first].DisambiguatingEmail);
        Assert.Equal("o.koval2@school.example", choice[second].DisambiguatingEmail);
        Assert.Null(choice[unique].DisambiguatingEmail);
    }

    /// <summary>I-3: of two same-labelled choices, one without an email is distinguished by nothing further.</summary>
    [Fact]
    public async Task ASameLabelledChoiceWithoutAnEmail_CarriesNone()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        var withEmail = source.AddPerson("Коваль", "Олена", "o.koval@school.example");
        var withoutEmail = source.AddPerson("Коваль", "Олена");
        source.AddTeacher(course, withEmail);
        source.AddTeacher(course, withoutEmail);

        var result = await RunAsync(source);

        var choice = result.Page.TeacherChoice.ToDictionary(t => t.ParticipantId);
        Assert.Equal("o.koval@school.example", choice[withEmail].DisambiguatingEmail);
        Assert.Null(choice[withoutEmail].DisambiguatingEmail);
    }

    /// <summary>I-3: rows are not changed — a row lists names only, even for same-labelled teachers.</summary>
    [Fact]
    public async Task Rows_ListNamesOnly_EvenForSameLabelledTeachers()
    {
        var source = new FakeCourseListSource();
        var course = source.AddCourse("Фізика");
        source.AddTeacher(course, source.AddPerson("Коваль", "Олена", "o.koval1@school.example"));
        source.AddTeacher(course, source.AddPerson("Коваль", "Олена", "o.koval2@school.example"));

        var result = await RunAsync(source);

        var teachers = Assert.Single(Assert.IsType<CourseListResult>(result.Page.List).Rows).Teachers;
        Assert.All(teachers, t => Assert.Equal(new CourseListPersonName(CourseListPersonNameKind.ProfileName, "Коваль Олена"), t));
    }
}
