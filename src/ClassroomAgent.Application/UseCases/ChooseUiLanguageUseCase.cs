using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// A signed-in Admin or Dean chooses their own UI language (US-039 spec FR-004, FR-007): the code is validated
/// here (VR-001), the account is the session's own, and the write is the BR-026 permitted "user chooses their
/// UI language", so it runs in read-only mode too.
/// </summary>
/// <remarks>US-039 OD-008: compile-only skeleton, not registered in DI; IMPLEMENTATION writes the body.</remarks>
public sealed class ChooseUiLanguageUseCase(
    IAppUserRepository users,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope)
{
    private readonly IAppUserRepository _users = users;

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private readonly ServiceWriteScope _writeScope = writeScope;

    /// <summary>The stored language, or null when the code is refused or the account does not exist.</summary>
    public Task<UiLanguage?> ExecuteAsync(long accountId, string? languageCode, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
