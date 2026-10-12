using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-022 host seeding and markup readers. The school is inserted by SQL through the US-020 helpers of
/// <see cref="CourseListHostExtensions"/> so the tests do not depend on the code under test (TC-2). Every name and email
/// is invented (TC-4).
/// </summary>
public static partial class TeacherListHostExtensions
{
    public const string PhysicsName = "Фізика";

    public const string ChemistryName = "Хімія";

    public const string BiologyName = "Біологія";

    public const string GeographyName = "Географія";

    public const string HistoryName = "Історія України";

    /// <summary>Teaches Фізика and Хімія (active), left Біологія's roster as teacher, studies Географія — 2 active courses (AC-016).</summary>
    public const string ShevchenkoLabel = "Шевченко Тарас";

    /// <summary>Teaches Географія (active) and Історія України (archived): 1 active, 1 archived, 2 under "all" (AC-002).</summary>
    public const string UkrainkaLabel = "Українка Леся";

    /// <summary>Teaches only the archived Історія України (AC-001, AC-002).</summary>
    public const string KovalLabel = "Коваль Олена";

    /// <summary>A former teacher of Фізика — off the roster everywhere (AC-001).</summary>
    public const string FrankoLabel = "Франко Іван";

    /// <summary>Only a student, of Фізика (AC-001).</summary>
    public const string StudentLabel = "Мельник Петро";

    public static string ShevchenkoEmail => CourseTestData.Email("t.shevchenko");

    public static string UkrainkaEmail => CourseTestData.Email("l.ukrainka");

    public static string KovalEmail => CourseTestData.Email("o.koval");

    public static string FrankoEmail => CourseTestData.Email("i.franko");

    public static string StudentEmail => CourseTestData.Email("p.melnyk");

    /// <summary>Every course name, teacher name and email the world holds — none may reach a log line (SC-10, S-08).</summary>
    public static IReadOnlyList<string> SensitiveValues { get; } =
    [
        PhysicsName, ChemistryName, BiologyName, GeographyName, HistoryName,
        "Шевченко", "Українка", "Коваль", "Франко", "Мельник",
        "t.shevchenko@", "l.ukrainka@", "o.koval@", "i.franko@", "p.melnyk@",
    ];

    /// <summary>The ids the world holds.</summary>
    public sealed record SeededWorld(
        long Physics,
        long Chemistry,
        long Biology,
        long Geography,
        long History,
        long Shevchenko,
        long Ukrainka,
        long Koval,
        long Franko,
        long Student);

    /// <summary>
    /// Фізика, Хімія, Біологія, Географія (active) and Історія України (archived). Шевченко teaches Фізика and Хімія,
    /// is an off-roster teacher of Біологія and a student of Географія; Українка teaches Географія and Історія України;
    /// Коваль teaches Історія України; Франко is an off-roster teacher of Фізика; Мельник is a student of Фізика.
    /// Expected: ACTIVE — Українка (1), Шевченко (2); ARCHIVED — Коваль (1), Українка (1); all — Коваль (1),
    /// Українка (2), Шевченко (2).
    /// </summary>
    public static async Task<SeededWorld> SeedTeacherListAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var physics = await host.InsertCourseAsync(1, PhysicsName, cancellationToken);
        var chemistry = await host.InsertCourseAsync(2, ChemistryName, cancellationToken);
        var biology = await host.InsertCourseAsync(3, BiologyName, cancellationToken);
        var geography = await host.InsertCourseAsync(4, GeographyName, cancellationToken);
        var history = await host.InsertCourseAsync(5, HistoryName, cancellationToken, CourseTestData.States.Archived);

        var shevchenko = await host.InsertPersonAsync(1, "Шевченко", "Тарас", ShevchenkoEmail, cancellationToken);
        var ukrainka = await host.InsertPersonAsync(2, "Українка", "Леся", UkrainkaEmail, cancellationToken);
        var koval = await host.InsertPersonAsync(3, "Коваль", "Олена", KovalEmail, cancellationToken);
        var franko = await host.InsertPersonAsync(4, "Франко", "Іван", FrankoEmail, cancellationToken);
        var student = await host.InsertPersonAsync(5, "Мельник", "Петро", StudentEmail, cancellationToken);

