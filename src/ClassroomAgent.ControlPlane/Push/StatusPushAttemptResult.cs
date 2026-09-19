namespace ClassroomAgent.ControlPlane.Push;

/// <summary>How one push attempt ended (US-006 api-design §6). An outcome, never an exception (AD-9).</summary>
public enum StatusPushAttemptResult
{
    /// <summary>The installation answered <c>202</c>: delivered, no retry.</summary>
    Delivered,

    /// <summary>The installation answered <c>404</c>: the id is not its own; no retry.</summary>
    Refused,

    /// <summary>Any other status: a failed attempt that is retried.</summary>
    UnexpectedStatus,

    /// <summary>No connection: refused, reset or a name that does not resolve.</summary>
    ConnectionFailed,

    /// <summary>No response status within the attempt timeout.</summary>
    Timeout,
}
