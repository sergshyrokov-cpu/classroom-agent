namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The fixed values of US-014: the three tables the Story adds, their constraints and indexes, the stored
/// vocabularies, and synthetic Classroom data. Kept in one place so a rename shows up as one edit (the US-011,
/// US-012 and US-013 pattern).
/// </summary>
/// <remarks>
/// Every person and course here is invented. TC-4 forbids a real roster, a real name, a real address or a real
/// school domain in a fixture, and this Story is the first whose fixtures describe people at all.
/// </remarks>
public static class CourseTestData
{
    /// <summary>US-014 db-design §6: the migration's name suffix.</summary>
    public const string Migration = "_AddCoursesAndRosters";

    /// <summary>US-014 db-design §3: the course table.</summary>
    public const string CourseTable = "course";

    /// <summary>US-014 db-design §4: the participant table.</summary>
    public const string ParticipantTable = "classroom_participant";

    /// <summary>US-014 db-design §5: the membership table.</summary>
    public const string MembershipTable = "course_membership";

    /// <summary>US-014 db-design §3.2, §5.2: the constraints and indexes the database enforces.</summary>
    public static class Constraints
    {
        public const string CoursePrimaryKey = "pk_course";

        public const string CourseGoogleId = "uq_course_google_id";

        public const string CourseState = "ck_course_course_state";

        public const string ParticipantPrimaryKey = "pk_classroom_participant";

        public const string ParticipantGoogleUserId = "uq_classroom_participant_google_user_id";

        public const string ParticipantEmail = "ix_classroom_participant_email";

        public const string MembershipPrimaryKey = "pk_course_membership";

        public const string MembershipCourseParticipant = "uq_course_membership_course_participant";

        public const string MembershipCourseSeen = "ix_course_membership_course_seen";

        public const string MembershipParticipant = "ix_course_membership_participant_id";

        /// <summary>US-037 db-design §3: the partial index the leaver purge reads.</summary>
        public const string MembershipOffRosterLastSeen = "ix_course_membership_off_roster_last_seen";

        public const string MembershipRole = "ck_course_membership_role";

        public const string MembershipSeenOrder = "ck_course_membership_seen_order";

        public const string MembershipCourseForeignKey = "fk_course_membership_course";

        public const string MembershipParticipantForeignKey = "fk_course_membership_classroom_participant";
    }

    /// <summary>The codes stored in <c>course.course_state</c> (db-design §3.2), the five values of §3.</summary>
    public static class States
    {
        public const string Active = "active";

        public const string Archived = "archived";

        public const string Provisioned = "provisioned";

        public const string Declined = "declined";

        public const string Suspended = "suspended";

        /// <summary>Every code the vocabulary allows, for the enumeration assertions.</summary>
        public static readonly string[] All = [Active, Archived, Provisioned, Declined, Suspended];
    }

    /// <summary>The codes stored in <c>course_membership.role</c> (db-design §5.2), the two values of §3.</summary>
    public static class Roles
    {
        public const string Teacher = "teacher";

        public const string Student = "student";
    }

    /// <summary>
    /// The state string Google would have to invent for OD-010 to apply — not one of the five. Classroom's own
    /// enumeration has <c>COURSE_STATE_UNSPECIFIED</c>, which Google documents as never returned on a course, so
    /// the rule guards a future change rather than today's behaviour.
    /// </summary>
    public const string UnrecognisedState = "COURSE_STATE_UNSPECIFIED";

    /// <summary>Synthetic Google course ids.</summary>
    public static string CourseId(int ordinal) => $"7{ordinal:D11}";

    /// <summary>Synthetic Google user ids.</summary>
    public static string UserId(int ordinal) => $"1{ordinal:D17}";

    /// <summary>A synthetic address in the school's test domain.</summary>
    public static string Email(string local) => $"{local}@{InstallationTestData.Domain}";

    /// <summary>A synthetic person's name — invented, as TC-4 requires.</summary>
    public static string Name(int ordinal) => $"Test Person {ordinal}";

    /// <summary>A synthetic course name.</summary>
    public static string CourseName(int ordinal) => $"Test Course {ordinal}";
}
