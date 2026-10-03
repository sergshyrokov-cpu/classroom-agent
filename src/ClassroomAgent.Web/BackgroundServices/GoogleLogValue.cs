namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// The one bound on a Google-supplied value written to a log line (US-017 spec FR-011, I-4): a course or submission
/// id, a raw course state, a raw submission state. A longer value would let one odd response grow the log without
/// limit.
/// </summary>
public static class GoogleLogValue
{
    /// <summary>The longest a Google-supplied value may be in a log line.</summary>
    public const int MaxLength = 64;

    /// <summary>The value itself when it fits, otherwise its first <see cref="MaxLength"/> characters.</summary>
    public static string Bounded(string value) => value.Length <= MaxLength ? value : value[..MaxLength];
}
