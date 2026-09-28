namespace ClassroomAgent.Application.Models;

/// <summary>
/// A course the run did not import because Classroom reported a state outside the five values
/// <c>trebovaniya.md</c> §3 lists (US-014 spec FR-003, OD-010).
/// </summary>
/// <param name="GoogleId">The Classroom course id — an identifier, never the course name (SC-10).</param>
/// <param name="State">The unrecognised state string, as Google sent it.</param>
/// <remarks>
/// The run reports the skip so the host can write the one <c>Warning</c> line OD-010 requires: the Application
/// layer has no logger of its own, and <c>SyncState</c> has one counter which cannot carry a skipped count
/// (spec FR-013, FR-016). Both values are safe for a log line; neither is personal data.
/// </remarks>
public sealed record SkippedCourse(string GoogleId, string State);
