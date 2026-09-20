using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Infrastructure.Secrets;

/// <summary>
/// The configured secret store of the first version (US-008 OD-004, option 1): a reference is the <b>name of an
/// environment variable</b>, which the Owner sets on the installation's server at deployment (DC-2, SC-7). The
/// secret is never in configuration, in the database, in a DTO, in a view or in a log.
/// </summary>
/// <remarks>
/// Nothing is cached in a static field: the value is read from the process environment each time it is asked
/// for, and the caller — the host at start-up — decides what a missing secret means (AGENTS.md: no static
/// mutable state). US-009 decides whether the service-account key reuses this store or a managed one.
/// </remarks>
public sealed class EnvironmentSecretStore : ISecretStore
{
    public string? Resolve(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        var value = Environment.GetEnvironmentVariable(reference.Trim());
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
