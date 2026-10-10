namespace ClassroomAgent.Application.Models;

/// <summary>
/// The result of a meet-code write (US-032 api-design §2.7): the outcome, the list to show next — the origin list
/// after a success, the code's current list after a stale state — and the page to return to.
/// </summary>
public sealed record MeetCodeChangeResult(MeetCodeChangeOutcome Outcome, MeetCodeList List, int ReturnPage);
