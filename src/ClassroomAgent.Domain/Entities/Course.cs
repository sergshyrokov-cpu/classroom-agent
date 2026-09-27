using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The school's Classroom course (US-014 entity model §1; <c>trebovaniya.md</c> §3). One row per
/// <see cref="GoogleId"/>, which never changes once set and is the upsert key (FR-008).
/// </summary>
/// <remarks>
/// Every setter is private; the entity is changed only through <see cref="Import"/> and <see cref="UpdateFrom"/>.
/// Truncation happens here: each string is bounded by a public constant and a longer value is cut, not refused
/// (VR-002). There is deliberately no <c>MarkMissing</c> and no <c>Delete</c>: a course Google stopped returning
/// is left untouched (FR-011, I-4), and deletion belongs to the purge (PC-11, US-037).
/// <para>
/// Compile-only skeleton created at TEST_WRITING under US-014 OD-012; IMPLEMENTATION owns it from here.
/// </para>
/// </remarks>
public sealed class Course
{
    /// <summary>db-design §3.1: Classroom's own bound on a course id.</summary>
    public const int MaxGoogleIdLength = 64;

    /// <summary>db-design §3.1: Classroom's own documented bound.</summary>
    public const int MaxNameLength = 750;

    /// <summary>db-design §3.1: Classroom's bound.</summary>
    public const int MaxSectionLength = 2800;

    /// <summary>db-design §3.1: Classroom's bound.</summary>
    public const int MaxDescriptionHeadingLength = 3600;

    /// <summary>db-design §3.1: Classroom's bound.</summary>
    public const int MaxDescriptionLength = 30000;

    /// <summary>db-design §3.1: Classroom's bound.</summary>
    public const int MaxRoomLength = 650;

    /// <summary>db-design §3.1: Google's own bound on a user id (OD-004).</summary>
    public const int MaxOwnerGoogleIdLength = 64;

    /// <summary>db-design §3.1.</summary>
    public const int MaxAlternateLinkLength = 2048;

    /// <summary>db-design §3.1.</summary>
    public const int MaxTeacherFolderIdLength = 128;

    /// <summary>db-design §3.1.</summary>
    public const int MaxTeacherFolderTitleLength = 750;

    /// <summary>db-design §3.1.</summary>
    public const int MaxCalendarIdLength = 256;

    private Course()
    {
        GoogleId = string.Empty;
        Name = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The upsert key; never changes once set.</summary>
    public string GoogleId { get; private set; }

    public string Name { get; private set; }

    public string? Section { get; private set; }

    public string? DescriptionHeading { get; private set; }

    public string? Description { get; private set; }

    public string? Room { get; private set; }

    /// <summary>A value, not a reference (OD-004).</summary>
    public string? OwnerGoogleId { get; private set; }

    /// <summary>Google's, stored as given (I-1).</summary>
    public DateTimeOffset? CreationTime { get; private set; }

    /// <summary>Google's.</summary>
    public DateTimeOffset? UpdateTime { get; private set; }

    /// <summary>Closed enum (§2).</summary>
    public CourseState State { get; private set; }

    public string? AlternateLink { get; private set; }

    public string? TeacherFolderId { get; private set; }

    public string? TeacherFolderTitle { get; private set; }

    public string? CalendarId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates the row for a course not seen before. <paramref name="googleId"/> is required and non-blank.</summary>
    public static Course Import(string googleId, CourseState state, CourseDetails details) =>
        throw new NotImplementedException();

    /// <summary>
    /// The upsert's update half. The surrogate identity and <see cref="GoogleId"/> are untouched, so every
    /// reference to the course survives (FR-008).
    /// </summary>
    public void UpdateFrom(CourseState state, CourseDetails details) => throw new NotImplementedException();
}
