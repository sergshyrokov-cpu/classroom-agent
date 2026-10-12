namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// A participant with the label key of US-020 db-design §3.4 (profile name, else email, else null), as a query shape
/// shared by the course list's teacher choice and the teacher list (US-022 db-design §3.3).
/// </summary>
internal sealed class LabeledParticipant
{
    public long Id { get; init; }

    public string? Surname { get; init; }

    public string? GivenName { get; init; }

    public string? Email { get; init; }

    public string? Label { get; init; }
}
