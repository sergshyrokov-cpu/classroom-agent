using ClassroomAgent.Application.MeetLinking;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.MeetLinking;

/// <summary>
/// US-032 spec FR-003, FR-004, I-3 … I-5, AC-001 … AC-003, AC-006, AC-013 (BR-065): who counts toward a course's share,
/// which courses are candidates, and when a code is unambiguous — exact fractions, never rounded percents.
/// </summary>
public sealed class MeetCodeScorerTests
{
    private const long A = 101;

    private const long B = 102;

    private const long C = 103;

    private const string Code = "abc-0001-xyz";

    private static readonly TimeZoneInfo Kyiv = JournalTestData.Kyiv;

    private static readonly MeetLinkingThresholds Defaults = MeetLinkingThresholds.Default;

    /// <summary>Noon in Kyiv on <paramref name="day"/> of September 2026, as UTC.</summary>
    private static DateTimeOffset Noon(int day) => new(2026, 9, day, 9, 0, 0, TimeSpan.Zero);

    private static string Teacher(int n) => MeetTestData.Teacher(n);

    private static string Student(int n) => MeetTestData.Student(n);

    private static IEnumerable<string> Students(int from, int count) => Enumerable.Range(from, count).Select(Student);

    private static MeetCodeMeeting Meeting(long id, string organizer, int day, IEnumerable<string> participants) =>
        new(id, organizer, Noon(day), participants.ToList());

    /// <summary>A membership on the roster since 1 September.</summary>
    private static RosterMembership OnRoster(long course, ClassroomRole role, string email) =>
        new(course, role, email, Noon(1), Noon(1), true);

    private static IEnumerable<RosterMembership> StudentsOf(long course, IEnumerable<string> emails) =>
        emails.Select(e => OnRoster(course, ClassroomRole.Student, e));

    private static MeetCodeScore Score(IEnumerable<MeetCodeMeeting> meetings, IEnumerable<RosterMembership> memberships) =>
        MeetCodeScorer.Score(new MeetCodeScoringInput(Code, meetings.ToList(), memberships.ToList()), Kyiv);

    private static CandidateShare ShareOf(MeetCodeScore score, long course) =>
        Assert.Single(score.Candidates, c => c.CourseId == course);

