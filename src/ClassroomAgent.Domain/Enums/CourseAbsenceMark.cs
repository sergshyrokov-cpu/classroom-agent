namespace ClassroomAgent.Domain.Enums;

/// <summary>Why a stored course is marked absent from Google's course list (US-059 VR-003). Stored as a lower-case code.</summary>
public enum CourseAbsenceMark
{
    NotInClassroom,
    NotVisibleToTechnicalAccount,
}
