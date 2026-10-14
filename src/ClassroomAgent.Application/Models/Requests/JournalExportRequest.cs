using System.Text.Json;

namespace ClassroomAgent.Application.Models.Requests;

/// <summary>
/// US-028 entity model §3.2: the raw query of a journal export, validated in the use case. US-043 api-design §2.3 adds
/// the layout (a string, as the orientation) and the two per-copy members as raw JSON values, so that a string, a
/// fraction or a boolean is refused naming its field; JSON <c>null</c> is the same as absent (spec I-12).
/// </summary>
public sealed record JournalExportRequest(
    string? Template,
    string? CourseId,
    string? From,
    string? To,
    string? Names,
    string? Orientation,
    string? Layout = null,
    JsonElement? StudentsPerCopy = null,
    JsonElement? LessonsPerCopy = null);
