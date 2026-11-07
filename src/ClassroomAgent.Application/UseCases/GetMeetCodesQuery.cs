using System.Globalization;
using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>The Meet meetings page (US-032 spec FR-007, VR-004; api-design §2.2). Reads only; works in read-only mode.</summary>
/// <remarks>
/// One list at a time, paginated per API-8. Shares are computed by <see cref="MeetCodeScorer"/> over the data as it is
/// now (spec I-8) for the codes on the requested page only; the ordering "codes with candidates first" comes from the
/// read source. Dates and times are the school's (NFR-074); codes, emails and course names are passed as stored.
/// </remarks>
public sealed class GetMeetCodesQuery(
    IMeetCodesReadSource reads,
    IMeetCodeScoringSource scoring,
    SchoolTimeZone zone)
{
    /// <summary>API-8: the default page size.</summary>
    public const int DefaultSize = 20;

    /// <summary>API-8: the largest page size.</summary>
    public const int MaxSize = 100;

    /// <summary>The page for a raw query; an invalid query gives the Unassigned list at its defaults with <c>QueryInvalid</c>.</summary>
    public async Task<MeetCodesPageResult> ExecuteAsync(
        MeetCodesRequest request,
        MeetCodesMessageKey? message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var list = One(request.List, ParseList, MeetCodeList.Unassigned);
        var page = One(request.Page, value => Whole(value, 0, int.MaxValue), 0);
        var size = One(request.Size, value => Whole(value, 1, MaxSize), DefaultSize);

        // US-070 VR-003, I-5, I-13: a refused text reads nothing and takes precedence over every other message.
        if (CourseListQueryRules.ReadSearch(request.Q, out var search) is { } refused)
        {
            return new MeetCodesPageResult(
                true,
                new MeetCodesPageModel(
                    list ?? MeetCodeList.Unassigned, null, [], [], [], page ?? 0, size ?? DefaultSize, 0, 0, null, null, refused));
        }

        if (list is not { } chosen || page is not { } number || size is not { } pageSize)
        {
            var defaults = await BuildAsync(MeetCodeList.Unassigned, 0, DefaultSize, MeetCodesMessageKey.QueryInvalid, search, cancellationToken);
            return new MeetCodesPageResult(true, defaults);
        }

        return new MeetCodesPageResult(false, await BuildAsync(chosen, number, pageSize, message, search, cancellationToken));
    }

    /// <summary>
    /// A list and page chosen by the host (after a write, a stale state or an unknown code), default size, with the search
    /// already validated (US-070 FR-005).
    /// </summary>
    public Task<MeetCodesPageModel> ExecuteAsync(
        MeetCodeList list,
        int page,
        MeetCodesMessageKey? message,
        string? search,
        CancellationToken cancellationToken) =>
        BuildAsync(list, Math.Max(page, 0), DefaultSize, message, search, cancellationToken);

    /// <summary>The query value of a list (api-design §2.2).</summary>
    public static string ListValue(MeetCodeList list) => list switch
    {
        MeetCodeList.Linked => "linked",
        MeetCodeList.NotACourse => "not-a-course",
        _ => "unassigned",
    };

    private async Task<MeetCodesPageModel> BuildAsync(
        MeetCodeList list,
        int requestedPage,
        int size,
        MeetCodesMessageKey? message,
        string? search,
        CancellationToken cancellationToken)
    {
        var counts = await reads.GetCountsAsync(search, cancellationToken);

        // US-070 I-6, api-design D-4: a page past the last shows the last, known from the counts before any row is read.
        var shownCount = list switch
        {
            MeetCodeList.Linked => counts.Linked,
            MeetCodeList.NotACourse => counts.NotACourse,
            _ => counts.Unassigned,
        };
        var lastPage = (int)(((long)shownCount + size - 1) / size) - 1;
        var page = Math.Clamp(requestedPage, 0, Math.Max(lastPage, 0));

        IReadOnlyList<UnassignedCodeItem> unassigned = [];
        IReadOnlyList<LinkedCodeItem> linked = [];
        IReadOnlyList<NotACourseCodeItem> marked = [];
        long total;
        switch (list)
        {
            case MeetCodeList.Linked:
                var linkedRows = await reads.GetLinkedPageAsync(page, size, search, cancellationToken);
                linked = linkedRows.Rows.Select(Map).ToList();
                total = linkedRows.TotalElements;
                break;
            case MeetCodeList.NotACourse:
                var markedRows = await reads.GetMarkedPageAsync(page, size, search, cancellationToken);
                marked = markedRows.Rows.Select(Map).ToList();
                total = markedRows.TotalElements;
                break;
            default:
                var unassignedRows = await reads.GetUnassignedPageAsync(page, size, zone.Zone, search, cancellationToken);
                unassigned = await MapAsync(unassignedRows.Rows, cancellationToken);
                total = unassignedRows.TotalElements;
                break;
        }

        var totalPages = (int)((total + size - 1) / size);
        return new MeetCodesPageModel(list, counts, unassigned, linked, marked, page, size, total, totalPages, message, search, null);
    }

    private async Task<IReadOnlyList<UnassignedCodeItem>> MapAsync(
        IReadOnlyList<UnassignedCodeRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var inputs = await scoring.GetScoringInputsAsync(rows.Select(r => r.MeetingCode).ToList(), cancellationToken);
        var scores = inputs.ToDictionary(i => i.MeetingCode, i => MeetCodeScorer.Score(i, zone.Zone), StringComparer.Ordinal);
        var courses = (await reads.GetAllCoursesAsync(cancellationToken)).ToDictionary(c => c.Id);

        return rows
            .Select(row => new UnassignedCodeItem(
                row.MeetingCode,
                Organizers(row.Organizers),
                RosterOnDate.LocalDate(row.FirstStartedAt, zone.Zone),
                RosterOnDate.LocalDate(row.LastStartedAt, zone.Zone),
                row.MeetingCount,
                row.DomainAccounts + row.OtherParticipants,
                scores.TryGetValue(row.MeetingCode, out var score) ? Candidates(score, courses) : []))
            .ToList();
    }

    /// <summary>FR-007: best share first, then course name in the UI language's collation, then id.</summary>
    internal static IReadOnlyList<CandidateCourse> Candidates(MeetCodeScore score, IReadOnlyDictionary<long, CourseOption> courses) =>
        score.Candidates
            .Where(c => courses.ContainsKey(c.CourseId))
            .Select(c => (Share: c, Course: courses[c.CourseId]))
            .OrderByDescending(c => c.Share.Numerator)
            .ThenBy(c => c.Course.Name, StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: false))
            .ThenBy(c => c.Course.Id)
            .Select(c => new CandidateCourse(c.Course, c.Share.PercentRoundedDown))
            .ToList();

    private LinkedCodeItem Map(LinkedCodeRow row) =>
        new(
            row.MeetingCode,
            Organizers(row.Organizers),
            new CourseOption(row.CourseId, row.CourseName, row.CourseSection),
            row.LinkedAutomatically,
            row.LinkedAutomatically || row.LinkedBy is null ? null : Label(row.LinkedBy),
            Local(row.LinkedAt),
            row.ConfirmedBy is null ? null : Label(row.ConfirmedBy),
            row.ConfirmedAt is { } confirmed ? Local(confirmed) : null,
            row.LinkedAutomatically && row.ConfirmedAt is null,
            row.MeetingCount,
            row.LastStartedAt is { } last ? RosterOnDate.LocalDate(last, zone.Zone) : null);

    private NotACourseCodeItem Map(MarkedCodeRow row) =>
        new(
            row.MeetingCode,
            Organizers(row.Organizers),
            Label(row.MarkedBy),
            Local(row.MarkedAt),
            row.MeetingCount,
            row.LastStartedAt is { } last ? RosterOnDate.LocalDate(last, zone.Zone) : null);

    /// <summary>
    /// US-074 entity model §3, db-design §2 step 3: each organizer as a person; a blank chosen name is no name, by the rule
    /// the Meet reports apply (spec AC-009).
    /// </summary>
    private static IReadOnlyList<PersonRef> Organizers(IReadOnlyList<MeetOrganizerRecord> organizers) =>
        organizers.Select(o => new PersonRef(MeetReportRules.Name(o.StoredFullName), o.Email)).ToList();

    /// <summary>Spec I-7: the account's email, or a deleted account.</summary>
    private static AccountLabel Label(AccountRef account) => new(account.Email is null, account.Email);

    private DateTime Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, zone.Zone).DateTime;

    private static MeetCodeList? ParseList(string? value) => value switch
    {
        "unassigned" => MeetCodeList.Unassigned,
        "linked" => MeetCodeList.Linked,
        "not-a-course" => MeetCodeList.NotACourse,
        _ => null,
    };

    private static int? Whole(string? value, int min, int max) =>
        value is { Length: > 0 } && value.All(char.IsAsciiDigit)
        && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        && number >= min && number <= max
            ? number
            : null;

    /// <summary>VR-004: absent → the default; exactly one valid value → it; anything else → invalid (null).</summary>
    private static T? One<T>(IReadOnlyList<string?> occurrences, Func<string?, T?> parse, T absent)
        where T : struct =>
        occurrences.Count switch
        {
            0 => absent,
            1 => parse(occurrences[0]),
            _ => null,
        };
}
