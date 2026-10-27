namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-060 spec FR-003.1: the two header lines of the journal, the report and the export.</summary>
public enum CourseDataNoticeKind
{
    /// <summary>"Course not in Classroom since …" (BR-048).</summary>
    NotInClassroom,

    /// <summary>"Data not updated since …" — not visible to the technical account or skipped (BR-046, BR-049).</summary>
    NotUpdated,
}