        await host.AddTeacherAsync(physics, shevchenko, cancellationToken);
        await host.AddTeacherAsync(chemistry, shevchenko, cancellationToken);
        await host.AddTeacherAsync(biology, shevchenko, cancellationToken, onRoster: false);
        await host.AddStudentAsync(geography, shevchenko, cancellationToken);
        await host.AddTeacherAsync(geography, ukrainka, cancellationToken);
        await host.AddTeacherAsync(history, ukrainka, cancellationToken);
        await host.AddTeacherAsync(history, koval, cancellationToken);
        await host.AddTeacherAsync(physics, franko, cancellationToken, onRoster: false);
        await host.AddStudentAsync(physics, student, cancellationToken);

        return new SeededWorld(physics, chemistry, biology, geography, history, shevchenko, ukrainka, koval, franko, student);
    }

    /// <summary>
    /// <paramref name="count"/> teachers, each teaching one active course, inserted so that their label order
    /// ("Вчитель 001" … ) is the reverse of their id order; returns the participant ids in label order.
    /// </summary>
    public static async Task<IReadOnlyList<long>> SeedNumberedTeachersAsync(
        this InstallationTestHost host, int count, CancellationToken cancellationToken)
    {
        await host.WaitForPurgeRunsAsync(1, cancellationToken);
        var ids = new List<long>();
        for (var i = count; i >= 1; i--)
        {
            var course = await host.InsertCourseAsync(i, $"Курс {i:D3}", cancellationToken);
            var person = await host.InsertPersonAsync(i, "Вчитель", $"{i:D3}", null, cancellationToken);
            await host.AddTeacherAsync(course, person, cancellationToken);
            ids.Add(person);
        }

        ids.Reverse();
        return ids;
    }

    /// <summary>Runs <paramref name="body"/> with the real <see cref="TeacherListSource"/> over the host's database.</summary>
    public static async Task WithTeacherListSourceAsync(this InstallationTestHost host, Func<ITeacherListSource, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new TeacherListSource(db));
    }

    /// <summary>The participant ids of the listed rows, in page order (<c>data-participant-id</c>).</summary>
    public static IReadOnlyList<long> RowIds(string html) =>
        RowAttribute().Matches(html).Select(m => long.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture)).ToList();

    /// <summary>The text of the row of that participant, tags removed and white space collapsed; null when not listed.</summary>
    public static string? RowText(string html, long participantId) =>
        RowElement(html, participantId) is { } body ? Collapse(body) : null;

    /// <summary>The <c>href</c>s inside the row of that participant, decoded.</summary>
    public static IReadOnlyList<string> RowHrefs(string html, long participantId) =>
        RowElement(html, participantId) is { } body ? CourseListHostExtensions.Hrefs(body) : [];

    /// <summary>Whether the page has the status form, submitting by GET to the teacher list.</summary>
    public static bool HasGetForm(string html)
    {
        var tag = Regex.Match(
            html, @"<form[^>]*\bid=""" + TeacherListTestData.Markup.FormId + @"""[^>]*>", RegexOptions.IgnoreCase).Value;
        return Regex.IsMatch(tag, @"\bmethod=""get""", RegexOptions.IgnoreCase)
               && tag.Contains(@"action=""" + TeacherListTestData.Path + @"""", StringComparison.Ordinal);
    }

    private static string? RowElement(string html, long participantId)
    {
        var match = Regex.Match(
            html,
            @"<tr[^>]*\b" + TeacherListTestData.Markup.RowAttribute + @"=""" + participantId.ToString(CultureInfo.InvariantCulture)
            + @"""[^>]*>(?<body>.*?)</tr>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["body"].Value : null;
    }

    private static string Collapse(string fragment) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(fragment, " ")), " ").Trim();

    [GeneratedRegex(@"<tr[^>]*\bdata-participant-id=""(?<id>\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex RowAttribute();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
