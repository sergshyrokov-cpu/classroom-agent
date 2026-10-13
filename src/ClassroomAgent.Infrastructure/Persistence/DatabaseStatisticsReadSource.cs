using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace ClassroomAgent.Infrastructure.Persistence;

/// <summary>US-024 db-design §3.1: the eleven counters in one statement; a non-entity query, so nothing is tracked.</summary>
/// <remarks>
/// One round trip and one statement-level snapshot, whatever the size of the school (spec FR-011, AC-017). The state and
/// role codes are the stored codes of <c>CourseConfiguration</c> and <c>CourseMembershipConfiguration</c>
/// (db-design §2). The last sub-query is the SQL form of <see cref="MeetCodesReadSource.GetCountsAsync"/>'s Unassigned
/// count, so the two numbers agree (AC-018). Counts arrive as <c>bigint</c> and are narrowed with a checked conversion:
/// an overflow is an unexpected failure, not a wrong number (api-design §2.8).
/// </remarks>
public sealed class DatabaseStatisticsReadSource(ClassroomAgentDbContext db) : IDatabaseStatisticsSource
{
    public async Task<DatabaseCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        var row = await db.Database
            .SqlQuery<CountsRecord>($"""
                SELECT
                  (SELECT count(*) FROM course WHERE course_state = 'active') AS active_courses,
                  (SELECT count(*) FROM course WHERE course_state = 'archived') AS archived_courses,
                  (SELECT count(*) FROM course WHERE course_state = 'provisioned') AS provisioned_courses,
                  (SELECT count(*) FROM course WHERE course_state = 'declined') AS declined_courses,
                  (SELECT count(*) FROM course WHERE course_state = 'suspended') AS suspended_courses,
                  (SELECT count(DISTINCT participant_id) FROM course_membership
                     WHERE role = 'teacher' AND on_roster = true) AS teachers,
                  (SELECT count(DISTINCT participant_id) FROM course_membership
                     WHERE role = 'student' AND on_roster = true) AS students,
                  (SELECT count(*) FROM course_work) AS course_work_items,
                  (SELECT count(*) FROM submission) AS submissions,
                  (SELECT count(*) FROM meet_session) AS meet_meetings,
                  (SELECT count(DISTINCT s.meeting_code) FROM meet_session s
                     WHERE NOT EXISTS (SELECT 1 FROM meeting_code_link l WHERE l.meeting_code = s.meeting_code))
                    AS meet_codes_awaiting_decision
                """)
            .SingleAsync(cancellationToken);

        return new DatabaseCounts(
            checked((int)row.ActiveCourses),
            checked((int)row.ArchivedCourses),
            checked((int)row.ProvisionedCourses),
            checked((int)row.DeclinedCourses),
            checked((int)row.SuspendedCourses),
            checked((int)row.Teachers),
            checked((int)row.Students),
            checked((int)row.CourseWorkItems),
            checked((int)row.Submissions),
            checked((int)row.MeetMeetings),
            checked((int)row.MeetCodesAwaitingDecision));
    }

    /// <summary>The columns of Q1; settable properties because EF materializes by name.</summary>
    private sealed class CountsRecord
    {
        public long ActiveCourses { get; set; }

        public long ArchivedCourses { get; set; }

        public long ProvisionedCourses { get; set; }

        public long DeclinedCourses { get; set; }

        public long SuspendedCourses { get; set; }

        public long Teachers { get; set; }

        public long Students { get; set; }

        public long CourseWorkItems { get; set; }

        public long Submissions { get; set; }

        public long MeetMeetings { get; set; }

        public long MeetCodesAwaitingDecision { get; set; }
    }
}
