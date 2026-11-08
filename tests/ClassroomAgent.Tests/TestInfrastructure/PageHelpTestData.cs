using System.Net;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-076: the page help — its five pages (spec section 1), its translation keys (api-design §3), its texts verbatim from
/// spec v2 section 4.1 with the journal line 2 of api-design §3.1 (finding SPEC_REVIEW/m-4), and a reader of the rendered
/// native disclosure (spec FR-002 … FR-005). The markup is read by element, not by an id or class: the spec fixes a
/// <c>&lt;details&gt;</c> whose <c>&lt;summary&gt;</c> is the control and leaves class names to implementation.
/// </summary>
public static partial class PageHelpTestData
{
    /// <summary>The five page ids of spec section 1.</summary>
    public static class Pages
    {
        public const string Journal = "journal";

        public const string MeetCodes = "meet-codes";

        public const string MeetReports = "meet-reports";

        public const string Meeting = "meeting";

        public const string ReportTemplates = "report-templates";

        public static IReadOnlyList<string> All { get; } = [Journal, MeetCodes, MeetReports, Meeting, ReportTemplates];

        /// <summary>The pages that also carry the page-search hint (US-046 FR-001); the template list has none.</summary>
        public static IReadOnlyList<string> WithSearchHint { get; } = [Journal, MeetCodes, MeetReports, Meeting];
    }

    /// <summary>Api-design §3: the key of the control's accessible name.</summary>
    public const string ControlNameKey = "PageHelp.Control.Name";

