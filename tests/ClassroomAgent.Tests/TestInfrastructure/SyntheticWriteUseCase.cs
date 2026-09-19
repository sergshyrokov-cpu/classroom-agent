using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// A compliant write path for US-007: it calls the guard first and only then stages a row and commits
/// (spec FR-002, AC-001). No real write use case exists yet, so the enforcement is proven on this one and
/// every later Story proves its own (TC-5, Story Notes). Its write is deliberately not declared a
/// permitted service write.
/// </summary>
public sealed class SyntheticWriteUseCase(
    IReadOnlyModeGuard guard,
    ILegitimacyStateRepository states,
    IUnitOfWork unitOfWork)
{
    /// <summary>The operation constant the refusal carries (spec VR-001).</summary>
    public const string Operation = "SyntheticWrite";

    /// <summary>The domain this use case writes, so a test can tell whether the write happened.</summary>
    public const string MarkerDomain = "synthetic-write.example.test";

    public async Task ExecuteAsync(DateTimeOffset checkedAt, CancellationToken cancellationToken)
    {
        await guard.EnsureAllowedAsync(Operation, cancellationToken);
        await WriteAsync(states, unitOfWork, checkedAt, cancellationToken);
    }

    /// <summary>
    /// Stages the marker row - creating it or moving the existing one - and commits. It writes only the
    /// domain, through <c>RecordUpgradeRequired</c>, so the write never moves the last successful check or
    /// the status: a fixture that changed those would change the very mode the test is judging.
    /// </summary>
    internal static async Task WriteAsync(
        ILegitimacyStateRepository states,
        IUnitOfWork unitOfWork,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        _ = checkedAt;
        var state = await states.GetAsync(cancellationToken);
        if (state is null)
        {
            states.Add(LegitimacyState.FromUpgradeRequired(
                InstallationStatus.Active,
                MarkerDomain,
                InstallationTestData.ClientId));
        }
        else
        {
            state.RecordUpgradeRequired(state.Status, MarkerDomain, InstallationTestData.ClientId);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
