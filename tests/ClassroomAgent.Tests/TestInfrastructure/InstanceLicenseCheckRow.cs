namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One <c>instance_license_check</c> row as stored (US-005 db-design §3).</summary>
public sealed record InstanceLicenseCheckRow(
    long Id,
    long InstallationId,
    DateTimeOffset AnsweredAt,
    string ApplicationVersion,
    int ContractVersion,
    string AnsweredStatus,
    string AnsweredCompatibility,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
