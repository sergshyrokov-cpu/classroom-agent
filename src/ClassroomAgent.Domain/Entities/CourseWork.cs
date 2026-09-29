using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One Classroom coursework or material item, kept as a single entity for both resources (US-015 entity model §1;
/// OD-008). <see cref="Resource"/> together with <see cref="GoogleId"/> is the upsert key (PC-3); the surrogate
/// <see cref="Id"/> is what everything else references (FR-010).
/// </summary>
/// <remarks>
/// TEST_WRITING compile-only skeleton (OD-012): every behaviour member below throws
/// <see cref="NotImplementedException"/>. IMPLEMENTATION replaces every one of them.
/// </remarks>
public sealed class CourseWork
{
    /// <summary>Classroom's own bound on a coursework/material title.</summary>
    public const int MaxTitleLength = 3000;

    /// <summary>Classroom's own bound on a coursework/material id.</summary>
    public const int MaxGoogleIdLength = 64;

    private CourseWork()
    {
        GoogleId = string.Empty;
        Title = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The owning <see cref="Course"/>.</summary>
    public long CourseId { get; private set; }

    /// <summary>Classroom's id for the item; part of the upsert key together with <see cref="Resource"/> (PC-3).</summary>
    public string GoogleId { get; private set; }

    /// <summary>Which Classroom resource this row came from (§2).</summary>
    public CourseWorkResource Resource { get; private set; }

    /// <summary>Truncated at <see cref="MaxTitleLength"/>.</summary>
    public string Title { get; private set; }

    /// <summary>The one date of the FR-008 cascade.</summary>
    public DateTimeOffset ItemDate { get; private set; }

    /// <summary>Absent unless Google set both date and time (db-design §3.4).</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>Absent means ungraded work.</summary>
    public decimal? MaxPoints { get; private set; }

    /// <summary>Google's, as given.</summary>
    public DateTimeOffset? CreationTime { get; private set; }

    /// <summary>Google's, as given; PC-11 reads it.</summary>
    public DateTimeOffset? UpdateTime { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// BR-052's three kinds, computed and never stored (§3, PC-3): <see cref="CourseWorkKind.Material"/> when
    /// <see cref="Resource"/> is <see cref="CourseWorkResource.CourseWorkMaterial"/>, otherwise
    /// <see cref="CourseWorkKind.GradedWork"/> when <see cref="MaxPoints"/> has a value and
    /// <see cref="CourseWorkKind.UngradedWork"/> when it does not.
    /// </summary>
    public CourseWorkKind Kind => throw new NotImplementedException();

    /// <summary>
    /// Creates the row for an item not seen before, truncating <see cref="Title"/> at <see cref="MaxTitleLength"/>
    /// rather than refusing (VR-002), and refusing outright only what db-design §3.3 names unimportable: a
    /// missing Google id, a missing resource, or no date at all from the cascade.
    /// </summary>
    public static CourseWork Import(long courseId, string googleId, CourseWorkResource resource, CourseWorkDetails details) =>
        throw new NotImplementedException();

    /// <summary>
    /// The upsert's update half; the surrogate identity and <see cref="GoogleId"/> are untouched, so anything
    /// referring to the row still does (FR-010).
    /// </summary>
    public void UpdateFrom(CourseWorkDetails details) => throw new NotImplementedException();
}
