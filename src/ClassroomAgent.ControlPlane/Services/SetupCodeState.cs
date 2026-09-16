namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// The current one-time setup code, held only in process memory (FR-002). A restart
/// loses it; creating the Owner account voids it.
/// </summary>
public sealed class SetupCodeState
{
    private readonly Lock _gate = new();
    private string? _code;

    public void Set(string code)
    {
        lock (_gate)
        {
            _code = code;
        }
    }

    public void Void()
    {
        lock (_gate)
        {
            _code = null;
        }
    }

    public bool Matches(string? submitted)
    {
        string? code;
        lock (_gate)
        {
            code = _code;
        }

        return code is not null && SetupCodeComparer.Matches(code, submitted);
    }
}
