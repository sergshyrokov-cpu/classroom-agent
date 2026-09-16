namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Writes operator output straight to standard output, bypassing the logging pipeline (FR-002, SC-10).</summary>
public sealed class StandardOutputOperatorConsole : IOperatorConsole
{
    public void WriteLine(string line) => Console.Out.WriteLine(line);
}
