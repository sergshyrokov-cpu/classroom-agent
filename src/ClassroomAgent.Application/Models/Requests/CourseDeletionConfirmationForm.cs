namespace ClassroomAgent.Application.Models.Requests;

/// <summary>API design §2.7: the repeated form field, bound as strings and parsed in Application (VR-001).</summary>
public sealed class CourseDeletionConfirmationForm
{
    public IReadOnlyList<string>? CourseIds { get; init; }
}
