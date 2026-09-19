namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The closed list of service writes that still run in read-only mode (BR-026; <c>trebovaniya.md</c> §2;
/// US-007 spec FR-004). This enum is the only place the list is expressed in code: no use case carries a
/// private exemption. A new service write is permitted only by extending the list in
/// <c>trebovaniya.md</c> §2 first, and then here.
/// </summary>
public enum PermittedServiceWrite
{
    /// <summary>BR-026: audit rows.</summary>
    AuditEvent,

    /// <summary>
    /// BR-026: Identity failed-attempt counting and lockout, the time of the last successful sign-in,
    /// creating the <c>AppUser</c> of an approved Admin at first sign-in, a Dean changing their own
    /// password, and a user choosing their UI language.
    /// </summary>
    SignInBookkeeping,

    /// <summary>
    /// BR-026: the legitimacy-check state - last successful check time, last known status, last
    /// compatibility state, and the installation's domain and client ID. Without it the installation
    /// could never leave read-only mode.
    /// </summary>
    LegitimacyCheckState,

    /// <summary>BR-026: the retention purge and its audit event (BR-075).</summary>
    RetentionPurge,
}
