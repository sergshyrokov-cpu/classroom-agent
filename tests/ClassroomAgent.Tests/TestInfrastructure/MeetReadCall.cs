namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One call of <see cref="FakeMeetReportsReader"/>: as whom and for which window.</summary>
public sealed record MeetReadCall(string ImpersonationUser, DateTimeOffset From, DateTimeOffset To);
