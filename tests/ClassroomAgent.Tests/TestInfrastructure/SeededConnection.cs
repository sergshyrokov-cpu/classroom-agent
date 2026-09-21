namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Which <c>workspace_connection</c> a US-011 test host holds when the test begins (US-009 FR-002 states).</summary>
public enum SeededConnection
{
    /// <summary>No row: the US-009 state <c>NotConfigured</c>.</summary>
    None,

    /// <summary>A row for the <c>Installation</c> domain: <c>Configured</c>, usable.</summary>
    Usable,

    /// <summary>A row for another domain: <c>DomainMismatch</c>, not usable (US-009 OD-002).</summary>
    ForAnotherDomain,
}
