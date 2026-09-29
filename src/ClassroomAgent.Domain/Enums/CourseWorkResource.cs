namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// Which Classroom resource a <see cref="ClassroomAgent.Domain.Entities.CourseWork"/> row came from (US-015
/// entity model §2). Exactly two members, no <c>Unknown</c>: the adapter reads two named Classroom endpoints and
/// knows which one answered, so an unrecognised value cannot arise — unlike a course state or a submission state,
/// which arrive inside a payload.
/// </summary>
public enum CourseWorkResource
{
    CourseWork,
    CourseWorkMaterial,
}
