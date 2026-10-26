using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A course the latest runs skipped for out-of-range values or a course state the program does not know, kept until
/// the problem is gone (US-058 entity model §3; <c>trebovaniya.md</c> §3 <c>SyncState</c>, BR-046).
/// </summary>
/// <remarks>
/// Keyed by Google's course id alone, so a course never loaded can be recorded too (FR-005.3). Values are cut, not
/// refused, as <see cref="Course"/> cuts them (VR-001, VR-002); only a blank id and an undefined reason are refused.
/// </remarks>
public sealed class SyncSkippedCourse
{
    private SyncSkippedCourse()
    {
        CourseGoogleId = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>Google's course id from the course list; the natural key (unique).</summary>
    public string CourseGoogleId { get; private set; }

    /// <summary>Google's list name, trimmed and cut to Course.MaxNameLength; null when missing or blank.</summary>
    public string? CourseName { get; private set; }

    public CourseSkipReason Reason { get; private set; }

    /// <summary>The run instant that began the current streak of skips; never moved by a repeated skip.</summary>
    public DateTimeOffset SkippedSince { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>FR-005: the first skip of a streak. A blank Google id is refused (ArgumentException).</summary>
    public static SyncSkippedCourse Record(
        string courseGoogleId,
        string? courseName,
        CourseSkipReason reason,
        DateTimeOffset skippedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courseGoogleId);
        var record = new SyncSkippedCourse
        {
            CourseGoogleId = Cut(courseGoogleId.Trim(), Course.MaxGoogleIdLength),
            SkippedSince = skippedAt,
        };
        record.SkipAgain(courseName, reason);
        return record;
    }

    /// <summary>FR-006, I-3: a repeated skip — the name and the reason follow this run, the instant stays.</summary>
    public void SkipAgain(string? courseName, CourseSkipReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }

        CourseName = string.IsNullOrWhiteSpace(courseName) ? null : Cut(courseName.Trim(), Course.MaxNameLength);
        Reason = reason;
    }

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
