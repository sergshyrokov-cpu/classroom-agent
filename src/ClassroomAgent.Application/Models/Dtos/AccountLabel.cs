namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>A person on the Meet meetings page: the email, or a deleted account (US-032 spec I-7).</summary>
public sealed record AccountLabel(bool Deleted, string? Email);
