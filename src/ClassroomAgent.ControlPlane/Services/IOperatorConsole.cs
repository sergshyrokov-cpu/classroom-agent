namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// Operator output on the server console (standard output), outside the logging
/// pipeline (FR-002, SC-10).
/// </summary>
public interface IOperatorConsole
{
    void WriteLine(string line);
}
