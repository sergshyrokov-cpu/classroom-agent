using System.Collections.Concurrent;
using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Substitutes the operator console so the test can assert what was printed.</summary>
public sealed class CapturingOperatorConsole : IOperatorConsole
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines.ToArray();

    public void WriteLine(string line) => _lines.Enqueue(line);
}
