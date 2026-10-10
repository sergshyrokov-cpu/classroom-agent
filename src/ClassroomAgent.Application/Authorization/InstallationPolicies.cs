namespace ClassroomAgent.Application.Authorization;

/// <summary>
/// The cells of the <c>trebovaniya.md</c> §2 permission matrix this Story needs, as policy names
/// (US-008 spec FR-004; SC-4). The matrix lives in one place so it can be compared against the requirements;
/// the rest of it is not implemented speculatively.
/// </summary>
public static class InstallationPolicies
{
    /// <summary>"Просмотр статуса легитимности" — visible to Admin and Dean alike.</summary>
    public const string ViewLegitimacyStatus = "ViewLegitimacyStatus";

    /// <summary>Any authenticated account of the installation; sign-out needs nothing more.</summary>
    public const string AuthenticatedUser = "AuthenticatedUser";

    /// <summary>
    /// "Настройка `WorkspaceConnection` (домен, impersonation)" — ✔ Admin, ✘ Dean (US-009 spec FR-010).
    /// </summary>
    public const string ConfigureWorkspaceConnection = "ConfigureWorkspaceConnection";

    /// <summary>
    /// "Просмотр инструкции по подключению" — ✔ Admin, ✘ Dean (<c>trebovaniya.md</c> §2, v39; US-010 spec
    /// FR-011). Deliberately separate from <see cref="ConfigureWorkspaceConnection"/>: §2 split the two rows
    /// because one is a write read-only mode blocks and the other a read it permits (US-010 spec I-7).
    /// </summary>
    public const string ViewConnectionInstruction = "ViewConnectionInstruction";

    /// <summary>
    /// "Проверить доступ" — ✔ Admin, ✘ Dean (<c>trebovaniya.md</c> §2, v39; US-011 spec FR-011). Its own policy: §2
    /// keeps the three settings rows apart and read-only mode treats them differently (US-011 spec I-9).
    /// </summary>
    public const string RunAccessCheck = "RunAccessCheck";

    /// <summary>
    /// "Запуск синхронизации" — ✔ Admin, ✔ Dean (<c>trebovaniya.md</c> §2, BR-004; US-019 spec FR-007). Its own
    /// policy for its own matrix row (spec I-5).
    /// </summary>
    public const string StartSynchronization = nameof(StartSynchronization);

    /// <summary>
    /// "Просмотр и экспорт журнала успеваемости" — ✔ Admin, ✔ Dean (<c>trebovaniya.md</c> §2; US-025 spec FR-012).
    /// Its own policy for its own matrix row; the export Stories reuse it.
    /// </summary>
    public const string ViewJournal = nameof(ViewJournal);

    /// <summary>
    /// "Использование шаблонов отчётов" — ✔ Admin, ✔ Dean (<c>trebovaniya.md</c> §2 v84; US-027 spec FR-012): the
    /// template list and the report page.
    /// </summary>
    public const string UseReportTemplates = nameof(UseReportTemplates);

    /// <summary>
    /// "Привязка кода встречи Meet к курсу" — ✔ Admin, ✔ Dean (<c>trebovaniya.md</c> §2; US-032 spec §7): the Meet
    /// meetings page and its lists.
    /// </summary>
    public const string ViewMeetCodes = nameof(ViewMeetCodes);

    /// <summary>
    /// "Привязка кода встречи Meet к курсу" — ✔ Admin, ✔ Dean (<c>trebovaniya.md</c> §2; US-032 spec §7): the
    /// course-choice form and the three link writes.
    /// </summary>
    public const string LinkMeetCodes = nameof(LinkMeetCodes);

    /// <summary>
    /// "Создание и редактирование шаблонов отчётов" — ✔ Admin, ✔ Dean (<c>trebovaniya.md</c> §2 v84; US-027 spec
    /// FR-012): the new, copy, change and delete forms and saves.
    /// </summary>
    public const string EditReportTemplates = nameof(EditReportTemplates);

    /// <summary>
    /// US-012 spec FR-016: creating, disabling, re-enabling and resetting the password of a Dean account.
    /// Admin only — the matrix row is "Создание, отключение и включение, сброс пароля учётных записей
    /// Деканов", ✔ Admin, ✘ Dean (<c>trebovaniya.md</c> §2 v64).
    /// </summary>
    public const string ManageDeanAccounts = nameof(ManageDeanAccounts);

    /// <summary>
    /// US-012 spec FR-016: a Dean changing their own password. Dean only — an Admin has no local password at
    /// all (SC-2), so the page answers 403 to one.
    /// </summary>
    public const string ChangeOwnPassword = nameof(ChangeOwnPassword);

    /// <summary>
    /// US-012 spec FR-006, api-design §2.6: the session created at step 5 of the sign-in sequence, which may
    /// reach the forced change form and nothing else.
    /// </summary>
    public const string CompleteTemporaryPasswordChange = nameof(CompleteTemporaryPasswordChange);
}
