namespace ClassroomAgent.Contracts;

/// <summary>
/// Admin login check answer (US-008 api-design §3). Exactly one property: no entry id, no list, no other
/// school's data and no hint about whether the email exists elsewhere (SC-12, S-16).
/// </summary>
public sealed record AdminLoginCheckResponse(bool Allowed);
