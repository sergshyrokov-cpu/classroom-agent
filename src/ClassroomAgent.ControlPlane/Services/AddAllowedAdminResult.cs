namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Result of <see cref="AllowedAdminRegistry.AddAsync"/>: the outcome and, for a wrong domain, the
/// installation's domain the message names (api-design §4 step 5).
/// </summary>
public sealed record AddAllowedAdminResult(AddAllowedAdminOutcome Outcome, string? ExpectedDomain)
{
    public static AddAllowedAdminResult Added { get; } = new(AddAllowedAdminOutcome.Added, null);

    public static AddAllowedAdminResult NotFound { get; } = new(AddAllowedAdminOutcome.NotFound, null);

    public static AddAllowedAdminResult Taken { get; } = new(AddAllowedAdminOutcome.Taken, null);

    public static AddAllowedAdminResult WrongDomain(string expectedDomain) => new(AddAllowedAdminOutcome.WrongDomain, expectedDomain);
}
