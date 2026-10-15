using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-003: one membership of the participant — the course name as stored and the role.</summary>
public sealed record ErasureCourseRecord(string CourseName, ClassroomRole Role);
