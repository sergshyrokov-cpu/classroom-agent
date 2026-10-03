namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Which use case performs which write of the <see cref="PermittedServiceWrite"/> closed list (US-007 spec
/// FR-005). The structural test of AC-007 reads this registry: a write path that is neither guarded nor
/// registered here fails the suite. An entry is added only for a write already on the BR-026 list - and
/// that list grows only by being extended in <c>trebovaniya.md</c> §2 first.
/// </summary>
public static class PermittedServiceWrites
{
    /// <summary>Use-case type to the BR-026 write it performs.</summary>
    public static IReadOnlyDictionary<Type, PermittedServiceWrite> Declarations { get; } =
        new Dictionary<Type, PermittedServiceWrite>
        {
            // US-005: without it the installation could never leave read-only mode (AC-003, AC-009).
            [typeof(CheckLegitimacyUseCase)] = PermittedServiceWrite.LegitimacyCheckState,

            // US-008: creating the Admin's account at first sign-in, stamping the sign-in time and writing the
            // sign-in audit row. Registered against the existing members - the BR-026 list is not widened
            // (US-008 spec FR-013).
            [typeof(CompleteGoogleSignInUseCase)] = PermittedServiceWrite.SignInBookkeeping,

            // US-008 spec FR-016, AC-014: signing out rotates the account's security stamp so the previous cookie
            // stops authenticating. That is sign-in bookkeeping, so it works in read-only mode as well.
            [typeof(AccountSessionService)] = PermittedServiceWrite.SignInBookkeeping,

            // US-012 spec FR-012, FR-013, FR-015: the Dean's sign-in writes only sign-in bookkeeping — the
            // failed-attempt counter, the lockout and the time of the last successful sign-in — which BR-026
            // names explicitly, so the sequence works in read-only mode and takes no guard.
            [typeof(SignInDeanUseCase)] = PermittedServiceWrite.SignInBookkeeping,

            // US-012 spec FR-006, FR-015: the forced change of a temporary password. BR-026 permits "a Dean
            // changing their own password" — without it a Dean whose school is read-only could never sign in
            // at all, because the temporary password must be replaced first.
            [typeof(CompleteTemporaryPasswordChangeUseCase)] = PermittedServiceWrite.SignInBookkeeping,

            // US-012 spec FR-014, FR-015: the same permission for a later, voluntary change.
            [typeof(ChangeOwnPasswordUseCase)] = PermittedServiceWrite.SignInBookkeeping,

            // US-039 spec FR-007: BR-026 names "a user choosing their UI language" in the same entry as the password
            // changes, so the choice works in read-only mode as well. The closed list itself does not grow.
            [typeof(ChooseUiLanguageUseCase)] = PermittedServiceWrite.SignInBookkeeping,
        };
}
