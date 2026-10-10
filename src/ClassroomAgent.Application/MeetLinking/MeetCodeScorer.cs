using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.MeetLinking;

/// <summary>Scores a code against its candidate courses and decides it (US-032 spec FR-003, FR-004; BR-065).</summary>
/// <remarks>
/// Emails are matched case-insensitively (PC-12). Every account that organized any meeting of the code is left out of
/// the count (I-3). Shares stay exact fractions; the decision compares them by cross-multiplication, never through a
/// rounded percent (I-4).
/// </remarks>
public static class MeetCodeScorer
{
    private static readonly StringComparer Emails = StringComparer.OrdinalIgnoreCase;

    public static MeetCodeScore Score(MeetCodeScoringInput input, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(zone);

        var byEmail = input.Memberships.ToLookup(m => m.Email, Emails);

        // FR-003 item 2: a course is a candidate when an organizer was its teacher on the date of a meeting they organized.
        var candidates = new SortedSet<long>();
        foreach (var meeting in input.Meetings)
        {
            var date = RosterOnDate.LocalDate(meeting.StartedAt, zone);
            foreach (var membership in byEmail[meeting.OrganizerEmail])
            {
                if (membership.Role == ClassroomRole.Teacher && RosterOnDate.Covers(membership, date, zone))
                {
                    candidates.Add(membership.CourseId);
                }
            }
        }

        // FR-003 item 3: the distinct domain accounts of all the code's meetings, organizers excluded, each with the
        // dates of the meetings they joined.
        var organizers = new HashSet<string>(input.Meetings.Select(m => m.OrganizerEmail), Emails);
        var datesByAccount = new Dictionary<string, HashSet<DateOnly>>(Emails);
        foreach (var meeting in input.Meetings)
        {
            var date = RosterOnDate.LocalDate(meeting.StartedAt, zone);
            foreach (var email in meeting.ParticipantEmails)
            {
                if (organizers.Contains(email))
                {
                    continue;
                }

                if (!datesByAccount.TryGetValue(email, out var dates))
                {
                    datesByAccount[email] = dates = [];
                }

                dates.Add(date);
            }
        }

        // FR-003 item 4: an account counts for C if it was C's student on the date of at least one of its own meetings.
        var denominator = datesByAccount.Count;
        var shares = new List<CandidateShare>();
        foreach (var course in candidates)
        {
            var numerator = datesByAccount.Count(account => byEmail[account.Key].Any(m =>
                m.CourseId == course
                && m.Role == ClassroomRole.Student
                && account.Value.Any(date => RosterOnDate.Covers(m, date, zone))));
            shares.Add(new CandidateShare(course, numerator, denominator));
        }

        // Best first; equal shares by course id. The denominator is common, so numerators order the shares.
        var ordered = shares.OrderByDescending(s => s.Numerator).ThenBy(s => s.CourseId).ToList();
        return new MeetCodeScore(input.MeetingCode, ordered);
    }

    /// <summary>
    /// FR-004: unambiguous when the best share is at least S % and leads the next best — 0 for a single candidate (I-5)
    /// — by at least G points. Equal best shares never link, since G ≥ 1.
    /// </summary>
    public static MeetCodeDecision Decide(MeetCodeScore score, MeetLinkingThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(thresholds);

        if (score.Candidates.Count == 0)
        {
            return new MeetCodeDecision(false, null);
        }

        var best = score.Candidates[0];
        var next = score.Candidates.Count > 1 ? score.Candidates[1].Numerator : 0;
        var denominator = (long)best.Denominator;
        if (denominator == 0)
        {
            return new MeetCodeDecision(false, null);
        }

        // best / d ≥ S / 100  ⇔  100·best ≥ S·d;  (best − next) / d ≥ G / 100  ⇔  100·(best − next) ≥ G·d.
        var shareHolds = 100L * best.Numerator >= thresholds.MinSharePercent * denominator;
        var gapHolds = 100L * (best.Numerator - next) >= thresholds.MinGapPoints * denominator;
        return shareHolds && gapHolds ? new MeetCodeDecision(true, best.CourseId) : new MeetCodeDecision(false, null);
    }
}
