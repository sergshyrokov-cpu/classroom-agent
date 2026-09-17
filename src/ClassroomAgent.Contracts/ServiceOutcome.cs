namespace ClassroomAgent.Contracts;

/// <summary>Body of a <c>400</c> or <c>404</c> answer on the service channel (US-005 api-design §4). Wire type only.</summary>
public sealed record ServiceOutcome(string Outcome)
{
    /// <summary>The request body is missing, malformed or breaks a rule (<c>400</c>).</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>No <c>Installation</c> has the installation id (<c>404</c>).</summary>
    public const string UnknownInstallation = "unknown_installation";
}