    /// <summary>
    /// A code with one meeting organized by teacher 1 (teacher of A and B) on 15 September, attended by
    /// <paramref name="participants"/> students 1 … n; the first <paramref name="ofA"/> are students of A and students
    /// <c>n − ofB + 1 … n</c> students of B.
    /// </summary>
    private static MeetCodeScore TwoCourses(int participants, int ofA, int ofB) =>
        Score(
            [Meeting(1, Teacher(1), 15, Students(1, participants))],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(B, ClassroomRole.Teacher, Teacher(1)),
                .. StudentsOf(A, Students(1, ofA)),
                .. StudentsOf(B, Students(participants - ofB + 1, ofB)),
            ]);

    // ---------------- Who counts (FR-003) ----------------

    /// <summary>AC-001's numbers: 17 of 20 are students of A (85 %), 4 of 20 of B (20 %).</summary>
    [Fact]
    public void Shares_AreStudentsOfTheCourse_OverDistinctCountedAccounts()
    {
        var score = TwoCourses(participants: 20, ofA: 17, ofB: 4);

        Assert.Equal((17, 20), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
        Assert.Equal((4, 20), (ShareOf(score, B).Numerator, ShareOf(score, B).Denominator));
    }

    [Fact]
    public void Candidates_AreOrderedBestShareFirst()
    {
        var score = TwoCourses(participants: 20, ofA: 4, ofB: 17);

        Assert.Equal([B, A], score.Candidates.Select(c => c.CourseId));
    }

    /// <summary>Equal shares: the lower course id first, so the order is deterministic.</summary>
    [Fact]
    public void EqualShares_AreOrderedByCourseId()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, Students(1, 2))],
            [
                OnRoster(B, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                .. StudentsOf(B, Students(1, 2)),
                .. StudentsOf(A, Students(1, 2)),
            ]);

        Assert.Equal([A, B], score.Candidates.Select(c => c.CourseId));
    }

    /// <summary>AC-002: the organizer is not counted, even when they are also a student of the course.</summary>
    [Fact]
    public void TheOrganizer_IsNotCounted()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Teacher(1), Student(1), Student(2)])],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(A, ClassroomRole.Student, Teacher(1)),
                .. StudentsOf(A, [Student(1)]),
            ]);

        Assert.Equal((1, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>
    /// I-3: an account that organized any meeting of the code is excluded everywhere — here teacher 2 organized the
    /// second meeting and only attended the first.
    /// </summary>
    [Fact]
    public void AnAccountThatOrganizedAnyMeetingOfTheCode_IsNotCountedInAnyMeeting()
    {
        var score = Score(
            [
                Meeting(1, Teacher(1), 15, [Teacher(2), Student(1)]),
                Meeting(2, Teacher(2), 16, [Student(1)]),
            ],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                .. StudentsOf(A, [Student(1), Teacher(2)]),
            ]);

        Assert.Equal((1, 1), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>An account joining several meetings of the code counts once.</summary>
    [Fact]
    public void AnAccountInSeveralMeetings_CountsOnce()
    {
        var score = Score(
            [
                Meeting(1, Teacher(1), 15, [Student(1), Student(2)]),
                Meeting(2, Teacher(1), 16, [Student(1)]),
            ],
            [OnRoster(A, ClassroomRole.Teacher, Teacher(1)), .. StudentsOf(A, [Student(1)])]);

        Assert.Equal((1, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>PC-12: emails are matched case-insensitively — Google may spell one address differently in Meet and Classroom.</summary>
    [Fact]
    public void Emails_AreMatchedIgnoringCase()
    {
        var score = Score(
            [Meeting(1, Teacher(1).ToUpperInvariant(), 15, [Student(1).ToUpperInvariant(), Student(2)])],
            [OnRoster(A, ClassroomRole.Teacher, Teacher(1)), .. StudentsOf(A, [Student(1), Student(2).ToUpperInvariant()])]);

        Assert.Equal((2, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>AC-002: a student who joined the course after the meeting's date does not count.</summary>
    [Fact]
    public void AStudentFirstSeenAfterTheMeetingDate_DoesNotCount()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Student(1), Student(2)])],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(A, ClassroomRole.Student, Student(1)),
                new RosterMembership(A, ClassroomRole.Student, Student(2), Noon(16), Noon(16), true),
            ]);

        Assert.Equal((1, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>AC-002: a student who left the course before the meeting's date does not count.</summary>
    [Fact]
    public void AStudentWhoLeftBeforeTheMeetingDate_DoesNotCount()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Student(1), Student(2)])],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(A, ClassroomRole.Student, Student(1)),
                new RosterMembership(A, ClassroomRole.Student, Student(2), Noon(1), Noon(14), false),
            ]);

        Assert.Equal((1, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>
    /// AC-002, BR-065: a student counts if they were on the roster on the date of at least one of <em>their</em>
    /// meetings of the code — here only on the 20th, which they attended.
    /// </summary>
    [Fact]
    public void AStudentOnTheRosterForOneOfTheirMeetings_Counts()
    {
        var score = Score(
            [
                Meeting(1, Teacher(1), 15, [Student(1)]),
                Meeting(2, Teacher(1), 20, [Student(1)]),
            ],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                new RosterMembership(A, ClassroomRole.Student, Student(1), Noon(18), Noon(18), true),
            ]);

        Assert.Equal((1, 1), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>
    /// The student was on the roster on the 20th, but only attended the meeting of the 15th: the roster date must be
    /// the date of one of their own meetings.
    /// </summary>
    [Fact]
    public void AStudentOnTheRosterOnlyOnTheDateOfAMeetingTheyMissed_DoesNotCount()
    {
        var score = Score(
            [
                Meeting(1, Teacher(1), 15, [Student(1), Student(2)]),
                Meeting(2, Teacher(1), 20, [Student(2)]),
            ],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(A, ClassroomRole.Student, Student(2)),
                new RosterMembership(A, ClassroomRole.Student, Student(1), Noon(18), Noon(18), true),
            ]);

        Assert.Equal((1, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>A participant who is a teacher of the course (not a student) is counted in the denominator only.</summary>
    [Fact]
    public void ATeacherOfTheCourseAmongParticipants_IsNotAStudent()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Teacher(2), Student(1)])],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(A, ClassroomRole.Teacher, Teacher(2)),
                .. StudentsOf(A, [Student(1)]),
            ]);

        Assert.Equal((1, 2), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    /// <summary>With no counted account (only the organizer joined), every share is 0 of 0.</summary>
    [Fact]
    public void NoCountedAccount_GivesZeroShares()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Teacher(1)])],
            [OnRoster(A, ClassroomRole.Teacher, Teacher(1))]);

        var share = ShareOf(score, A);
        Assert.Equal((0, 0), (share.Numerator, share.Denominator));
        Assert.Equal(0, share.PercentRoundedDown);
    }

    // ---------------- Candidates (FR-003 item 2) ----------------

    /// <summary>AC-002: a course whose teacher did not organize any meeting of the code is not a candidate.</summary>
    [Fact]
    public void ACourseNoneOfWhoseTeachersOrganized_IsNotACandidate()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Student(1)])],
            [
                OnRoster(A, ClassroomRole.Teacher, Teacher(1)),
                OnRoster(C, ClassroomRole.Teacher, Teacher(2)),
                .. StudentsOf(A, [Student(1)]),
                .. StudentsOf(C, [Student(1)]),
            ]);

        Assert.Equal([A], score.Candidates.Select(c => c.CourseId));
    }

    /// <summary>AC-002: the organizer must be a teacher of the course on the date of a meeting they organized.</summary>
    [Fact]
    public void AnOrganizerWhoBecameTeacherAfterTheirMeeting_DoesNotMakeACandidate()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Student(1)])],
            [
                new RosterMembership(A, ClassroomRole.Teacher, Teacher(1), Noon(16), Noon(16), true),
                .. StudentsOf(A, [Student(1)]),
            ]);

        Assert.Empty(score.Candidates);
    }

    /// <summary>A student-role membership of the organizer does not make the course a candidate.</summary>
    [Fact]
    public void AnOrganizerWhoIsOnlyAStudentOfTheCourse_DoesNotMakeACandidate()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, [Student(1)])],
            [OnRoster(A, ClassroomRole.Student, Teacher(1)), .. StudentsOf(A, [Student(1)])]);

        Assert.Empty(score.Candidates);
    }

    /// <summary>
    /// BR-065 (v41): one organizer-teacher on one meeting is enough; a substitute organizing another meeting does not
    /// remove the candidate.
    /// </summary>
    [Fact]
    public void OneOrganizerTeacherOnOneMeeting_IsEnough()
    {
        var score = Score(
            [
                Meeting(1, Teacher(1), 15, [Student(1)]),
                Meeting(2, Teacher(9), 16, [Student(1)]),
            ],
            [OnRoster(A, ClassroomRole.Teacher, Teacher(1)), .. StudentsOf(A, [Student(1)])]);

        Assert.Equal([A], score.Candidates.Select(c => c.CourseId));
    }

    /// <summary>AC-013: a code with no candidate has an empty candidate list and is never decided.</summary>
    [Fact]
    public void NoCandidate_IsAnEmptyList_AndNeverUnambiguous()
    {
        var score = Score([Meeting(1, Teacher(1), 15, Students(1, 5))], StudentsOf(A, Students(1, 5)));

        Assert.Empty(score.Candidates);
        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(score, Defaults));
    }

    /// <summary>
    /// TC-8: the organizer became a teacher at 00:30 Kyiv time on the 15th (21:30 UTC on the 14th); the meeting at
    /// 00:45 Kyiv time on the 15th is on the 15th in the school's zone, so the course is a candidate.
    /// </summary>
    [Fact]
    public void MeetingAndRosterDates_AreTakenInTheSchoolsZone()
    {
        var firstSeen = new DateTimeOffset(2026, 9, 14, 21, 30, 0, TimeSpan.Zero);
        var meeting = new MeetCodeMeeting(1, Teacher(1), new DateTimeOffset(2026, 9, 14, 21, 45, 0, TimeSpan.Zero), [Student(1)]);
        var score = MeetCodeScorer.Score(
            new MeetCodeScoringInput(
                Code,
                [meeting],
                [
                    new RosterMembership(A, ClassroomRole.Teacher, Teacher(1), firstSeen, firstSeen, true),
                    new RosterMembership(A, ClassroomRole.Student, Student(1), firstSeen, firstSeen, true),
                ]),
            Kyiv);

        Assert.Equal((1, 1), (ShareOf(score, A).Numerator, ShareOf(score, A).Denominator));
    }

    // ---------------- The decision (FR-004) ----------------

    /// <summary>AC-001: 85 % and 20 % — linked to A.</summary>
    [Fact]
    public void EightyFiveAgainstTwenty_LinksTheBest() =>
        Assert.Equal(new MeetCodeDecision(true, A), MeetCodeScorer.Decide(TwoCourses(20, 17, 4), Defaults));

    /// <summary>AC-003: 70 % and 55 % — the gap is too small.</summary>
    [Fact]
    public void SeventyAgainstFiftyFive_IsAmbiguous() =>
        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(TwoCourses(20, 14, 11), Defaults));

    /// <summary>AC-003: a best share below 60 % never links, however large the gap.</summary>
    [Fact]
    public void BestBelowSixty_IsAmbiguous() =>
        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(TwoCourses(20, 11, 0), Defaults));

    /// <summary>Boundaries are inclusive: exactly 60 % with a gap of exactly 30 points links.</summary>
    [Fact]
    public void ExactlySixtyWithAGapOfExactlyThirty_Links() =>
        Assert.Equal(new MeetCodeDecision(true, A), MeetCodeScorer.Decide(TwoCourses(10, 6, 3), Defaults));

    [Fact]
    public void SixtyWithAGapOfTwenty_IsAmbiguous() =>
        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(TwoCourses(10, 6, 4), Defaults));

    /// <summary>I-4: 179 of 300 is 59.67 % — a rounded 60 % would link it; the exact share does not.</summary>
    [Fact]
    public void JustBelowSixty_IsAmbiguous_ThoughItRoundsToSixty() =>
        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(TwoCourses(300, 179, 0), Defaults));

    /// <summary>I-4: 90 % against 60.33 % is a gap of 29.67 points — whole-percent arithmetic (90 − 60) would say 30.</summary>
    [Fact]
    public void AGapJustBelowThirty_IsAmbiguous_ThoughRoundedPercentsSayThirty() =>
        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(TwoCourses(300, 270, 181), Defaults));

    /// <summary>I-5: a single candidate is compared with a next-best of 0.</summary>
    [Fact]
    public void ASingleCandidateAtOneHundred_Links()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, Students(1, 5))],
            [OnRoster(A, ClassroomRole.Teacher, Teacher(1)), .. StudentsOf(A, Students(1, 5))]);

        Assert.Equal(new MeetCodeDecision(true, A), MeetCodeScorer.Decide(score, Defaults));
    }

    /// <summary>I-5: a single candidate whose share is at least S but below G does not link.</summary>
    [Fact]
    public void ASingleCandidateBelowTheGap_IsAmbiguous()
    {
        var score = Score(
            [Meeting(1, Teacher(1), 15, Students(1, 10))],
            [OnRoster(A, ClassroomRole.Teacher, Teacher(1)), .. StudentsOf(A, Students(1, 3))]);

        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(score, new MeetLinkingThresholds(20, 40)));
    }

    /// <summary>FR-004: two equal best shares are never unambiguous (Story Notes: Mathematics and Physics of one class).</summary>
    [Fact]
    public void TwoEqualBestShares_AreAmbiguous_EvenWithTheSmallestGap() =>
        Assert.Equal(
            new MeetCodeDecision(false, null),
            MeetCodeScorer.Decide(TwoCourses(10, 10, 10), new MeetLinkingThresholds(1, 1)));

    /// <summary>AC-006: the thresholds passed in are the ones used — 55 % / 10 points links what the defaults would not.</summary>
    [Fact]
    public void ConfiguredThresholds_AreTheOnesUsed()
    {
        var score = TwoCourses(20, 14, 11);

        Assert.Equal(new MeetCodeDecision(false, null), MeetCodeScorer.Decide(score, Defaults));
        Assert.Equal(new MeetCodeDecision(true, A), MeetCodeScorer.Decide(score, new MeetLinkingThresholds(55, 10)));
    }

    // ---------------- Display (FR-003, I-4) ----------------

    [Theory]
    [InlineData(2, 3, 66)]
    [InlineData(179, 300, 59)]
    [InlineData(1, 1, 100)]
    [InlineData(0, 5, 0)]
    [InlineData(0, 0, 0)]
    public void PercentRoundedDown_NeverRoundsUp(int numerator, int denominator, int expected) =>
        Assert.Equal(expected, new CandidateShare(A, numerator, denominator).PercentRoundedDown);
}
