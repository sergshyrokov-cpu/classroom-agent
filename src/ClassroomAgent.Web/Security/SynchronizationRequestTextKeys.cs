using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the "Synchronize" button (US-019 spec FR-008; openapi <c>SynchronizationMessageKey</c>).
/// Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class SynchronizationRequestTextKeys
{
    public const string Button = "Synchronization.Request.Button";

    public const string Requested = "Synchronization.Request.Requested";

    public const string RequestedAfterCurrentWork = "Synchronization.Request.RequestedAfterCurrentWork";

    public const string ConnectionNotUsableAdmin = "Synchronization.Request.ConnectionNotUsableAdmin";

    public const string ConnectionNotUsableDean = "Synchronization.Request.ConnectionNotUsableDean";

    /// <summary>
    /// The message for an outcome. An unusable connection is worded per role (OD-009 a): the Admin saves it, the
    /// Dean asks the Admin.
    /// </summary>
    public static string Of(RequestSynchronizationOutcome outcome, bool isDean) => outcome switch
    {
        RequestSynchronizationOutcome.Requested => Requested,
        RequestSynchronizationOutcome.RequestedAfterCurrentWork => RequestedAfterCurrentWork,
        RequestSynchronizationOutcome.ConnectionNotUsable => isDean ? ConnectionNotUsableDean : ConnectionNotUsableAdmin,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
