using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// A final failure of a Google read, as the adapter reports it upward (US-017 spec FR-004): its class and, except for
/// <see cref="GoogleReadFailureKind.CourseGone"/>, the diagnosis the run records. Carries no Google detail — no
/// message, reason, status text or body — so nothing raw can reach <c>SyncState</c> or a log (SC-10).
/// </summary>
/// <remarks>Created at TEST_WRITING under US-017 OD-010; a data-carrying type, written in full.</remarks>
public sealed class GoogleReadFailedException : Exception
{
    public GoogleReadFailedException(GoogleReadFailureKind kind, SyncDiagnosis? diagnosis)
        : base("A Google read failed: " + kind + ".")
    {
        Kind = kind;
        Diagnosis = diagnosis;
    }

    public GoogleReadFailureKind Kind { get; }

    public SyncDiagnosis? Diagnosis { get; }
}
