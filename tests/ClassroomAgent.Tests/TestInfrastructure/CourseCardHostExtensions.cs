using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Cl = ClassroomAgent.Tests.TestInfrastructure.CourseListHostExtensions;
using Markup = ClassroomAgent.Tests.TestInfrastructure.CourseCardTestData.Markup;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-021 host seeding and markup readers: one fully described course with current teachers (the owner among them),
/// current students in a known Ukrainian order, three leavers (a former teacher, a former student and a never-rostered
/// submitter), and a second course whose people must never appear on the first one's card. Rows are inserted by SQL so
/// the tests do not depend on the production code under test (TC-2). Every name and email is invented (TC-4).
/// </summary>
public static partial class CourseCardHostExtensions
{
    public const string PhysicsName = "Фізика";

    public const string PhysicsSection = "9-Б";

    public const string PhysicsRoom = "Кабінет 214";

    public const string PhysicsHeading = "Про курс";

    /// <summary>Two lines; the line break must survive rendering (spec FR-002).</summary>
    public const string PhysicsDescription = "Рядок перший\nРядок другий";

    /// <summary>Markers set on the course's hidden columns; none may appear on the card (spec FR-002, S-10).</summary>
    public const string AlternateLinkMarker = "zz-alternate-link";

    public const string TeacherFolderMarker = "zz-teacher-folder";

    public const string CalendarMarker = "zz-calendar-id";

    public const string ChemistryName = "Хімія";

    public const string ShevchenkoLabel = "Шевченко Тарас";

    public const string UkrainkaLabel = "Українка Леся";

    public const string FrankoLabel = "Франко Іван";

    public const string MelnykLabel = "Мельник Петро";

    public const string KovalLabel = "Коваль Олена";

    public const string ChemistryStudentLabel = "Бондаренко Ольга";

    /// <summary>The five students of spec AC-015, in the expected order.</summary>
    public static IReadOnlyList<string> StudentLabelsInOrder { get; } =
        ["Андрієнко Марія", "Гнатюк Ірина", "Ґудзь Петро", "Іваненко Олег", "Ігнатенко Анна"];

    public static string ShevchenkoEmail => CourseTestData.Email("t.shevchenko");

    public static string UkrainkaEmail => CourseTestData.Email("l.ukrainka");

    public static string FrankoEmail => CourseTestData.Email("i.franko");

    public static string MelnykEmail => CourseTestData.Email("p.melnyk");

    /// <summary>A current student with an email and no name — shown by the email (spec FR-005).</summary>
    public static string EmailOnlyStudent => CourseTestData.Email("zz.email.only");

    /// <summary>22:30 UTC on 31 March 2026 — 01:30 on 1 April in Kyiv (spec AC-014).</summary>
    public static readonly DateTimeOffset PhysicsUpdateTime = new(2026, 3, 31, 22, 30, 0, TimeSpan.Zero);

    /// <summary>06:15:42 UTC on 1 September 2025 — 09:15 in Kyiv, seconds dropped.</summary>
    public static readonly DateTimeOffset PhysicsCreationTime = new(2025, 9, 1, 6, 15, 42, TimeSpan.Zero);

    /// <summary>Франко last seen: 22:30 UTC on 31 March 2026 — 1 April 2026 in Kyiv (spec AC-014).</summary>
    public static readonly DateTimeOffset FrankoLastSeen = new(2026, 3, 31, 22, 30, 0, TimeSpan.Zero);

