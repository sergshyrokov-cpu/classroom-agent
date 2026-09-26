namespace ClassroomAgent.Application.Models;

/// <summary>The overall verdict of a check (US-011 spec FR-001; openapi <c>AccessCheckVerdict</c>).</summary>
public enum AccessCheckVerdict
{
    /// <summary>All eight steps succeeded.</summary>
    AccessInPlace,

    /// <summary>At least one step failed for a configuration cause.</summary>
    NotConfigured,

    /// <summary>No configuration failure, but at least one step could not be completed because Google did not answer.</summary>
    Inconclusive,
}
