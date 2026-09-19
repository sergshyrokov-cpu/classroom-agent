namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Outcome of a push address submission (US-006 entity model §3); an expected result, never an exception (AD-9).</summary>
public enum ChangePushAddressResult
{
    NotFound,
    Unchanged,
    Changed,
}
