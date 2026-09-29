namespace ClassroomAgent.Application.Models;

/// <summary>
/// The retention period N, in years (US-015 entity model §8; §5's retention period). FR-011's age rule runs
/// inside <c>RunSynchronizationUseCase</c>, which is why this lives in <c>Application</c> rather than <c>Web</c>
/// (AD-3). Narrow on purpose, as <see cref="SchoolDefaults"/> is (US-013 security review F-1): the whole
/// <c>InstallationSettings</c> record stays out of the container (SC-7).
/// </summary>
public sealed record RetentionSettings(int Years);