    /// <summary>Spec section 4.1: the control's accessible name.</summary>
    public static string ControlName(string language) => language switch
    {
        "uk" => "Довідка про сторінку",
        "en" => "Help about this page",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    /// <summary>Api-design §3: the key segment of each page id.</summary>
    public static string KeySegment(string page) => page switch
    {
        Pages.Journal => "Journal",
        Pages.MeetCodes => "MeetCodes",
        Pages.MeetReports => "MeetReports",
        Pages.Meeting => "Meeting",
        Pages.ReportTemplates => "ReportTemplates",
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    /// <summary>Api-design §3: the keys of a page's lines, in order.</summary>
    public static IReadOnlyList<string> LineKeys(string page) =>
        Enumerable.Range(1, Lines(page, "uk").Count).Select(n => $"PageHelp.{KeySegment(page)}.Line{n}").ToList();

    /// <summary>Api-design §3: all 18 keys.</summary>
    public static IReadOnlyList<string> AllKeys { get; } =
        [.. Pages.All.SelectMany(LineKeys), ControlNameKey];

    /// <summary>Spec v2 section 4.1 (journal line 2 per api-design §3.1): the lines of a page in a language, in order.</summary>
    public static IReadOnlyList<string> Lines(string page, string language) => (page, language) switch
    {
        (Pages.Journal, "uk") =>
        [
            "Журнал показує оцінки й стан здачі завдань курсу за обраний період так, як їх повернув Google Classroom. Дані оновлює синхронізація, на цій сторінці їх не змінюють.",
            "Позначка «вибув(-ла)» з датою біля імені означає, що учня зараз немає у списку курсу в Classroom; дата — коли синхронізація бачила його востаннє. Позначка «немає в Classroom» під назвою завдання стосується лише цього завдання, а рядок над таблицею «Курсу немає в Classroom з …» або «Дані не оновлювалися з …» — усього курсу.",
            "Вивантаження в Excel робиться на сторінці звіту за шаблоном («Шаблони звітів» → «Звіт»); позначка «вибув(-ла)» там стоїть у примітці до клітинки з ім'ям.",
            "Якщо оцінка неправильна, її виправляє викладач у Classroom, і наступна синхронізація покаже нову. Що означає кожна проблема даних і хто її вирішує, видно на сторінці «Стан даних».",
        ],
        (Pages.Journal, "en") =>
        [
            "The journal shows the grades and submission states of a course's coursework for the chosen period, as Google Classroom returned them. Synchronization refreshes the data; it is not edited on this page.",
            "The mark \"left\" with a date beside a name means the student is not on the course's list in Classroom now; the date is when synchronization last saw them. The mark \"not in Classroom\" under a coursework title concerns only that item, while a line above the table, \"Course not in Classroom since …\" or \"Data not updated since …\", concerns the whole course.",
            "The Excel export is made on the template report page (\"Report templates\" → \"Report\"); there the \"left\" mark is a note on the name cell.",
            "If a grade is wrong, the teacher corrects it in Classroom, and the next synchronization shows the new one. What each data problem means and who solves it is on the \"Data health\" page.",
        ],
        (Pages.MeetCodes, "uk") =>
        [
            "Тут коди зустрічей Meet із даних Google. Classroom не повідомляє, до якого курсу належить зустріч, тому код прив'язують до курсу, і тоді його зустрічі входять у звіти Meet цього курсу.",
            "Програма прив'язує код сама, коли організатор — викладач курсу, а учасники здебільшого збігаються зі списком учнів; інакше код чекає в «Непризначені», і курс обирає Декан або Адмін.",
            "Зустрічі коду без прив'язки не входять у звіти жодного курсу. Код педради, наради чи консультації для кількох груп позначають «не курс».",
            "Помилкову прив'язку виправляють кнопкою «Прив'язати до іншого курсу»: усі зустрічі коду перейдуть у вибраний курс. У режимі лише для читання прив'язки змінювати не можна.",
        ],
        (Pages.MeetCodes, "en") =>
        [
            "These are the Meet meeting codes from Google's data. Classroom does not tell which course a meeting belongs to, so a code is linked to a course, and then its meetings count in that course's Meet reports.",
            "The program links a code itself when the organizer is a teacher of the course and the participants mostly match its list of students; otherwise the code waits under \"Unassigned\", and a Dean or Admin picks the course.",
            "Meetings of a code with no link count in no course's reports. A code of a staff meeting or a consultation for several groups is marked \"not a course\".",
            "A wrong link is corrected with \"Link to another course\": all the code's meetings move to the chosen course. In read-only mode links cannot be changed.",
        ],
        (Pages.MeetReports, "uk") =>
        [
            "Звіти показують, як фактично проходять зустрічі Meet курсу за період. Це не журнал відвідування: заняття могло пройти в аудиторії або з матеріалами в Classroom.",
            "Присутність — це підключення до зустрічі з особистого акаунта домену, який програма знає з Classroom, і хвилини підключення. Дані надходять від Google із затримкою до 24 годин і оновлюються синхронізацією.",
            "Людини може не бути серед учнів курсу, якщо на дату зустрічі її не було у списку курсу (тоді вона в стовпці «Не в списку курсу»), якщо вона підключилася з акаунта, якого програма не знає з Classroom (тоді вона серед «Інші учасники»), або якщо код зустрічі не прив'язаний до цього курсу — це видно на сторінці «Зустрічі Meet».",
        ],
        (Pages.MeetReports, "en") =>
        [
            "The reports show how a course's Meet meetings actually went over the period. This is not an attendance register: a lesson may have been held in class or with materials in Classroom.",
            "Presence is a connection to the meeting from a personal domain account the program knows from Classroom, and the minutes connected. The data arrives from Google with a delay of up to 24 hours and is refreshed by synchronization.",
            "A person may be missing from the course's students if they were not on the course list on the meeting's date (then they are in the \"Not on the course list\" column), if they connected from an account the program does not know from Classroom (then they are among \"Other participants\"), or if the meeting code is not linked to this course — the \"Meet meetings\" page shows that.",
        ],
        (Pages.Meeting, "uk") =>
        [
            "Тут одна зустріч Meet: хто підключився, скільки хвилин був і ким був для курсу на дату зустрічі.",
            "Присутність — це підключення з особистого акаунта домену, який програма знає з Classroom. Дані надходять від Google із затримкою до 24 годин і оновлюються синхронізацією.",
            "Якщо учня немає серед учнів курсу, можливо, на дату зустрічі його не було у списку курсу (тоді біля нього позначка «не в списку курсу») або він підключився з акаунта, якого програма не знає з Classroom (тоді він серед «Інші учасники»). Це не журнал відвідування: заняття могло пройти й без Meet.",
        ],
        (Pages.Meeting, "en") =>
        [
            "This is one Meet meeting: who connected, how many minutes they stayed and who they were for the course on the meeting's date.",
            "Presence is a connection from a personal domain account the program knows from Classroom. The data arrives from Google with a delay of up to 24 hours and is refreshed by synchronization.",
            "If a student is not among the course's students, they may not have been on the course list on the meeting's date (then they are marked \"not on the course list\") or connected from an account the program does not know from Classroom (then they are among \"Other participants\"). This is not an attendance register: a lesson may have been held without Meet.",
        ],
        (Pages.ReportTemplates, "uk") =>
        [
            "Шаблон задає, як із даних курсу виходить звіт: вигляд, шкала оцінювання, позначки станів клітинки і звідки брати імена.",
            "Щоб отримати звіт, натисніть «Звіт» біля шаблону й оберіть курс і період; там же є «Експорт в Excel».",
            "Оцінки у звіті — з даних Google, оновлених синхронізацією; шаблон лише перераховує їх у шкалу школи. Вбудований «Академічний журнал» не змінюють: щоб налаштувати свій, натисніть «Копіювати».",
        ],
        (Pages.ReportTemplates, "en") =>
        [
            "A template sets how a report is made from a course's data: the view, the grading scale, the marks of the cell states and where names are taken from.",
            "To get a report, press \"Report\" beside a template and choose the course and period; \"Export to Excel\" is there too.",
            "The grades in a report come from Google's data refreshed by synchronization; a template only converts them to the school's scale. The built-in \"Academic journal\" is not changed: to set up your own, press \"Copy\".",
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(page), $"{page}/{language}"),
    };

    /// <summary>
    /// Fragments that a source file holding a help text as a literal would contain (spec AC-004): the opening words of
    /// every line in both languages and the control name.
    /// </summary>
    public static IReadOnlyList<string> LiteralFragments { get; } =
    [
        .. Pages.All.SelectMany(p => Lines(p, "uk").Concat(Lines(p, "en"))).Select(line => line[..Math.Min(40, line.Length)]),
        ControlName("uk"),
        ControlName("en"),
    ];

    /// <summary>One rendered help disclosure.</summary>
    /// <param name="Index">Where the <c>&lt;details</c> open tag starts in the page.</param>
    /// <param name="End">Where the markup after <c>&lt;/details&gt;</c> starts.</param>
    /// <param name="Attributes">The raw attributes of the <c>&lt;details&gt;</c> tag.</param>
    /// <param name="SummaryAttributes">The raw attributes of its <c>&lt;summary&gt;</c> tag; null when it has none.</param>
    /// <param name="SummaryHtml">The inner markup of the summary.</param>
    /// <param name="BeforeSummary">The markup inside the details before the summary.</param>
    /// <param name="BodyHtml">The markup inside the details after the summary — the expandable part.</param>
    public sealed record Disclosure(
        int Index,
        int End,
        string Attributes,
        string? SummaryAttributes,
        string SummaryHtml,
        string BeforeSummary,
        string BodyHtml)
    {
        /// <summary>Whether the details tag carries the <c>open</c> attribute.</summary>
        public bool IsOpen => OpenAttribute().IsMatch(Attributes);

        /// <summary>The summary's <c>aria-label</c>, decoded; null when absent.</summary>
        public string? AriaLabel => SummaryAttributes is null ? null : Attribute(SummaryAttributes, "aria-label");

        /// <summary>The summary's text, decoded, tags removed, white space collapsed.</summary>
        public string SummaryText => TextOf(SummaryHtml);

        /// <summary>The decoded text of each <c>&lt;p&gt;</c> of the expandable part, in order.</summary>
        public IReadOnlyList<string> Paragraphs =>
            Paragraph().Matches(BodyHtml).Select(m => TextOf(m.Groups["inner"].Value)).ToList();

        /// <summary>The expandable part with every <c>&lt;p&gt;…&lt;/p&gt;</c> and every bare wrapper tag removed — must be empty.</summary>
        public string BodyLeftover =>
            LeadingFiller().Replace(WrapperTag().Replace(Paragraph().Replace(BodyHtml, string.Empty), string.Empty), string.Empty).Trim();
    }

    /// <summary>Every <c>&lt;details&gt;</c> element of the page (spec FR-002: the help is the only one on its page).</summary>
    public static IReadOnlyList<Disclosure> Disclosures(string html)
    {
        var found = new List<Disclosure>();
        foreach (Match open in DetailsOpen().Matches(html))
        {
            var start = open.Index + open.Length;
            var close = html.IndexOf("</details>", start, StringComparison.OrdinalIgnoreCase);
            var inner = close < 0 ? html[start..] : html[start..close];
            var end = close < 0 ? html.Length : close + "</details>".Length;
            var summary = Summary().Match(inner);
            found.Add(summary.Success
                ? new Disclosure(
                    open.Index,
                    end,
                    open.Groups["attributes"].Value,
                    summary.Groups["attributes"].Value,
                    summary.Groups["inner"].Value,
                    inner[..summary.Index],
                    inner[(summary.Index + summary.Length)..])
                : new Disclosure(open.Index, end, open.Groups["attributes"].Value, null, string.Empty, inner, inner));
        }

        return found;
    }

    /// <summary>
    /// The markup with every <c>&lt;details&gt;</c> element removed — the help is the only one on its page (spec FR-002) —
    /// so that a text assertion about the page does not find a label the help names (spec I-8).
    /// </summary>
    public static string WithoutHelp(string html)
    {
        var result = html;
        foreach (var disclosure in Disclosures(html).Reverse())
        {
            result = result.Remove(disclosure.Index, disclosure.End - disclosure.Index);
        }

        return result;
    }

    /// <summary>
    /// Whether the page carries anything of the help (spec AC-005): a <c>&lt;details&gt;</c> element, the control name or
    /// the opening words of any help line, in either language, or a <c>PageHelp</c> key name.
    /// </summary>
    public static bool HasAnyHelp(string html)
    {
        var text = WebUtility.HtmlDecode(html);
        return DetailsOpen().IsMatch(html)
            || text.Contains("PageHelp", StringComparison.Ordinal)
            || LiteralFragments.Any(f => text.Contains(f, StringComparison.Ordinal));
    }

    /// <summary>
    /// The markup between the first <c>&lt;/h1&gt;</c> inside <c>&lt;main&gt;</c> and the help's <c>&lt;details</c>; null when
    /// either is missing or the details comes before the title. Spec FR-002/FR-004: beside the title, nothing between.
    /// </summary>
    public static string? BetweenTitleAndHelp(string html, Disclosure help)
    {
        var main = html.IndexOf("<main", StringComparison.OrdinalIgnoreCase);
        if (main < 0)
        {
            return null;
        }

        var close = html.IndexOf("</h1>", main, StringComparison.OrdinalIgnoreCase);
        if (close < 0 || close > help.Index)
        {
            return null;
        }

        return html[(close + "</h1>".Length)..help.Index];
    }

    /// <summary>Whether markup holds no text and no element other than bare wrapper tags (white space and comments allowed).</summary>
    public static bool IsOnlyWrapperMarkup(string markup) =>
        LeadingFiller().Replace(WrapperTag().Replace(markup, string.Empty), string.Empty).Trim().Length == 0;

    /// <summary>
    /// The markup after the help, with leading white space, comments and the closing tags of a title wrapper removed — where
    /// the page-search hint must start (spec FR-004, AC-002).
    /// </summary>
    public static string AfterHelp(string html, Disclosure help) =>
        ClosingFiller().Replace(html[help.End..], string.Empty);

    /// <summary>The <c>src</c> of every <c>&lt;script&gt;</c> of the page.</summary>
    public static IReadOnlyList<string?> ScriptSources(string html) =>
        ScriptOpen().Matches(html).Select(m => Attribute(m.Groups["attributes"].Value, "src")?.Split('?')[0]).ToList(); // US-077 FR-009: without the content version

    /// <summary>Markup in the help that would make it more than text (spec FR-008, SC-15): a link, a form, a script, a style.</summary>
    public static IReadOnlyList<string> ForbiddenMarkup(string markup) =>
        Forbidden().Matches(markup).Select(m => m.Value).ToList();

    private static string TextOf(string html) =>
        WhiteSpace().Replace(WebUtility.HtmlDecode(AnyTag().Replace(html, " ")), " ").Trim();

    private static string? Attribute(string attributes, string name)
    {
        var match = Regex.Match(
            attributes,
            @"(?:^|\s)" + Regex.Escape(name) + @"\s*=\s*(""(?<v>[^""]*)""|'(?<v>[^']*)')",
            RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value) : null;
    }

    [GeneratedRegex(@"<details\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex DetailsOpen();

    [GeneratedRegex(@"<summary\b(?<attributes>[^>]*)>(?<inner>.*?)</summary>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Summary();

    [GeneratedRegex(@"<p\b[^>]*>(?<inner>.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Paragraph();

    [GeneratedRegex(@"(?:^|\s)open(?:\s*=|\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex OpenAttribute();

    [GeneratedRegex(@"</?(?:div|span)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex WrapperTag();

    [GeneratedRegex(@"^(?:\s+|<!--.*?-->)*", RegexOptions.Singleline)]
    private static partial Regex LeadingFiller();

    [GeneratedRegex(@"^(?:\s+|<!--.*?-->|</div\s*>)*", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ClosingFiller();

    [GeneratedRegex(@"<script\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOpen();

    [GeneratedRegex(
        @"<(?:a|form|input|button|select|textarea|script|style|iframe|img|link)\b|\s(?:href|src|style|on[a-z]+)\s*=",
        RegexOptions.IgnoreCase)]
    private static partial Regex Forbidden();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
