namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Generates the one-time setup code (FR-002, OD-005).</summary>
public interface ISetupCodeGenerator
{
    string Generate();
}
