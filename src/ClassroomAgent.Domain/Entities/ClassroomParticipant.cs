namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A person who came from synchronization (US-014 entity model §3; <c>trebovaniya.md</c> §3): a teacher or
/// student on a Classroom course roster. One row per <see cref="GoogleUserId"/>, which is the person's only
/// identity (OD-011).
/// </summary>
/// <remarks>
/// The entity holds no clock and no policy — every instant arrives as an argument (US-013 entity model
/// precedent). It carries <b>no role member of any kind</b>: the role belongs to <see cref="CourseMembership"/>
/// (BR-050, FR-006); a <c>Role</c> property here would be a modelling defect.
/// </remarks>
public sealed class ClassroomParticipant
{
    /// <summary>db-design §4.1: Google's own bound on <c>userId</c>.</summary>
    public const int MaxGoogleUserIdLength = 64;

    /// <summary>db-design §4.1: 64 local + '@' + 255 domain.</summary>
    public const int MaxEmailLength = 320;

    /// <summary>db-design §4.1: Classroom's bound on a person's name.</summary>
    public const int MaxFullNameLength = 750;

    private ClassroomParticipant()
    {
        GoogleUserId = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>Google's <c>userId</c>; the upsert key and the person's only identity (OD-011).</summary>
    public string GoogleUserId { get; private set; }

    /// <summary>Optional (OD-006), non-unique (OD-011); personal data.</summary>
    public string? Email { get; private set; }

    /// <summary>One string (I-2); personal data.</summary>
    public string? FullName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates the row for a person not seen before.</summary>
    public static ClassroomParticipant Import(string googleUserId, string? email, string? fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleUserId);

        var participant = new ClassroomParticipant
        {
            GoogleUserId = Cut(googleUserId.Trim(), MaxGoogleUserIdLength),
        };
        participant.Apply(email, fullName);
        return participant;
    }

    /// <summary>The upsert's update half. Identity is <see cref="GoogleUserId"/>, which is untouched.</summary>
    public void UpdateFrom(string? email, string? fullName) => Apply(email, fullName);

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// Trimmed and lower-cased through the one normalisation the system has, so Epic 4's matching by address is
    /// not defeated by case (VR-003). A blank address becomes null, so "no address" has one representation.
    /// </summary>
    private void Apply(string? email, string? fullName)
    {
        Email = string.IsNullOrWhiteSpace(email)
            ? null
            : Cut(WorkspaceConnection.NormalizeEmail(email), MaxEmailLength);
        FullName = string.IsNullOrWhiteSpace(fullName) ? null : Cut(fullName.Trim(), MaxFullNameLength);
    }
}
