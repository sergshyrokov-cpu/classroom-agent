namespace ClassroomAgent.Application.Models;

/// <summary>One stored course for the journal drop-down (US-025 entity model §3, db-design Q1).</summary>
public sealed record JournalCourseRecord(long Id, string Name, string? Section);
