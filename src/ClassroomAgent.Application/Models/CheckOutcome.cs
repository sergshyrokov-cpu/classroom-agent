using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// The result of one legitimacy check (US-005 spec FR-007, FR-010): successful when <see cref="Failure"/> is
/// null. Status and compatibility are those of the answer, when there was one. Two outcomes are the same
/// result exactly when they are equal.
/// </summary>
public sealed record CheckOutcome(LegitimacyCheckFailure? Failure, InstallationStatus? Status, CompatibilityState? Compatibility)
{
    public bool Succeeded => Failure is null;

    public static CheckOutcome Success(InstallationStatus status, CompatibilityState compatibility) => new(null, status, compatibility);

    public static CheckOutcome Failed(LegitimacyCheckFailure failure, InstallationStatus? status = null, CompatibilityState? compatibility = null) =>
        new(failure, status, compatibility);
}
