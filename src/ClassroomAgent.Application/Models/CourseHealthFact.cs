using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>US-062 entity model §2.2, db-design §5.1: one course's stored facts the problems are computed from.</summary>
public sealed record CourseHealthFact(
    long CourseId,
    string GoogleId,
    string Name,
    CourseState State,
    CourseAbsenceMark? AbsenceMark,
    DateTimeOffset? AbsentSince,
    DateTimeOffset? LastCompleteReadAt,
    bool HasGradeAboveMaximum,
    bool HasItemNotInClassroom);
