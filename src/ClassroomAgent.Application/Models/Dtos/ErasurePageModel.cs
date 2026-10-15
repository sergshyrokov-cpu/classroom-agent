namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// Openapi US-038 <c>ErasurePageModel</c>: the read-only notice (display only, spec FR-014), the one-time message, the
/// search block and the lifting block. No Google user id, fingerprint or exclusion data (spec S-05, I-14).
/// </summary>
public sealed record ErasurePageModel(
    bool ReadOnlyNotice,
    ErasureMessageKey? MessageKey,
    SearchBlock Search,
    LiftBlock Lift);
