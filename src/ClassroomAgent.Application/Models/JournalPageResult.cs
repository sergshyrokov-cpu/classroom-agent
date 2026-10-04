using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The journal page to render and how it answers (US-025 api-design §5).</summary>
public sealed record JournalPageResult(JournalPageOutcome Outcome, JournalPageModel Page);
