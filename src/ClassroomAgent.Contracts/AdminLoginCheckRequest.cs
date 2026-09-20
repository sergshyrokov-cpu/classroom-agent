namespace ClassroomAgent.Contracts;

/// <summary>
/// Admin login check request (US-008 api-design §3). Wire type only: the members are nullable so a missing
/// property reaches validation instead of a default value. The email travels in the body, never in the address
/// (<c>trebovaniya.md</c> §8, v64).
/// </summary>
public sealed record AdminLoginCheckRequest(Guid? InstallationId, string? Email);
