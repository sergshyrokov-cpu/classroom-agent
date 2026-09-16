using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Substitutes the setup-code generator so the test knows the current code.</summary>
public sealed class FixedSetupCodeGenerator(string code) : ISetupCodeGenerator
{
    private int _calls;

    public string Code { get; } = code;

    public int Calls => Volatile.Read(ref _calls);

    public string Generate()
    {
        Interlocked.Increment(ref _calls);
        return Code;
    }
}