    /// <summary>Мельник last seen: 10 February 2026.</summary>
    public static readonly DateTimeOffset MelnykLastSeen = new(2026, 2, 10, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The synchronization that created the submitter-only student: 15 March 2026.</summary>
    public static readonly DateTimeOffset SubmitterSeen = new(2026, 3, 15, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Every course text, name and email the world holds — none may reach a log line (SC-10).</summary>
    public static IReadOnlyList<string> SensitiveValues { get; } =
    [
        PhysicsName, PhysicsRoom, PhysicsHeading, "Рядок перший", ChemistryName, "Шевченко", "Українка", "Франко",
        "Мельник", "Андрієнко", "Гнатюк", "Ґудзь", "Іваненко", "Ігнатенко", "Коваль", "Бондаренко",
        "t.shevchenko@", "l.ukrainka@", "i.franko@", "p.melnyk@", "zz.email.only@", "student.",
        CourseTestData.UserId(1), CourseTestData.CourseId(1),
    ];

    /// <summary>The ids the world holds.</summary>
    public sealed record SeededCard(
        long Physics,
        long Chemistry,
        long Shevchenko,
        long Ukrainka,
        long Franko,
        long Melnyk,
        long Submitter,
        long Nameless,
        long EmailOnly);

    /// <summary>
    /// Фізика (active; section, room, heading, two-line description, both times; owner Шевченко): teachers Шевченко
    /// (owner) and Українка; students the five of AC-015 inserted out of order, one with an email only and one with
    /// nothing; leavers Франко (former teacher), Мельник (former student) and a submitter-only student. Хімія: teacher
    /// Коваль, student Бондаренко.
    /// </summary>
    public static async Task<SeededCard> SeedCourseCardAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var physics = await CourseRows.InsertCourseAsync(
            host,
            cancellationToken,
            googleId: CourseTestData.CourseId(1),
            name: PhysicsName,
            ownerGoogleId: CourseTestData.UserId(1),
            creationTime: PhysicsCreationTime,
            updateTime: PhysicsUpdateTime,
            section: PhysicsSection,
            description: PhysicsDescription,
            room: PhysicsRoom,
            descriptionHeading: PhysicsHeading);
        await host.ExecuteAsync(
            """
            UPDATE course
            SET alternate_link = 'https://classroom.google.com/c/zz-alternate-link',
                teacher_folder_id = 'zz-teacher-folder', teacher_folder_title = 'zz-teacher-folder',
                calendar_id = 'zz-calendar-id'
            WHERE id = @id
            """,
            cancellationToken,
            ("id", physics));
        var chemistry = await CourseRows.InsertCourseAsync(
            host, cancellationToken, googleId: CourseTestData.CourseId(2), name: ChemistryName);

        var shevchenko = await Cl.InsertPersonAsync(host, 1, "Шевченко", "Тарас", ShevchenkoEmail, cancellationToken);
        var ukrainka = await Cl.InsertPersonAsync(host, 2, "Українка", "Леся", UkrainkaEmail, cancellationToken);
        var franko = await Cl.InsertPersonAsync(host, 3, "Франко", "Іван", FrankoEmail, cancellationToken);
        var melnyk = await Cl.InsertPersonAsync(host, 4, "Мельник", "Петро", MelnykEmail, cancellationToken);
        var koval = await Cl.InsertPersonAsync(host, 5, "Коваль", "Олена", CourseTestData.Email("o.koval"), cancellationToken);
        var bondarenko = await Cl.InsertPersonAsync(host, 6, "Бондаренко", "Ольга", CourseTestData.Email("student.c"), cancellationToken);

        await Cl.AddTeacherAsync(host, physics, ukrainka, cancellationToken);
        await Cl.AddTeacherAsync(host, physics, shevchenko, cancellationToken);
        await CourseRows.InsertMembershipAsync(
            host, physics, franko, cancellationToken, CourseTestData.Roles.Teacher, firstSeenAt: FrankoLastSeen.AddYears(-1), lastSeenAt: FrankoLastSeen, onRoster: false);
        await CourseRows.InsertMembershipAsync(
            host, physics, melnyk, cancellationToken, CourseTestData.Roles.Student, firstSeenAt: MelnykLastSeen.AddYears(-1), lastSeenAt: MelnykLastSeen, onRoster: false);

        var nameless = await Cl.InsertPersonAsync(host, 10, null, null, null, cancellationToken);
        await Cl.AddStudentAsync(host, physics, nameless, cancellationToken);
        var emailOnly = await Cl.InsertPersonAsync(host, 11, null, null, EmailOnlyStudent, cancellationToken);
        await Cl.AddStudentAsync(host, physics, emailOnly, cancellationToken);
        var ordinal = 12;
        foreach (var label in new[] { "Іваненко Олег", "Ігнатенко Анна", "Гнатюк Ірина", "Ґудзь Петро", "Андрієнко Марія" })
        {
            var parts = label.Split(' ');
            var student = await Cl.InsertPersonAsync(
                host, ordinal, parts[0], parts[1], CourseTestData.Email($"student.{ordinal}"), cancellationToken);
            await Cl.AddStudentAsync(host, physics, student, cancellationToken);
            ordinal++;
        }

        // US-015 ResolveSubmitterAsync: the Google user id alone, a student off the roster, first = last seen.
        var submitter = await CourseRows.InsertParticipantAsync(host, cancellationToken, googleUserId: CourseTestData.UserId(30));
        await CourseRows.InsertMembershipAsync(
            host,
            physics,
            submitter,
            cancellationToken,
            CourseTestData.Roles.Student,
            firstSeenAt: SubmitterSeen,
            lastSeenAt: SubmitterSeen,
            onRoster: false);

        await Cl.AddTeacherAsync(host, chemistry, koval, cancellationToken);
        await Cl.AddStudentAsync(host, chemistry, bondarenko, cancellationToken);

        return new SeededCard(physics, chemistry, shevchenko, ukrainka, franko, melnyk, submitter, nameless, emailOnly);
    }

    /// <summary>A course with nobody in it.</summary>
    public static Task<long> InsertEmptyCourseAsync(this InstallationTestHost host, int ordinal, string name, CancellationToken cancellationToken) =>
        CourseRows.InsertCourseAsync(host, cancellationToken, googleId: CourseTestData.CourseId(ordinal), name: name);

    /// <summary>Runs <paramref name="body"/> with the real <see cref="CourseCardSource"/> over the host's database.</summary>
    public static async Task WithCourseCardSourceAsync(this InstallationTestHost host, Func<ICourseCardSource, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new CourseCardSource(db));
    }

