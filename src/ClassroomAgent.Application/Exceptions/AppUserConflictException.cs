namespace ClassroomAgent.Application.Exceptions;

/// <summary>
/// A concurrent change of the same <c>app_user</c> row (US-048 spec FR-005): thrown by the unit of work for a
/// concurrency-stamp mismatch on an <c>AppUser</c>, after it has removed the failed account entry and the staged
/// audit rows, so <c>Application</c> never sees a provider type. The Dean sign-in sequence answers it by running
/// again on a fresh read (spec FR-003, FR-004); every other writer of the account lets it propagate.
/// </summary>
public sealed class AppUserConflictException : Exception
{
    public AppUserConflictException()
    {
    }

    public AppUserConflictException(Exception innerException)
        : base("The account was changed concurrently.", innerException)
    {
    }
}
