namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// BR-052's three kinds of coursework — graded work, ungraded work, material (US-015 entity model §3). Computed
/// from the Classroom resource and whether maximum points are set; never stored (PC-3, OD-008).
/// </summary>
public enum CourseWorkKind
{
    GradedWork,
    UngradedWork,
    Material,
}
