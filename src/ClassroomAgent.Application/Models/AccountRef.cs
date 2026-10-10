namespace ClassroomAgent.Application.Models;

/// <summary>An account named by a link row: the bare id and the account's email, null when the account no longer exists (US-032 spec I-7).</summary>
public sealed record AccountRef(long Id, string? Email);
