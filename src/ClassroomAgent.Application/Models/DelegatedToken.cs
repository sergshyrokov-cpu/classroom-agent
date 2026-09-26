namespace ClassroomAgent.Application.Models;

/// <summary>
/// An access token issued for one scope, held only for the duration of one check and never stored, logged or
/// returned to a browser (US-011 spec FR-002, S-07). <see cref="ToString"/> never prints it, so an accidental log
/// or interpolation cannot leak it.
/// </summary>
public sealed class DelegatedToken
{
    public DelegatedToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The bearer value; only the Google adapter reads it.</summary>
    public string Value { get; }

    public override string ToString() => "[delegated token]";
}
