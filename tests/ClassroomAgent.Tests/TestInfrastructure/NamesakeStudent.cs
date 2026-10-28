namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One invented student; <see cref="LeftLastSeen"/> set makes them a leaver (off the roster since then).</summary>
public sealed record NamesakeStudent(
    int Ordinal,
    string? FullName,
    string? Surname,
    string? GivenName,
    string? Email,
    DateTimeOffset? LeftLastSeen = null)
{
    public NamesakeStudent Leaving() => this with { LeftLastSeen = NamesakeWorld.LeftLastSeen };
}
