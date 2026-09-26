namespace ClassroomAgent.Application.Models;

/// <summary>What one step of the access check does (US-011 spec FR-001; openapi <c>AccessCheckStep.kind</c>).</summary>
public enum AccessCheckStepKind
{
    /// <summary>A delegated-token request for one scope (spec FR-002).</summary>
    Delegation,

    /// <summary>The minimal Classroom read (spec FR-003).</summary>
    ClassroomRead,

    /// <summary>The minimal Admin Reports read (spec FR-003).</summary>
    ReportsRead,
}
