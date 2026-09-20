namespace ClassroomAgent.Application.Models;

/// <summary>
/// The Control Plane's answer to the Admin login check, as a closed set of outcomes rather than a boolean
/// (US-008 spec FR-008). Only <see cref="Allowed"/> admits the user; each of the other three carries its own
/// audit and log category.
/// </summary>
public enum AdminLoginCheckReply
{
    /// <summary>An <c>AllowedAdmin</c> entry with this email exists for this installation.</summary>
    Allowed,

    /// <summary>It does not.</summary>
    NotAllowed,

    /// <summary>The Control Plane does not know this installation id.</summary>
    UnknownInstallation,

    /// <summary>Unreachable, timed out, an error answer, or an answer that did not parse.</summary>
    Unavailable,
}
