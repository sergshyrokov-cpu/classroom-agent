using System.Globalization;
using System.Text;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-028 spec FR-006.1 (entity model §2.3): <c>&lt;course name&gt; &lt;from&gt;–&lt;to&gt;.xlsx</c> — the course name
/// and the period only, never a person's name or the template name, made safe for a Windows file name.
/// </summary>
public static class JournalExportFileName
{
    /// <summary>The course part is cut to this many characters.</summary>
    public const int MaxCourseLength = 100;

    private const string ForbiddenCharacters = "\\/:*?\"<>|";

    public static string Build(string courseName, DateOnly from, DateOnly to, string fallback)
    {
        ArgumentNullException.ThrowIfNull(courseName);
        ArgumentNullException.ThrowIfNull(fallback);

        var safe = new StringBuilder(courseName.Length);
        var pendingSpace = false;
        foreach (var c in courseName)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && safe.Length > 0)
            {
                safe.Append(' ');
            }

            pendingSpace = false;
            safe.Append(char.IsControl(c) || ForbiddenCharacters.Contains(c, StringComparison.Ordinal) ? '_' : c);
        }

        var course = safe.ToString().Trim(' ', '.');
        if (course.Length > MaxCourseLength)
        {
            course = course[..MaxCourseLength].Trim(' ', '.');
        }

        if (course.Length == 0)
        {
            course = fallback;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{course} {from:yyyy-MM-dd}–{to:yyyy-MM-dd}.xlsx");
    }
}
