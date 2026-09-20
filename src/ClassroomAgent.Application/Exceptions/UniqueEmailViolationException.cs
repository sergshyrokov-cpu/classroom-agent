namespace ClassroomAgent.Application.Exceptions;

/// <summary>
/// The database refused an account because one with that address already exists (US-008 db-design §3.3; spec
/// I-10). Infrastructure translates the provider's unique-violation into this, so <c>Application</c> can treat a
/// concurrent first sign-in as "someone else created it" without knowing anything about Npgsql (AD-3, AD-4).
/// </summary>
public sealed class UniqueEmailViolationException(Exception innerException)
    : Exception("An account with that address already exists.", innerException)
{
}
