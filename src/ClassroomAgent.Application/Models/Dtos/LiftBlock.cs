namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-038 <c>LiftBlock</c>: the typed email — kept only on a VR-003 re-render or "no exclusion" — its field error
/// and whether no exclusion matched (spec FR-009).
/// </summary>
public sealed record LiftBlock(string? Email, LiftFieldError? FieldError, bool NoExclusionForEmail)
{
    /// <summary>An empty form.</summary>
    public static LiftBlock Empty { get; } = new(null, null, false);
}
