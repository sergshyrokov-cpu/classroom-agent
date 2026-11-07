namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-075 spec FR-008: the new hint texts and the Dean's X4 title, Ukrainian and English, verbatim from the table (the
/// US-062 precedent: the page tests pin literals). The translation-file test also pins the keys, which API design §3
/// fixed. The apostrophes are ASCII, as <see cref="DataHealthHostExtensions.Text"/> reads them.
/// </summary>
public sealed record DataHealthHintTexts(
    string Language,
    string AboveMaximum,
    string ItemsNotInClassroom,
    string NoRecentSuccess,
    string NoSuccessYet,
    string CourseListEmpty,
    string CoursesNotVisible,
    string UnknownCourseState,
    string DeanCourseListEmptySince,
    string AdminRoleInstruction)
{
    public static DataHealthHintTexts Uk { get; } = new(
        "uk",
        "Зазвичай це помилка введення: викладач виправляє оцінку або максимум завдання в Classroom, і після наступної синхронізації проблема зникає",
        "Завдання видалено або повернуто в чернетку в Classroom, оцінки в програмі збережено. Якщо це помилка, зверніться до викладача.",
        "Перегляньте стан синхронізації на сторінці підключення. Якщо запуски не починаються, повідомте Власника.",
        "Перевірте налаштування на сторінці підключення. Перша синхронізація великої школи може тривати понад добу.",
        "З меншими ролями Google курсів не показує. Роль технічного акаунта змінює супер-адміністратор школи в консолі адміністратора Google.",
        "Якщо акаунт змінено помилково, поверніть попередній або дайте новому потрібну роль, і позначка зникне сама. Якщо курси справді видалено, позначте їх і підтвердьте.",
        "Google повернув стан курсу, якого ця версія програми не знає, і курс не оновлюється. Повідомте Власника.",
        "Google не повертає жодного курсу з {0}",
        "перевірте роль технічного акаунта");

    public static DataHealthHintTexts En { get; } = new(
        "en",
        "Usually an entry error: the teacher corrects the grade or the item's maximum in Classroom, and the problem goes after the next synchronization",
        "The item was deleted or returned to draft in Classroom; its grades stay in the program. If that was a mistake, contact the teacher.",
        "Look at the synchronization state on the connection page. If runs do not start, tell the Owner.",
        "Check the settings on the connection page. A large school's first synchronization can take over a day.",
        "With lesser roles Google shows no courses. The school's super-admin changes the technical account's role in the Google Admin console.",
        "If the account was changed by mistake, restore the previous one or give the new one the needed role, and the mark goes by itself. If the courses really were deleted, select them and confirm.",
        "Google returned a course state this version of the program does not know, and the course is not updated. Tell the Owner.",
        "Google has returned no course since {0}",
        "check the technical account's role");

    public static DataHealthHintTexts Of(string language) => language == "en" ? En : Uk;

    /// <summary>The five FR-003 technical hints (Admin only).</summary>
    public IReadOnlyList<string> Technical => [NoRecentSuccess, NoSuccessYet, CourseListEmpty, CoursesNotVisible, UnknownCourseState];

    /// <summary>Every new hint of FR-002 and FR-003.</summary>
    public IReadOnlyList<string> All => [AboveMaximum, ItemsNotInClassroom, .. Technical];

    /// <summary>The hint of a technical kind (FR-003).</summary>
    public string Of(TechnicalKind kind) => kind switch
    {
        TechnicalKind.NoRecentSuccess => NoRecentSuccess,
        TechnicalKind.NoSuccessYet => NoSuccessYet,
        TechnicalKind.UnknownCourseState => UnknownCourseState,
        TechnicalKind.CourseListEmpty => CourseListEmpty,
        TechnicalKind.CoursesNotVisible => CoursesNotVisible,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The technical kinds US-075 gives a hint (spec FR-003: X2a, X2b, X3, X4, X5).</summary>
    public enum TechnicalKind
    {
        NoRecentSuccess,
        NoSuccessYet,
        UnknownCourseState,
        CourseListEmpty,
        CoursesNotVisible,
    }
}
