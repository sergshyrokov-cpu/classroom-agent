namespace ClassroomAgent.Application.Models.Export;

/// <summary>
/// US-043 spec FR-002: the fields of the shipped form. Each marks one cell of the file; the renderer finds the cell by
/// its marker text, so no cell address of the form appears in code (OD-008 a).
/// </summary>
public enum BlankFormMarker
{
    Course,
    StudentNumber,
    StudentName,
    LessonDate,
    Grade,
    Teachers,
    TopicNumber,
    TopicDate,
    TopicHours,
    TopicTitle,
}
