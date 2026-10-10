using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A Meet meeting code linked to one course, or carrying a person's "not a course" mark (US-032 spec FR-001,
/// entity model §1, db-design §2; <c>trebovaniya.md</c> §3 v55/v88). An unassigned code has no row.
/// </summary>
/// <remarks>
/// The invariants are those of <c>ck_meeting_code_link_*</c>: a linked row has a course, a "made automatically" flag
/// and a time, a person exactly when it is not automatic, and a confirmation only when automatic; a marked row has no
/// course, maker or confirmation and has who and when of the mark. Every change renews <see cref="ConcurrencyStamp"/>,
/// so two people changing one code cannot both succeed (spec FR-013). The use cases check the expected state before
/// calling a transition, so a guard firing here is a defect (AD-9).
/// </remarks>
public sealed class MeetingCodeLink
{
    public const int MeetingCodeMaxLength = MeetSession.MeetingCodeMaxLength;

    private MeetingCodeLink()
    {
        MeetingCode = string.Empty;
        ConcurrencyStamp = string.Empty;
    }

    public long Id { get; private set; }

    public string MeetingCode { get; private set; }

    /// <summary>Null = marked "not a course".</summary>
    public long? CourseId { get; private set; }

    public bool? LinkedAutomatically { get; private set; }

    /// <summary>The person who made the link; null for an automatic one. A bare id, no foreign key (PC-11).</summary>
    public long? LinkedByAppUserId { get; private set; }

    public DateTimeOffset? LinkedAt { get; private set; }

    public long? ConfirmedByAppUserId { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public long? MarkedByAppUserId { get; private set; }

    public DateTimeOffset? MarkedAt { get; private set; }

    public string ConcurrencyStamp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public MeetingCodeLinkState State => CourseId is null ? MeetingCodeLinkState.Marked : MeetingCodeLinkState.Linked;

    /// <summary>An automatic link nobody has confirmed yet (spec FR-009, I-9).</summary>
    public bool CanBeConfirmed => State == MeetingCodeLinkState.Linked && LinkedAutomatically == true && ConfirmedAt is null;

    /// <summary>The linking step's link (spec FR-006): automatic, unconfirmed, effective at once.</summary>
    public static MeetingCodeLink LinkAutomatically(string meetingCode, long courseId, DateTimeOffset at)
    {
        var link = Create(meetingCode);
        link.SetLink(courseId, null, at);
        return link;
    }

    /// <summary>A person's pick for an unassigned code (spec FR-008): made by them, unconfirmed (I-9).</summary>
    public static MeetingCodeLink LinkByPerson(string meetingCode, long courseId, long appUserId, DateTimeOffset at)
    {
        CheckId(appUserId, nameof(appUserId));
        var link = Create(meetingCode);
        link.SetLink(courseId, appUserId, at);
        return link;
    }

    /// <summary>A person's "not a course" mark on an unassigned code (spec FR-011, BR-083).</summary>
    public static MeetingCodeLink MarkNotACourse(string meetingCode, long appUserId, DateTimeOffset at)
    {
        CheckId(appUserId, nameof(appUserId));
        var link = Create(meetingCode);
        link.SetMark(appUserId, at);
        return link;
    }

    /// <summary>Moves a linked code to another course (spec FR-010): the person's link now, the confirmation cleared.</summary>
    public void Relink(long courseId, long appUserId, DateTimeOffset at)
    {
        CheckId(appUserId, nameof(appUserId));
        if (State != MeetingCodeLinkState.Linked)
        {
            throw new InvalidOperationException("Only a linked code can be re-linked.");
        }

        if (CourseId == courseId)
        {
            throw new InvalidOperationException("A code is re-linked to another course only.");
        }

        SetLink(courseId, appUserId, at);
    }

    /// <summary>Records that a person checked an automatic link (spec FR-009); nothing else changes.</summary>
    public void Confirm(long appUserId, DateTimeOffset at)
    {
        CheckId(appUserId, nameof(appUserId));
        if (!CanBeConfirmed)
        {
            throw new InvalidOperationException("Only an automatic, unconfirmed link can be confirmed.");
        }

        ConfirmedByAppUserId = appUserId;
        ConfirmedAt = at;
        ConcurrencyStamp = NewStamp();
    }

    /// <summary>Marks a linked code "not a course" (spec FR-011): course, maker and confirmation are cleared.</summary>
    public void Mark(long appUserId, DateTimeOffset at)
    {
        CheckId(appUserId, nameof(appUserId));
        if (State != MeetingCodeLinkState.Linked)
        {
            throw new InvalidOperationException("A marked code is already marked.");
        }

        SetMark(appUserId, at);
    }

    /// <summary>Removes the mark by picking a course (spec FR-012): the person's link, unconfirmed.</summary>
    public void LinkFromMark(long courseId, long appUserId, DateTimeOffset at)
    {
        CheckId(appUserId, nameof(appUserId));
        if (State != MeetingCodeLinkState.Marked)
        {
            throw new InvalidOperationException("Only a marked code can have its mark removed.");
        }

        SetLink(courseId, appUserId, at);
    }

    private static MeetingCodeLink Create(string meetingCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(meetingCode);
        if (meetingCode.Length > MeetingCodeMaxLength)
        {
            throw new ArgumentException("The meeting code is longer than its limit.", nameof(meetingCode));
        }

        return new MeetingCodeLink { MeetingCode = meetingCode };
    }

    private void SetLink(long courseId, long? appUserId, DateTimeOffset at)
    {
        CheckId(courseId, nameof(courseId));
        CourseId = courseId;
        LinkedAutomatically = appUserId is null;
        LinkedByAppUserId = appUserId;
        LinkedAt = at;
        ConfirmedByAppUserId = null;
        ConfirmedAt = null;
        MarkedByAppUserId = null;
        MarkedAt = null;
        ConcurrencyStamp = NewStamp();
    }

    private void SetMark(long appUserId, DateTimeOffset at)
    {
        CourseId = null;
        LinkedAutomatically = null;
        LinkedByAppUserId = null;
        LinkedAt = null;
        ConfirmedByAppUserId = null;
        ConfirmedAt = null;
        MarkedByAppUserId = appUserId;
        MarkedAt = at;
        ConcurrencyStamp = NewStamp();
    }

    private static void CheckId(long id, string name) => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id, name);

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
