namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>What a push asked of the check scheduler (US-006 api-design §5). Both answers are <c>202</c>.</summary>
public enum PushCheckRequestResult
{
    /// <summary>A legitimacy check starts now; the minute starts now.</summary>
    Started,

    /// <summary>A check is running or the minute has not passed: one pending check is remembered (spec FR-010, v77).</summary>
    Deferred,
}
