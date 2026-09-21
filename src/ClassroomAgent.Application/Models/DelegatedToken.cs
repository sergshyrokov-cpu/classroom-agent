namespace ClassroomAgent.Application.Models;

/// <summary>
/// An access token issued for one scope, held only for the duration of one check and never stored, logged or
/// returned to a browser (US-011 spec FR-002, S-07).
/// </summary>
/// <remarks>Compile-only skeleton created at TEST_WRITING under US-011 OD-006.</remarks>
public sealed class DelegatedToken
{
    public DelegatedToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => throw new NotImplementedException();
}
