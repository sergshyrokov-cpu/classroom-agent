using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>
/// The reads of the Meet meetings page (US-032 db-design §5). The unassigned page is ordered by a candidate test that
/// must run in SQL (db-design §5.2), so its page of codes is one raw-SQL query; everything around it is LINQ. All reads
/// are untracked; accounts named by a link are resolved from <c>app_user</c> by bare id (null email when the row is
/// gone, spec I-7). Code order is ordinal (<c>COLLATE "C"</c>).
/// </summary>
public sealed class MeetCodesReadSource(ClassroomAgentDbContext db) : IMeetCodesReadSource
{
    public async Task<MeetCodeListCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        var unassigned = await db.MeetSessions
            .AsNoTracking()
            .Where(s => !db.MeetingCodeLinks.Any(l => l.MeetingCode == s.MeetingCode))
            .Select(s => s.MeetingCode)
            .Distinct()
            .CountAsync(cancellationToken);
        var linked = await db.MeetingCodeLinks.AsNoTracking().CountAsync(l => l.CourseId != null, cancellationToken);
        var marked = await db.MeetingCodeLinks.AsNoTracking().CountAsync(l => l.CourseId == null, cancellationToken);
        return new MeetCodeListCounts(unassigned, linked, marked);
    }

    public async Task<PagedRows<UnassignedCodeRow>> GetUnassignedPageAsync(
        int page,
        int size,
        TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var total = (await GetCountsAsync(cancellationToken)).Unassigned;

        // The school's calendar day on both sides, converted by PostgreSQL from the IANA id (db-design §5.2); the
        // same rule as RosterOnDate. An organizer with a teacher membership covering the meeting's day is a candidate.
        var zoneId = zone.Id;
        var offset = (long)page * size;
        var codes = await db.Database
            .SqlQuery<UnassignedCodeRecord>($"""
                SELECT c.meeting_code AS meeting_code,
                       c.first_started_at AS first_started_at,
                       c.last_started_at AS last_started_at,
                       c.meeting_count AS meeting_count,
                       EXISTS (
                           SELECT 1
                           FROM meet_session m
                           JOIN classroom_participant p ON lower(p.email) = lower(m.organizer_email)
                           JOIN course_membership cm ON cm.participant_id = p.id AND cm.role = 'teacher'
                           WHERE m.meeting_code = c.meeting_code
                             AND (cm.first_seen_at AT TIME ZONE {zoneId})::date <= (m.started_at AT TIME ZONE {zoneId})::date
                             AND (cm.on_roster
                                  OR (cm.last_seen_at AT TIME ZONE {zoneId})::date >= (m.started_at AT TIME ZONE {zoneId})::date)
                       ) AS has_candidate
                FROM (
                    SELECT s.meeting_code,
                           min(s.started_at) AS first_started_at,
                           max(s.started_at) AS last_started_at,
                           count(*)::integer AS meeting_count
                    FROM meet_session s
                    WHERE NOT EXISTS (SELECT 1 FROM meeting_code_link l WHERE l.meeting_code = s.meeting_code)
                    GROUP BY s.meeting_code
                ) c
                ORDER BY has_candidate DESC, c.last_started_at DESC, c.meeting_code COLLATE "C"
                LIMIT {size} OFFSET {offset}
                """)
            .ToListAsync(cancellationToken);
        if (codes.Count == 0)
        {
            return new PagedRows<UnassignedCodeRow>([], total);
        }

        var pageCodes = codes.Select(c => c.MeetingCode).ToList();
        var organizers = await db.MeetSessions
            .AsNoTracking()
            .Where(s => pageCodes.Contains(s.MeetingCode))
            .Select(s => new { s.MeetingCode, s.OrganizerEmail })
            .Distinct()
            .ToListAsync(cancellationToken);
        var organizersByCode = organizers
            .GroupBy(o => o.MeetingCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(o => o.OrganizerEmail).OrderBy(e => e, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        // Connections per code: distinct lowered emails with a count each, so the page never pulls individual connections.
        var emailGroups = await db.MeetParticipations
            .AsNoTracking()
            .Join(
                db.MeetSessions.Where(s => pageCodes.Contains(s.MeetingCode)),
                p => p.MeetSessionId,
                s => s.Id,
                (p, s) => new { s.MeetingCode, p.Email })
            .GroupBy(x => new { x.MeetingCode, Email = x.Email == null ? null : x.Email.ToLower() })
            .Select(g => new { g.Key.MeetingCode, g.Key.Email, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var rows = new List<UnassignedCodeRow>(codes.Count);
        foreach (var code in codes)
        {
            var codeOrganizers = organizersByCode.TryGetValue(code.MeetingCode, out var list) ? list : [];
            var organizerSet = new HashSet<string>(codeOrganizers, StringComparer.OrdinalIgnoreCase);
            var own = emailGroups
                .Where(g => string.Equals(g.MeetingCode, code.MeetingCode, StringComparison.Ordinal))
                .ToList();
            var domainAccounts = own.Count(g => g.Email is not null && !organizerSet.Contains(g.Email));
            var other = own.Where(g => g.Email is null).Sum(g => g.Count);
            rows.Add(new UnassignedCodeRow(
                code.MeetingCode,
                codeOrganizers,
                code.FirstStartedAt,
                code.LastStartedAt,
                code.MeetingCount,
                domainAccounts,
                other,
                code.HasCandidate));
        }

        return new PagedRows<UnassignedCodeRow>(rows, total);
    }

    public async Task<PagedRows<LinkedCodeRow>> GetLinkedPageAsync(int page, int size, CancellationToken cancellationToken)
    {
        var total = await db.MeetingCodeLinks.AsNoTracking().CountAsync(l => l.CourseId != null, cancellationToken);
        var items = await db.MeetingCodeLinks
            .AsNoTracking()
            .Where(l => l.CourseId != null)
            .Select(l => new
            {
                l.MeetingCode,
                CourseId = l.CourseId!.Value,
                CourseName = db.Courses.Where(c => c.Id == l.CourseId).Select(c => c.Name).First(),
                CourseSection = db.Courses.Where(c => c.Id == l.CourseId).Select(c => c.Section).First(),
                LinkedAutomatically = l.LinkedAutomatically!.Value,
                l.LinkedByAppUserId,
                LinkedAt = l.LinkedAt!.Value,
                l.ConfirmedByAppUserId,
                l.ConfirmedAt,
                MeetingCount = db.MeetSessions.Count(s => s.MeetingCode == l.MeetingCode),
                LastStartedAt = db.MeetSessions
                    .Where(s => s.MeetingCode == l.MeetingCode)
                    .Max(s => (DateTimeOffset?)s.StartedAt),
            })
            .OrderBy(x => x.LastStartedAt == null)
            .ThenByDescending(x => x.LastStartedAt)
            .ThenBy(x => EF.Functions.Collate(x.MeetingCode, "C"))
            .Skip((int)Math.Min((long)page * size, int.MaxValue))
            .Take(size)
            .ToListAsync(cancellationToken);

        var accounts = await LoadEmailsAsync(
            items.SelectMany(i => new[] { i.LinkedByAppUserId, i.ConfirmedByAppUserId }).OfType<long>(),
            cancellationToken);

        AccountRef? Ref(long? id) => id is null ? null : new AccountRef(id.Value, accounts.GetValueOrDefault(id.Value));

        var rows = items
            .Select(i => new LinkedCodeRow(
                i.MeetingCode,
                i.CourseId,
                i.CourseName,
                i.CourseSection,
                i.LinkedAutomatically,
                Ref(i.LinkedByAppUserId),
                i.LinkedAt,
                Ref(i.ConfirmedByAppUserId),
                i.ConfirmedAt,
                i.MeetingCount,
                i.LastStartedAt))
            .ToList();
        return new PagedRows<LinkedCodeRow>(rows, total);
    }

    public async Task<PagedRows<MarkedCodeRow>> GetMarkedPageAsync(int page, int size, CancellationToken cancellationToken)
    {
        var total = await db.MeetingCodeLinks.AsNoTracking().CountAsync(l => l.CourseId == null, cancellationToken);
        var items = await db.MeetingCodeLinks
            .AsNoTracking()
            .Where(l => l.CourseId == null)
            .Select(l => new
            {
                l.MeetingCode,
                MarkedByAppUserId = l.MarkedByAppUserId!.Value,
                MarkedAt = l.MarkedAt!.Value,
                MeetingCount = db.MeetSessions.Count(s => s.MeetingCode == l.MeetingCode),
                LastStartedAt = db.MeetSessions
                    .Where(s => s.MeetingCode == l.MeetingCode)
                    .Max(s => (DateTimeOffset?)s.StartedAt),
            })
            .OrderByDescending(x => x.MarkedAt)
            .ThenBy(x => EF.Functions.Collate(x.MeetingCode, "C"))
            .Skip((int)Math.Min((long)page * size, int.MaxValue))
            .Take(size)
            .ToListAsync(cancellationToken);

        var accounts = await LoadEmailsAsync(items.Select(i => i.MarkedByAppUserId), cancellationToken);
        var rows = items
            .Select(i => new MarkedCodeRow(
                i.MeetingCode,
                new AccountRef(i.MarkedByAppUserId, accounts.GetValueOrDefault(i.MarkedByAppUserId)),
                i.MarkedAt,
                i.MeetingCount,
                i.LastStartedAt))
            .ToList();
        return new PagedRows<MarkedCodeRow>(rows, total);
    }

    public async Task<IReadOnlyList<CourseOption>> GetAllCoursesAsync(CancellationToken cancellationToken) =>
        await db.Courses
            .AsNoTracking()
            .Select(c => new CourseOption(c.Id, c.Name, c.Section))
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<long, string>> LoadEmailsAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        return await db.AppUsers
            .AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);
    }

    /// <summary>The columns of the raw unassigned-page query; settable properties because EF materializes by name.</summary>
    private sealed class UnassignedCodeRecord
    {
        public string MeetingCode { get; set; } = string.Empty;

        public DateTimeOffset FirstStartedAt { get; set; }

        public DateTimeOffset LastStartedAt { get; set; }

        public int MeetingCount { get; set; }

        public bool HasCandidate { get; set; }
    }
}
