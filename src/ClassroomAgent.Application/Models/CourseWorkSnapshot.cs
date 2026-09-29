using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// One coursework or material item as <see cref="ClassroomAgent.Application.Ports.IClassroomReader"/> reports it
/// (US-015 entity model §6). The FR-008 date cascade is already applied in the adapter, so only the resulting
/// instant crosses the boundary.
/// </summary>
/// <param name="GoogleId">The Classroom id of the item.</param>
/// <param name="Resource">Which Classroom resource this snapshot came from.</param>
/// <param name="Details">The item's other fields, as the <c>Domain</c> type <see cref="CourseWorkDetails"/>.</param>
public sealed record CourseWorkSnapshot(string GoogleId, CourseWorkResource Resource, CourseWorkDetails Details);
