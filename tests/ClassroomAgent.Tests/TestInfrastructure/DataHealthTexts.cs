using System.Globalization;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-062 spec FR-007 and OD-003: the texts the tests pin, Ukrainian and English, verbatim from the tables (the
/// US-054 / US-060 precedent: literals, not translation keys — the keys are IMPLEMENTATION's). A text the
/// Specification gives in Ukrainian only is null in English and not asserted there. Dates are the calendar date of the
/// instant in the school's zone (NFR-074, TC-8) in the language's short date pattern, as US-060 shows them.
/// </summary>
public sealed record DataHealthTexts(
    string Language,
    string PageTitle,
    string AboveMaximum,
    string ItemsNotInClassroom,
    string NotUpdatedSince,
    string NotUpdated,
    string NeverLoaded,
    string CourseNotInClassroomSince,
    string HintSkipped,
    string HintNotInClassroom,
    string LastRunFailed,
    string NoRecentSuccess,
    string NoSuccessYet,
    string UnknownCourseState,
    string CourseListEmptySince,
    string CoursesNotVisible,
    string ControlPlaneUnreachable,
    string TellTheAdmin,
    string ContactTheAdmin,
    string TellTheOwner,
    string None,
    string Confirmed,
    string ListChanged,
    string Untitled,
    string? NoProblems,
    string? ArchivedHeading,
    string? ConfirmButton)
{
    public static DataHealthTexts Uk { get; } = new(
        "uk",
        "Стан даних",
        "Бали вище максимуму",
        "Завдання або матеріали, яких немає в Classroom",
        "Курс не оновлюється з {0}",
        "Курс не оновлюється",
        "Курс ще не завантажено; пропускається з {0}",
        "Курсу немає в Classroom з {0}",
        "курс не оновлюється: у Classroom введено неприпустиме значення балів, зверніться до викладача",
        "курс видалено або приховано в Classroom, уточніть у викладача",
        "Остання синхронізація завершилася помилкою",
        "Успішної синхронізації не було понад 24 години",
        "Успішної синхронізації ще не було",
        "Курс пропущено: стан курсу, якого програма не знає",
        "Google не повертає жодного курсу з {0} — перевірте роль технічного акаунта",
        "Технічний акаунт не бачить курсів, які бачив попередній",
        "Немає зв'язку із сервісом з {0}; з {1} програма перейде в режим «лише для читання», якщо зв'язок не відновиться",
        "Повідомте Адміністратора",
        "Якщо з викладачем вирішити не вдалося, зверніться до Адміністратора",
        "Повідомте Власника",
        "Немає",
        "Позначено як видалені в Classroom: {0}",
        "Список змінився — оновіть сторінку",
        "Курс без назви ({0})",
        "Проблем із даними немає",
        "Архівні курси (не враховуються)",
        "Підтвердити: видалені в Classroom");

    public static DataHealthTexts En { get; } = new(
        "en",
        "Data health",
        "Points above the maximum",
        "Items not in Classroom",
        "The course has not been updated since {0}",
        "The course is not being updated",
        "The course has not been loaded yet; skipped since {0}",
        "The course is not in Classroom since {0}",
        "the course is not updated: an invalid points value was entered in Classroom, contact the teacher",
        "the course was deleted or hidden in Classroom, check with the teacher",
        "The last synchronization failed",
        "No successful synchronization for more than 24 hours",
        "No successful synchronization yet",
        "Course skipped: a course state the program does not know",
        "Google has returned no course since {0} — check the technical account's role",
        "The technical account does not see courses the previous one saw",
        "No connection with the service since {0}; from {1} the program will switch to read-only mode unless the connection is restored",
        "Tell the Admin",
        "If it could not be solved with the teacher, contact the Admin",
        "Tell the Owner",
        "None",
        "Marked as deleted in Classroom: {0}",
        "The list has changed — reload the page",
        "Untitled course ({0})",
        null,
        null,
        null);

    public static DataHealthTexts Of(string language) => language == "en" ? En : Uk;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language);

    /// <summary>FR-004.6, NFR-074: the calendar date of the instant in Kyiv, in this language's short date pattern.</summary>
    public string Date(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, JournalTestData.Kyiv).DateTime).ToString("d", Culture);

    public string With(string format, DateTimeOffset instant) => string.Format(Culture, format, Date(instant));

    public string With(string format, DateTimeOffset first, DateTimeOffset second) =>
        string.Format(Culture, format, Date(first), Date(second));

    /// <summary>
    /// FR-003.2, OD-005 a: «N технічних проблем, M навчальних» with the language's plural forms — Ukrainian one / few /
    /// many, English one / other; both numbers always, zero included. The examples are the Specification's
    /// («1 технічна проблема, 3 навчальні» / «1 technical problem, 3 teaching»); English "teaching" does not inflect.
    /// </summary>
    public string Banner(int technical, int teaching) => Language == "en"
        ? $"{technical} {(technical == 1 ? "technical problem" : "technical problems")}, {teaching} teaching"
        : $"{technical} {Ukrainian(technical, "технічна проблема", "технічні проблеми", "технічних проблем")}, "
          + $"{teaching} {Ukrainian(teaching, "навчальна", "навчальні", "навчальних")}";

    private static string Ukrainian(int n, string one, string few, string many) =>
        (n % 10, n % 100) switch
        {
            (1, not 11) => one,
            (2 or 3 or 4, < 12 or > 14) => few,
            _ => many,
        };
}
