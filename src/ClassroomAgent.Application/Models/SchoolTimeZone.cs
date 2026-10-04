namespace ClassroomAgent.Application.Models;

/// <summary>
/// The school's time zone (US-025 spec FR-010, DC-3, NFR-074): period boundaries and every date the journal shows are
/// in it. Narrow on purpose, as <see cref="RetentionSettings"/> is: the whole installation settings record stays out
/// of the container (SC-7).
/// </summary>
public sealed record SchoolTimeZone(TimeZoneInfo Zone);
