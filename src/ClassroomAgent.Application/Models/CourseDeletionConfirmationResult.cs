namespace ClassroomAgent.Application.Models;

/// <summary>US-062 entity model §2.4: the outcome and the number of courses marked (zero unless confirmed).</summary>
public sealed record CourseDeletionConfirmationResult(CourseDeletionConfirmationOutcome Outcome, int ConfirmedCount);
