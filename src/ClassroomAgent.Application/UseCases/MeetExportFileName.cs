using System.Globalization;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-044 spec FR-008.1 (entity model §2.4): <c>Meet &lt;course&gt; &lt;from&gt;–&lt;to&gt;.xlsx</c> and
/// <c>Meet &lt;course&gt; &lt;yyyy-MM-dd&gt; &lt;HH-mm&gt;.xlsx</c>, the course part cleaned by the rule of
/// <see cref="JournalExportFileName"/>. Never a person's name or email, never the meeting code.
/// </summary>
public static class MeetExportFileName
{
    public static string BuildForPeriod(string courseName, DateOnly from, DateOnly to, string fallback) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Meet {JournalExportFileName.CoursePart(courseName, fallback)} {from:yyyy-MM-dd}–{to:yyyy-MM-dd}.xlsx");

    /// <summary>Spec I-8: the start time as <c>HH-mm</c>, because <c>:</c> is forbidden in a Windows file name.</summary>
    public static string BuildForMeeting(string courseName, DateTime localStart, string fallback) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Meet {JournalExportFileName.CoursePart(courseName, fallback)} {localStart:yyyy-MM-dd} {localStart:HH-mm}.xlsx");
}