    /// <summary>The decoded text of the element carrying <c>data-field="{field}"</c>, or null when there is none.</summary>
    public static string? FieldText(string html, string field)
    {
        var inner = FieldInnerHtml(html, field);
        return inner is null ? null : WebUtility.HtmlDecode(Tags().Replace(inner, " ")).Trim();
    }

    /// <summary>The raw inner HTML of the element carrying <c>data-field="{field}"</c>, or null when there is none.</summary>
    public static string? FieldInnerHtml(string html, string field)
    {
        var match = Regex.Match(
            html,
            @"<(?<tag>[a-z0-9]+)[^>]*\b" + Markup.FieldAttribute + @"=""" + Regex.Escape(field) + @"""[^>]*>(?<body>.*?)</\k<tag>>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["body"].Value : null;
    }

    /// <summary>The inner HTML of the section with that id, or null when the page has none.</summary>
    public static string? Section(string html, string sectionId)
    {
        var match = Regex.Match(
            html,
            @"<section[^>]*\bid=""" + Regex.Escape(sectionId) + @"""[^>]*>(?<body>.*?)</section>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["body"].Value : null;
    }

    /// <summary>The count shown in a section (<c>data-count</c>), or null when none is shown.</summary>
    public static int? Count(string html, string sectionId)
    {
        var section = Section(html, sectionId);
        if (section is null)
        {
            return null;
        }

        var match = CountElement().Match(section);
        return match.Success ? int.Parse(Collapse(match.Groups["body"].Value), System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    /// <summary>The text of each row of a section, in order, tags removed and white space collapsed.</summary>
    public static IReadOnlyList<string> Rows(string html, string sectionId)
    {
        var section = Section(html, sectionId);
        return section is null ? [] : RowElement().Matches(section).Select(m => Collapse(m.Groups["body"].Value)).ToList();
    }

    /// <summary>The raw inner HTML of each row of a section, in order.</summary>
    public static IReadOnlyList<string> RowHtml(string html, string sectionId)
    {
        var section = Section(html, sectionId);
        return section is null ? [] : RowElement().Matches(section).Select(m => m.Groups["body"].Value).ToList();
    }

    /// <summary>The decoded text of the owner mark in a row, or null when the row has none.</summary>
    public static string? OwnerMark(string rowHtml)
    {
        var match = OwnerElement().Match(rowHtml);
        return match.Success ? Collapse(match.Groups["body"].Value) : null;
    }

    /// <summary>The empty-section message of a section, or null when it has none.</summary>
    public static string? EmptyMessage(string html, string sectionId)
    {
        var section = Section(html, sectionId);
        if (section is null)
        {
            return null;
        }

        var match = EmptyElement().Match(section);
        return match.Success ? Collapse(match.Groups["body"].Value) : null;
    }

    /// <summary>Whether the section draws a table.</summary>
    public static bool HasTable(string html, string sectionId) =>
        Section(html, sectionId) is { } section && section.Contains("<table", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every <c>href</c> on the page, decoded.</summary>
    public static IReadOnlyList<string> Hrefs(string html) => Cl.Hrefs(html);

    private static string Collapse(string fragment) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(fragment, " ")), " ").Trim();

    [GeneratedRegex(@"<(?<tag>[a-z0-9]+)[^>]*\bdata-count\b[^>]*>(?<body>.*?)</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CountElement();

    [GeneratedRegex(@"<tr[^>]*\bdata-person\b[^>]*>(?<body>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RowElement();

    [GeneratedRegex(@"<(?<tag>[a-z0-9]+)[^>]*\bdata-owner\b[^>]*>(?<body>.*?)</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OwnerElement();

    [GeneratedRegex(@"<(?<tag>[a-z0-9]+)[^>]*\bdata-empty\b[^>]*>(?<body>.*?)</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex EmptyElement();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
