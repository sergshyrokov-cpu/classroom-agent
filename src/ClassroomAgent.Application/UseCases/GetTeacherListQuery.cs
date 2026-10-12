using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The teacher list (US-022 spec FR-001 … FR-016). Reads only: no read-only guard, no unit of work, no audit, no Google
/// port (spec FR-013).
/// </summary>
/// <remarks>
/// Order (api-design §3): the shape of <c>status</c> and <c>page</c> → the count (T1) → the page (T2, only when someone
/// is listed). Nothing is read before the query passes (spec FR-010, S-07). Ordering and paging happen in the port
/// (db-design §3); this class keeps the order it receives. At most two reads per request (spec FR-014).
/// </remarks>
public sealed class GetTeacherListQuery(ITeacherListSource source)
{
    /// <summary>Spec FR-006, OD-006.</summary>
    public const int PageSize = 50;

    public async Task<TeacherListPageResult> ExecuteAsync(TeacherListRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Spec VR-001, VR-002: the course list's rules and tokens (api-design §2.2).
        var messages = new List<TeacherListMessageKey>();
        if (!CourseListQueryRules.TryReadStatus(request.Status, out var status))
        {
            messages.Add(TeacherListMessageKey.StatusMalformed);
        }

        if (!CourseListQueryRules.TryReadPage(request.Page, out var page))
        {
            messages.Add(TeacherListMessageKey.PageMalformed);
        }

        if (messages.Count > 0)
        {
            // Spec FR-010: the rejected field shows its default and appears nowhere, the return path included.
            return new TeacherListPageResult(
                TeacherListPageOutcome.Invalid,
                new TeacherListPageModel(status, messages, null, TeacherListUrl.Build(status, 1)));
        }

        var state = CourseListQueryRules.StateOf(status);
        var total = await source.CountTeachersAsync(state, cancellationToken);
        TeacherListResult list;
        if (total == 0)
        {
            // Spec FR-008, FR-006: page 1 with the empty message; the page is not read.
            list = new TeacherListResult(true, 0, 1, 1, PageSize, []);
        }
        else
        {
            // Spec FR-006: a page past the last shows the last.
            var pageCount = (total + PageSize - 1) / PageSize;
            var shown = Math.Min(page, pageCount);
            var records = await source.GetTeacherPageAsync(state, (shown - 1) * PageSize, PageSize, cancellationToken);
            var rows = records
                .Select(r => new TeacherListRow(
                    r.ParticipantId,
                    CoursePersonNameRule.NameOf(r.Surname, r.GivenName, r.Email),
                    string.IsNullOrEmpty(r.Email) ? null : r.Email,
                    r.CourseCount,
                    CourseListUrl.Build(status, r.ParticipantId, null, 1)))
                .ToList();
            list = new TeacherListResult(false, total, shown, pageCount, PageSize, rows);
        }

        return new TeacherListPageResult(
            TeacherListPageOutcome.Shown,
            new TeacherListPageModel(status, [], list, TeacherListUrl.Build(status, list.Page)));
    }
}
