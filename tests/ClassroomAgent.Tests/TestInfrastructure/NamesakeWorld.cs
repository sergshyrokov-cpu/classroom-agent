namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-066: the invented students the namesake tests seed (TC-4) and the texts the rows must show. Each student is
/// written with the full name the journal shows (US-025) and the surname and given name the report shows from the
/// profile (US-042, «Прізвище Ім'я»). Ordinals fix the participant ids' order, so namesakes keep a known order (FR-005).
/// </summary>
public static class NamesakeWorld
{
    public const string CourseName = "Test Namesakes Course";

    /// <summary>The one graded item (maximum 12, dated 2026-09-05); every student has the grade 10 on it.</summary>
    public const string GradedTitle = "Test Namesakes Graded";

    public const int GradedMaximum = 12;

    public const int Grade = 10;

    public static readonly DateTimeOffset GradedDate = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A leaver's last sighting, 2026-09-09 21:45 UTC — 10.09.2026 in Kyiv (as US-060's <c>L</c>).</summary>
    public static readonly DateTimeOffset LeftLastSeen = DataMarksWorld.LeftLastSeen;

    /// <summary>The domain that is not the school's (spec I-3).</summary>
    public const string OtherDomain = "other.example";

    /// <summary>The first «Іван Петренко»: AC-001 … AC-009.</summary>
    public static NamesakeStudent PetrenkoA { get; } =
        new(661, "Іван Петренко", "Петренко", "Іван", CourseTestData.Email("ivan.petrenko.a"));

    /// <summary>The second «Іван Петренко», a later participant id than <see cref="PetrenkoA"/>.</summary>
    public static NamesakeStudent PetrenkoB { get; } =
        new(662, "Іван Петренко", "Петренко", "Іван", CourseTestData.Email("ivan.petrenko.b"));

    /// <summary>A name nobody else has.</summary>
    public static NamesakeStudent Koval { get; } =
        new(663, "Олена Коваль", "Коваль", "Олена", CourseTestData.Email("olena.koval"));

    /// <summary>AC-005: the same name as <see cref="PetrenkoA"/> in lower case.</summary>
    public static NamesakeStudent PetrenkoLower { get; } =
        new(664, "іван петренко", "петренко", "іван", CourseTestData.Email("ivan.petrenko.c"));

    /// <summary>AC-008: <see cref="PetrenkoB"/>'s name with no email in Google.</summary>
    public static NamesakeStudent PetrenkoNoEmail { get; } = new(665, "Іван Петренко", "Петренко", "Іван", null);

    /// <summary>
    /// Spec I-3: the same part before <c>@</c> as <see cref="PetrenkoA"/> at another domain, and a profile name of
    /// its own, so it coincides with <see cref="PetrenkoA"/> only in the email variant.
    /// </summary>
    public static NamesakeStudent PetrenkoOtherDomain { get; } =
        new(666, "Іван Петрук", "Петрук", "Іван", "ivan.petrenko.a@" + OtherDomain);

    /// <summary>
    /// FR-002.5: no name in Google, so the journal shows the full email; the two addresses differ only in letter
    /// case and so coincide.
    /// </summary>
    public static NamesakeStudent MailUpper { get; } = new(667, null, null, null, CourseTestData.Email("Same.Mail"));

    public static NamesakeStudent MailLower { get; } = new(668, null, null, null, CourseTestData.Email("same.mail"));

    /// <summary>The report's profile label (US-042 spec FR-003 I-1).</summary>
    public static string Profile(NamesakeStudent student) => student.Surname + " " + student.GivenName;

    /// <summary>The report's email-variant label: the stored address up to its first <c>@</c>.</summary>
    public static string LocalPart(NamesakeStudent student) => student.Email![..student.Email!.IndexOf('@', StringComparison.Ordinal)];

    /// <summary>Spec FR-004.1, OD-001 a: «&lt;name&gt; (&lt;email&gt;)» — one space, parentheses, one line.</summary>
    public static string WithEmail(string name, NamesakeStudent student) => name + " (" + student.Email + ")";
}
