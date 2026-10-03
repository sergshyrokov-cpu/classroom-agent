using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// One item of a course — an assignment or a material (US-015 entity model §1; <c>trebovaniya.md</c> §3
/// "CourseWork"). Both Classroom resources live in this one table, distinguished by <see cref="Resource"/>
/// (OD-008), and the natural key is the three of them together: the course, the resource and Google's id
/// (Specification v2, PC-3), because a Classroom <c>courseWork.id</c> is unique only within its course.
/// </summary>
/// <remarks>
/// The entity holds no clock and no policy: the FR-008 date cascade is reduced to one instant in the adapter,
/// and the item arrives already resolved (entity model §6). A value longer than its bound is <b>cut, not
/// refused</b> (VR-002), while the Google id and a date are required — an item without either could never be
/// upserted or placed in a journal column (db-design §3.3).
/// </remarks>
public sealed class CourseWork
{
    /// <summary>Classroom's own documented bound for a title.</summary>
    public const int MaxTitleLength = 3000;

    /// <summary>PC-3: Google-side identifiers are stored in their own bounded column.</summary>
    public const int MaxGoogleIdLength = 64;

    private CourseWork()
    {
        GoogleId = string.Empty;
        Title = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The owning <see cref="Course"/>; part of the natural key (Specification v2).</summary>
    public long CourseId { get; private set; }

    /// <summary>Google's id for the item. Unique only within its course, hence the composite key.</summary>
    public string GoogleId { get; private set; }

    /// <summary>Which Classroom resource answered — part of the natural key (PC-3, OD-008).</summary>
    public CourseWorkResource Resource { get; private set; }

    public string Title { get; private set; }

    /// <summary>The one date of the FR-008 cascade, resolved in the adapter.</summary>
    public DateTimeOffset ItemDate { get; private set; }

    /// <summary>§3's "срок сдачи (если задан)"; absent unless Google gave both a date and a time (db-design §3.4).</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>§3's maximum points; absent means ungraded work (BR-052).</summary>
    public decimal? MaxPoints { get; private set; }

    /// <summary>Google's, stored as given (I-1). PC-11 reads it for a course's last activity.</summary>
    public DateTimeOffset? CreationTime { get; private set; }

    /// <summary>Google's. PC-11 reads it.</summary>
    public DateTimeOffset? UpdateTime { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// BR-052's three kinds, <b>computed and never stored</b> (PC-3, §3 v32): the material resource is always a
    /// material, and otherwise the presence of maximum points decides. A teacher adding points later therefore
    /// changes the kind without changing the row's identity.
    /// </summary>
    public CourseWorkKind Kind => Resource == CourseWorkResource.CourseWorkMaterial
        ? CourseWorkKind.Material
        : MaxPoints.HasValue
            ? CourseWorkKind.GradedWork
            : CourseWorkKind.UngradedWork;

    /// <summary>Creates the row for an item not seen before. The Google id and a resolved date are required.</summary>
    public static CourseWork Import(long courseId, string googleId, CourseWorkResource resource, CourseWorkDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleId);
        ArgumentNullException.ThrowIfNull(details);

        var item = new CourseWork
        {
            CourseId = courseId,
            GoogleId = Cut(googleId.Trim(), MaxGoogleIdLength),
            Resource = resource,
        };
        item.Apply(details);
        return item;
    }

    /// <summary>
    /// The upsert's update half (FR-010). The surrogate identity, the course, the resource and
    /// <see cref="GoogleId"/> are untouched, so anything referring to the item survives.
    /// </summary>
    public void UpdateFrom(CourseWorkDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Apply(details);
    }

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private void Apply(CourseWorkDetails details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Title);

        // db-design §3.3: the cascade ends at creationTime, which Classroom always returns, so the default instant
        // means no source produced anything — and a row without a date could never be a journal column (§4 Epic 3).
        if (details.ItemDate == default)
        {
            throw new ArgumentException(
                "A course work item needs a date from the FR-008 cascade.",
                nameof(details));
        }

        Title = Cut(details.Title.Trim(), MaxTitleLength);
        ItemDate = details.ItemDate;
        DueAt = details.DueAt;
        MaxPoints = details.MaxPoints;
        CreationTime = details.CreationTime;
        UpdateTime = details.UpdateTime;
    }
}
