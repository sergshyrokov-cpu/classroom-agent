using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// When a value recorded by a legitimacy check may be used (US-009 spec FR-003; US-010 spec FR-003, I-1).
/// A value counts only when a check has actually **succeeded**: an <c>upgrade_required</c> answer records the
/// domain and the client ID without moving the last successful check, and licenses nothing.
/// </summary>
/// <remarks>
/// Written once so the connection settings and the super-admin instruction cannot decide "known" two subtly
/// different ways — US-010 spec I-1 requires the existing rule to be reused rather than restated.
/// </remarks>
public static class ConfirmedLegitimacy
{
    /// <summary>The <c>Installation</c> domain of the last successful check, or null when none has succeeded.</summary>
    public static string? DomainOf(LegitimacyState? state) => ValueOf(state, state?.Domain);

    /// <summary>The service account's client ID of the last successful check, or null when none has succeeded.</summary>
    public static string? ClientIdOf(LegitimacyState? state) => ValueOf(state, state?.ClientId);

    private static string? ValueOf(LegitimacyState? state, string? value) =>
        state?.LastSuccessfulCheckAt is null || string.IsNullOrWhiteSpace(value) ? null : value;
}
