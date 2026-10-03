using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// A signed-in Admin or Dean chooses their own UI language (US-039 spec FR-004, FR-007): the code is validated
/// here (VR-001), the account is the session's own, and the write is the BR-026 permitted "user chooses their
/// UI language", so it runs in read-only mode too. No audit row (OD-005) and no security-stamp rotation (I-3).
/// </summary>
public sealed class ChooseUiLanguageUseCase(
    IAppUserRepository users,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    /// <summary>The stored language, or null when the code is refused or the account does not exist.</summary>
    public async Task<UiLanguage?> ExecuteAsync(long accountId, string? languageCode, CancellationToken cancellationToken)
    {
        if (LanguageOf(languageCode) is not { } language)
        {
            return null;
        }

        var user = await users.FindByIdAsync(accountId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.ChooseUiLanguage(language);
        using (writeScope.Declare(PermittedServiceWrite.SignInBookkeeping))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return language;
    }

    /// <summary>
    /// VR-001: exactly <c>uk</c> or <c>en</c>, compared ordinally as strings — never parsed as an enum, which would
    /// accept numbers and any casing (api-design §2.5).
    /// </summary>
    private static UiLanguage? LanguageOf(string? code) => code switch
    {
        "uk" => UiLanguage.Uk,
        "en" => UiLanguage.En,
        _ => null,
    };
}
