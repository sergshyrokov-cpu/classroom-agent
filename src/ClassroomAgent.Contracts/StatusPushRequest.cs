namespace ClassroomAgent.Contracts;

/// <summary>
/// Body of a status-change push (US-006 api-design §3). Wire type only: the installation id is nullable so
/// a missing property reaches validation instead of an empty UUID, and nothing else is carried (SC-12).
/// </summary>
public sealed record StatusPushRequest(Guid? InstallationId);
