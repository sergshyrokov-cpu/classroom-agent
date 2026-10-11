using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-020 host and port seeding: a small school (courses in several states, current and former teachers, students on
/// and off the roster) inserted by SQL so the tests do not depend on the production code under test (TC-2), and the
/// readers of the page's markup hooks (<see cref="CourseListTestData.Markup"/>). Every name and email is invented (TC-4).
/// </summary>
public static partial class CourseListHostExtensions
{
    public const string PhysicsName = "Фізика";

    public const string PhysicsSection = "9-Б";

    public const string ChemistryName = "Хімія";

    public const string HistoryName = "Історія України";

    public const string AlgebraName = "Алгебра";

    /// <summary>Owner of Фізика and teacher of the archived Історія України.</summary>
    public const string ShevchenkoLabel = "Шевченко Тарас";

    /// <summary>Co-teacher of Фізика.</summary>
    public const string UkrainkaLabel = "Українка Леся";

    /// <summary>A former teacher of Фізика — off the roster.</summary>
    public const string FrankoLabel = "Франко Іван";

    /// <summary>Teacher of Хімія.</summary>
    public const string KovalLabel = "Коваль Олена";

    public const string StudentLabel = "Мельник Петро";

    public static string ShevchenkoEmail => CourseTestData.Email("t.shevchenko");

    public static string UkrainkaEmail => CourseTestData.Email("l.ukrainka");

    public static string FrankoEmail => CourseTestData.Email("i.franko");

    public static string KovalEmail => CourseTestData.Email("o.koval");

    /// <summary>Фізика's update time: 22:30 UTC on 31 March 2026 — 01:30 on 1 April in Kyiv (spec AC-018).</summary>
    public static readonly DateTimeOffset PhysicsUpdateTime = new(2026, 3, 31, 22, 30, 0, TimeSpan.Zero);

    /// <summary>Every course name, section, teacher name and email the world holds — none may reach a log line (SC-10).</summary>
    public static IReadOnlyList<string> SensitiveValues { get; } =
    [
        PhysicsName, ChemistryName, HistoryName, AlgebraName, "Шевченко", "Українка", "Франко", "Коваль", "Мельник",
        "t.shevchenko@", "l.ukrainka@", "i.franko@", "o.koval@", "student.",
    ];

    /// <summary>The ids the world holds.</summary>
    public sealed record SeededWorld(
        long Physics,
        long Chemistry,
        long History,
        long Algebra,
        long Shevchenko,
        long Ukrainka,
        long Franko,
        long Koval);

    /// <summary>
    /// Фізика (active, section 9-Б, owner Шевченко and co-teacher Українка current, Франко a former teacher, three current
    /// students and one leaver); Хімія (active, Коваль, one student); Історія України (archived, Шевченко); Алгебра
    /// (provisioned, nobody).
    /// </summary>
    public static async Task<SeededWorld> SeedCourseListAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet.
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var physics = await host.InsertCourseAsync(
            1, PhysicsName, cancellationToken, section: PhysicsSection, updateTime: PhysicsUpdateTime);
        var chemistry = await host.InsertCourseAsync(2, ChemistryName, cancellationToken);
        var history = await host.InsertCourseAsync(3, HistoryName, cancellationToken, CourseTestData.States.Archived);
        var algebra = await host.InsertCourseAsync(4, AlgebraName, cancellationToken, CourseTestData.States.Provisioned);

        var shevchenko = await host.InsertPersonAsync(1, "Шевченко", "Тарас", ShevchenkoEmail, cancellationToken);
        var ukrainka = await host.InsertPersonAsync(2, "Українка", "Леся", UkrainkaEmail, cancellationToken);
        var franko = await host.InsertPersonAsync(3, "Франко", "Іван", FrankoEmail, cancellationToken);
        var koval = await host.InsertPersonAsync(4, "Коваль", "Олена", KovalEmail, cancellationToken);

        await host.AddTeacherAsync(physics, shevchenko, cancellationToken);
        await host.AddTeacherAsync(physics, ukrainka, cancellationToken);
        await host.AddTeacherAsync(physics, franko, cancellationToken, onRoster: false);
        await host.AddTeacherAsync(chemistry, koval, cancellationToken);
        await host.AddTeacherAsync(history, shevchenko, cancellationToken);

        for (var i = 0; i < 3; i++)
        {
            var student = await host.InsertPersonAsync(10 + i, "Мельник", "Петро", CourseTestData.Email($"student.{i}"), cancellationToken);
            await host.AddStudentAsync(physics, student, cancellationToken);
        }

        var leaver = await host.InsertPersonAsync(20, "Мельник", "Петро", CourseTestData.Email("student.left"), cancellationToken);
        await host.AddStudentAsync(physics, leaver, cancellationToken, onRoster: false);
        var chemistryStudent = await host.InsertPersonAsync(21, "Мельник", "Петро", CourseTestData.Email("student.c"), cancellationToken);
        await host.AddStudentAsync(chemistry, chemistryStudent, cancellationToken);

        return new SeededWorld(physics, chemistry, history, algebra, shevchenko, ukrainka, franko, koval);
    }

    /// <summary>A course with an ordinal-numbered Google id and the given state code.</summary>
    public static Task<long> InsertCourseAsync(
        this InstallationTestHost host,
        int ordinal,
        string name,
        CancellationToken cancellationToken,
        string state = CourseTestData.States.Active,
        string? section = null,
        DateTimeOffset? updateTime = null) =>
        CourseRows.InsertCourseAsync(
            host,
            cancellationToken,
            googleId: CourseTestData.CourseId(ordinal),
            name: name,
            state: state,
            section: section,
            updateTime: updateTime);

    /// <summary>A participant with an ordinal-numbered Google id and the FR-007 name parts.</summary>
    public static Task<long> InsertPersonAsync(
        this InstallationTestHost host,
        int ordinal,
        string? surname,
        string? givenName,
        string? email,
        CancellationToken cancellationToken) =>
        CourseRows.InsertParticipantAsync(
            host,
            cancellationToken,
            googleUserId: CourseTestData.UserId(ordinal),
            email: email,
            fullName: givenName is null && surname is null ? null : $"{givenName} {surname}".Trim(),
            surname: surname,
            givenName: givenName);

    public static Task<int> AddTeacherAsync(
        this InstallationTestHost host, long courseId, long participantId, CancellationToken cancellationToken, bool onRoster = true) =>
        CourseRows.InsertMembershipAsync(
            host, courseId, participantId, cancellationToken, role: CourseTestData.Roles.Teacher, onRoster: onRoster);

    public static Task<int> AddStudentAsync(
        this InstallationTestHost host, long courseId, long participantId, CancellationToken cancellationToken, bool onRoster = true) =>
        CourseRows.InsertMembershipAsync(
            host, courseId, participantId, cancellationToken, role: CourseTestData.Roles.Student, onRoster: onRoster);

    /// <summary>
    /// A fingerprint of every table the page could write to (spec FR-015, FR-016, S-12): the three US-014 tables row by
    /// row, and the audit and sync-state counts.
    /// </summary>
    public static async Task<string> CourseListFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var table in new[] { "course", "classroom_participant", "course_membership" })
        {
            var digest = await host.ScalarAsync<string>(
                $"SELECT coalesce(md5(string_agg(row_to_json(t)::text, '|' ORDER BY t.id)), '-') FROM {table} t",
                cancellationToken);
            parts.Add(table + "=" + digest);
        }

        parts.Add("audit=" + await host.ScalarAsync<long>("SELECT count(*) FROM audit_event", cancellationToken));
        parts.Add("sync=" + await host.ScalarAsync<long>("SELECT count(*) FROM sync_state", cancellationToken));
        return string.Join(';', parts);
    }

    /// <summary>Runs <paramref name="body"/> with the real <see cref="CourseListSource"/> over the host's database.</summary>
    public static async Task WithCourseListSourceAsync(this InstallationTestHost host, Func<ICourseListSource, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new CourseListSource(db));
    }

    /// <summary>The course ids of the listed rows, in page order (<c>data-course-id</c>).</summary>
    public static IReadOnlyList<long> RowIds(string html) =>
        RowAttribute().Matches(html).Select(m => long.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture)).ToList();

    /// <summary>The text of each listed row, in page order, tags removed and white space collapsed.</summary>
    public static IReadOnlyList<string> RowTexts(string html) =>
        RowElement().Matches(html).Select(m => Collapse(m.Groups["body"].Value)).ToList();

    /// <summary>The options of the teacher choice: (value, decoded label), "all teachers" included with an empty value.</summary>
    public static IReadOnlyList<(string Value, string Label)> TeacherOptions(string html) => Options(html, CourseListTestData.Parameters.TeacherId);

    /// <summary>The options of the status choice: (value, decoded label).</summary>
    public static IReadOnlyList<(string Value, string Label)> StatusOptions(string html) => Options(html, CourseListTestData.Parameters.Status);

    /// <summary>The value of the selected option of a select, or null when none is marked selected.</summary>
    public static string? SelectedValue(string html, string selectName)
    {
        var select = SelectElement(html, selectName);
        if (select is null)
        {
            return null;
        }

        var selected = SelectedOption().Match(select);
        return selected.Success ? WebUtility.HtmlDecode(selected.Groups["value"].Value) : null;
    }

    /// <summary>Every <c>href</c> on the page, decoded.</summary>
    public static IReadOnlyList<string> Hrefs(string html) =>
        Href().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups["href"].Value)).ToList();

    /// <summary>The <c>href</c>s inside the element with that id (for the paging nav and the home page section).</summary>
    public static IReadOnlyList<string> HrefsInside(string html, string elementId, string tag)
    {
        var match = Regex.Match(
            html,
            "<" + tag + @"[^>]*\bid=""" + Regex.Escape(elementId) + @"""[^>]*>(?<body>.*?)</" + tag + ">",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? Hrefs(match.Groups["body"].Value) : [];
    }

    /// <summary>The decoded text of every validation message, in page order.</summary>
    public static IReadOnlyList<string> ErrorMessages(string html) =>
        ErrorElement().Matches(html).Select(m => Collapse(m.Groups["body"].Value)).ToList();

    /// <summary>Whether the page has the filter form, submitting by GET to the course list.</summary>
    public static bool HasGetForm(string html)
    {
        var tag = Regex.Match(
            html, @"<form[^>]*\bid=""" + CourseListTestData.Markup.FormId + @"""[^>]*>", RegexOptions.IgnoreCase).Value;
        return Regex.IsMatch(tag, @"\bmethod=""get""", RegexOptions.IgnoreCase)
               && tag.Contains(@"action=""" + CourseListTestData.Path + @"""", StringComparison.Ordinal);
    }

    private static IReadOnlyList<(string Value, string Label)> Options(string html, string selectName)
    {
        var select = SelectElement(html, selectName);
        return select is null
            ? []
            : OptionElement().Matches(select)
                .Select(m => (WebUtility.HtmlDecode(m.Groups["value"].Value), Collapse(m.Groups["label"].Value)))
                .ToList();
    }

    private static string? SelectElement(string html, string selectName)
    {
        var match = Regex.Match(
            html,
            @"<select[^>]*\bname=""" + Regex.Escape(selectName) + @"""[^>]*>(?<body>.*?)</select>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["body"].Value : null;
    }

    private static string Collapse(string fragment) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tags().Replace(fragment, " ")), " ").Trim();

    [GeneratedRegex(@"<tr[^>]*\bdata-course-id=""(?<id>\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex RowAttribute();

    [GeneratedRegex(@"<tr[^>]*\bdata-course-id=""\d+""[^>]*>(?<body>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RowElement();

    [GeneratedRegex(@"<option[^>]*\bvalue=""(?<value>[^""]*)""[^>]*>(?<label>.*?)</option>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OptionElement();

    [GeneratedRegex(@"<option(?=[^>]*\bselected\b)[^>]*\bvalue=""(?<value>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex SelectedOption();

    [GeneratedRegex(@"\bhref=""(?<href>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    [GeneratedRegex(@"<(?<tag>[a-z0-9]+)[^>]*\bclass=""[^""]*\bform-error\b[^""]*""[^>]*>(?<body>.*?)</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ErrorElement();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
