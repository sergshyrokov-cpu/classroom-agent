using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// One course as <see cref="ClassroomAgent.Application.Ports.IClassroomReader"/> reports it (US-014 entity model
/// §6). <see cref="State"/> stays the string Google sent, precisely so the use case can apply OD-010 and skip an
/// unrecognised value — parsing into <c>CourseState</c> happens in the use case, not the adapter.
/// </summary>
/// <param name="GoogleId">The Classroom course id.</param>
/// <param name="State">The state as the string Google sent, not yet parsed into <c>CourseState</c>.</param>
/// <param name="Details">The course's other fields.</param>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-014 OD-012; IMPLEMENTATION owns it from here.
/// </remarks>
public sealed record CourseSnapshot(string GoogleId, string State, CourseDetails Details);
