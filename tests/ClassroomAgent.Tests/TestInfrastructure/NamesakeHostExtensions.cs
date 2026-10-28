using System.Net;
using ClosedXML.Excel;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-066 over HTTP and PostgreSQL (TC-2): one course with the students a test names, one graded item and its
/// teachers, written by SQL so nothing depends on synchronization (TC-4: nothing reaches Google); and the readers of
/// the student name of every row of a page or a file.
/// </summary>
public static class NamesakeHostExtensions
{
    private const int ProgramHeadingRow = 6;

    private static readonly DateTimeOffset SeenAt = JournalTestData.Period.StartUtc.AddDays(-30);

    /// <summary>The default single teacher of the course.</summary>
    public static IReadOnlyList<(string FullName, string? Surname, string Email)> OneTeacher { get; } =
        [("Test Teacher Namesakes", "Namesaketeacher", CourseTestData.Email("teacher.namesakes"))];

    /// <summary>OD-002 a: two teachers of one course with the same name and different emails.</summary>
    public static IReadOnlyList<(string FullName, string? Surname, string Email)> TwoSameTeachers { get; } =
    [
        ("Test Teacher Same", "Sameteacher", CourseTestData.Email("teacher.same.a")),
        ("Test Teacher Same", "Sameteacher", CourseTestData.Email("teacher.same.b")),
    ];

    /// <summary>
    /// A teacher with no profile name: the report labels them by the part of the email before <c>@</c> whichever name
    /// source is chosen, so two exports of one course that differ only in the name source label them alike.
    /// </summary>
    public static IReadOnlyList<(string FullName, string? Surname, string Email)> TeacherWithoutProfileName { get; } =
        [("Test Teacher Nameless", null, CourseTestData.Email("teacher.nameless"))];

    /// <summary>
    /// The namesake course: the teachers, the graded item and <paramref name="students"/> in the order given (the
    /// participant ids follow it), each with the grade <see cref="NamesakeWorld.Grade"/>. A student with
    /// <see cref="NamesakeStudent.LeftLastSeen"/> is off the roster since then. Returns the course id.
    /// </summary>
    public static async Task<long> SeedNamesakeCourseAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        IReadOnlyList<NamesakeStudent> students,
        IReadOnlyList<(string FullName, string? Surname, string Email)>? teachers = null)
    {
        // The retention purge runs at every host start (US-037); seeding after its first run keeps it from racing the inserts.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);
        var courseId = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(66), name: NamesakeWorld.CourseName, createdAt: SeenAt);

        var teacherOrdinal = 690;
        foreach (var (fullName, surname, email) in teachers ?? OneTeacher)
        {
            var teacher = await CourseRows.InsertParticipantAsync(
                host, cancellationToken, googleUserId: CourseTestData.UserId(teacherOrdinal++), email: email,
                fullName: fullName, surname: surname, givenName: surname is null ? null : "Test");
            await CourseRows.InsertMembershipAsync(
                host, courseId, teacher, cancellationToken, role: CourseTestData.Roles.Teacher, firstSeenAt: SeenAt);
        }

        var graded = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(661), title: NamesakeWorld.GradedTitle,
            itemDate: NamesakeWorld.GradedDate, dueAt: NamesakeWorld.GradedDate, maxPoints: NamesakeWorld.GradedMaximum);

        foreach (var student in students)
        {
            var id = await CourseRows.InsertParticipantAsync(
                host, cancellationToken, googleUserId: CourseTestData.UserId(student.Ordinal), email: student.Email,
                fullName: student.FullName, surname: student.Surname, givenName: student.GivenName);
            await CourseRows.InsertMembershipAsync(
                host, courseId, id, cancellationToken, firstSeenAt: SeenAt,
                lastSeenAt: student.LeftLastSeen ?? host.Time.GetUtcNow(), onRoster: student.LeftLastSeen is null);
            await CourseWorkRows.InsertSubmissionAsync(
                host, graded, id, cancellationToken, googleId: CourseWorkTestData.SubmissionId(student.Ordinal),
                state: CourseWorkTestData.StateCodes.Returned, assignedGrade: NamesakeWorld.Grade);
        }

        return courseId;
    }

    // ---- HTML ---------------------------------------------------------------------------------------------------

    /// <summary>The text of every body row's heading of the table with that id, in page order.</summary>
    public static IReadOnlyList<string> RowHeadings(string html, string tableId) =>
        DataMarksHostExtensions.BodyRows(DataMarksHostExtensions.Table(html, tableId))
            .Select(DataMarksHostExtensions.RowHeading)
            .ToList();

    // ---- Workbooks ----------------------------------------------------------------------------------------------

    /// <summary>The program layout's student name cells (column A below the heading row), top to bottom.</summary>
    public static IReadOnlyList<IXLCell> ProgramNames(IXLWorksheet sheet) =>
        sheet.Column(1).CellsUsed().Where(c => c.Address.RowNumber > ProgramHeadingRow).ToList();

    /// <summary>The grading sheets of a blank («Оцінювання», numbered when there are several copies), in file order.</summary>
    public static IReadOnlyList<IXLWorksheet> BlankGradingSheets(XLWorkbook workbook) =>
        workbook.Worksheets.Where(s => s.Name.StartsWith(GroupLessonsBlank.GradingSheet, StringComparison.Ordinal)).ToList();

    /// <summary>A blank grading sheet's student name cells (column B from row 9) that hold text, top to bottom.</summary>
    public static IReadOnlyList<IXLCell> BlankNames(IXLWorksheet sheet) =>
        sheet.Column(GroupLessonsBlank.StudentNameColumn).CellsUsed()
            .Where(c => c.Address.RowNumber >= GroupLessonsBlank.FirstStudentRow && c.GetString().Length > 0)
            .ToList();

    /// <summary>Every text of every cell of every sheet of a file.</summary>
    public static IEnumerable<string> AllTexts(XLWorkbook workbook) =>
        workbook.Worksheets.SelectMany(s => s.CellsUsed()).Select(c => c.GetString());

    /// <summary>A started host with a Dean signed in, as US-060's tests start it.</summary>
    public static Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database, string language, CancellationToken cancellationToken) =>
        DataMarksHostExtensions.StartAsync(database, language, cancellationToken);

    /// <summary>Asserts a 200 page and that nothing reached Google (TC-4).</summary>
    public static void AssertServed(InstallationTestHost host, HttpStatusCode status)
    {
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, host.GoogleCallCount());
    }
}
