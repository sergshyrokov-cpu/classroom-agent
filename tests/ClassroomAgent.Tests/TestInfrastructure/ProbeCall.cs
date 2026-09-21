namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One recorded call: its kind, the impersonated account and scope of a delegation, the token of a read.</summary>
public sealed record ProbeCall(
    ProbeCallKind Kind,
    string? TechnicalAccount,
    string? Scope,
    string? Token,
    bool DuringRequest);
