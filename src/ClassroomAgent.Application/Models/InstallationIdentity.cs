namespace ClassroomAgent.Application.Models;

/// <summary>
/// What the installation reports about itself on every check (US-005 spec FR-001, FR-007, I-7): the configured
/// installation id, its release version <c>MAJOR.MINOR.PATCH</c> and the contract version it speaks.
/// </summary>
public sealed record InstallationIdentity(Guid InstallationId, string ApplicationVersion, int ContractVersion);
