using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Exceptions;

/// <summary>
/// A write or a Google call refused because the installation is in read-only mode (US-007 spec FR-003;
/// BR-025). It carries the reason as data so a caller renders it in Ukrainian or English (NFR-073), and no
/// HTTP concept: the mapping to <c>409</c> belongs to the host's exception handler (AD-9, API-5).
/// </summary>
public sealed class ReadOnlyModeException(
    LegitimacyModeReason reason,
    DateTimeOffset? lastSuccessfulCheckAt,
    string operation)
    : Exception("The installation is in read-only mode; the operation was refused.")
{
    /// <summary>Which of the three reasons of BR-025 applies.</summary>
    public LegitimacyModeReason Reason { get; } = reason;

    /// <summary>The last successful check; null for <see cref="LegitimacyModeReason.NotYetConfirmed"/>.</summary>
    public DateTimeOffset? LastSuccessfulCheckAt { get; } = lastSuccessfulCheckAt;

    /// <summary>The refused operation, as the caller named it (spec VR-001).</summary>
    public string Operation { get; } = operation;
}
