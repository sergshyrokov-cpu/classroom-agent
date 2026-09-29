namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The fixed values of US-015: the migration and tables the Story adds, their constraints and indexes, the stored
/// vocabularies, the raw strings Classroom sends on the wire, and synthetic identifiers and titles. Kept in one
/// place so a rename shows up as one edit (the <see cref="CourseTestData"/> pattern of US-014).
/// </summary>
/// <remarks>
/// Every item, title and submission id here is invented. TC-4 forbids a real assignment title, a real grade
/// context or a real school domain in a fixture.
/// </remarks>
public static class CourseWorkTestData
{
    /// <summary>US-015 db-design §6: the migration's name suffix.</summary>
    public const string Migration = "_AddCourseWorkAndSubmissions";

    /// <summary>US-015 db-design §3: the coursework/material table.</summary>
    public const string CourseWorkTable = "course_work";

    /// <summary>US-015 db-design §4: the submission table.</summary>
    public const string SubmissionTable = "submission";

    /// <summary>US-015 db-design §3.2, §3.5, §4.2, §4.3: the constraints and indexes the database enforces.</summary>
    public static class Constraints
    {
        public const string CourseWorkPrimaryKey = "pk_course_work";

        public const string CourseWorkCourseForeignKey = "fk_course_work_course";

        public const string CourseWorkResource = "ck_course_work_resource";

        public const string CourseWorkMaterialHasNoGrading = "ck_course_work_material_has_no_grading";

        public const string CourseWorkMaxPointsNonNegative = "ck_course_work_max_points_non_negative";

        public const string CourseWorkCourseResourceGoogleId = "uq_course_work_course_resource_google_id";

        public const string CourseWorkCourseItemDate = "ix_course_work_course_item_date";

        public const string SubmissionPrimaryKey = "pk_submission";

        public const string SubmissionCourseWorkForeignKey = "fk_submission_course_work";

        public const string SubmissionParticipantForeignKey = "fk_submission_classroom_participant";

        public const string SubmissionState = "ck_submission_state";

        public const string SubmissionRawState = "ck_submission_raw_state";

        public const string SubmissionGradesNonNegative = "ck_submission_grades_non_negative";

        public const string SubmissionCourseWorkGoogleId = "uq_submission_course_work_google_id";

        public const string SubmissionCourseWorkParticipant = "ix_submission_course_work_participant";

        public const string SubmissionParticipantId = "ix_submission_participant_id";
    }

    /// <summary>The codes stored in <c>course_work.resource</c> (db-design §3.2), the two Classroom resources.</summary>
    public static class ResourceCodes
    {
        public const string CourseWork = "course_work";

        public const string CourseWorkMaterial = "course_work_material";
    }

    /// <summary>
    /// The codes stored in <c>submission.state</c> (db-design §4.2): the six recognised values of VR-004 plus the
    /// <c>unrecognised</c> marker OD-005 requires.
    /// </summary>
    public static class StateCodes
    {
        public const string New = "new";

        public const string Created = "created";

        public const string TurnedIn = "turned_in";

        public const string Returned = "returned";

        public const string ReclaimedByStudent = "reclaimed_by_student";

        public const string StudentEditedAfterTurnIn = "student_edited_after_turn_in";

        public const string Unrecognised = "unrecognised";

        /// <summary>Every code the vocabulary allows, for the enumeration assertions.</summary>
        public static readonly string[] All =
        [
            New, Created, TurnedIn, Returned, ReclaimedByStudent, StudentEditedAfterTurnIn, Unrecognised,
        ];
    }

    /// <summary>
    /// The raw strings Classroom sends on the wire for a submission's state (VR-004, OD-011) — the shape that
    /// crosses <see cref="ClassroomAgent.Application.Models.SubmissionSnapshot.State"/> before the use case
    /// classifies it into <see cref="ClassroomAgent.Domain.Enums.SubmissionState"/>.
    /// </summary>
    public static class RawStates
    {
        public const string New = "NEW";

        public const string Created = "CREATED";

        public const string TurnedIn = "TURNED_IN";

        public const string Returned = "RETURNED";

        public const string ReclaimedByStudent = "RECLAIMED_BY_STUDENT";

        public const string StudentEditedAfterTurnIn = "STUDENT_EDITED_AFTER_TURN_IN";

        /// <summary>Every raw string the six-value vocabulary recognises (VR-004).</summary>
        public static readonly string[] AllRecognised =
        [
            New, Created, TurnedIn, Returned, ReclaimedByStudent, StudentEditedAfterTurnIn,
        ];
    }

    /// <summary>
    /// The state string Google would have to invent for OD-005's marker to apply — not one of the six. Classroom's
    /// own enumeration documents <c>SUBMISSION_STATE_UNSPECIFIED</c> as the zero value it never actually returns,
    /// the same convention <see cref="CourseTestData.UnrecognisedState"/> uses for a course.
    /// </summary>
    public const string UnrecognisedState = "SUBMISSION_STATE_UNSPECIFIED";

    /// <summary>Synthetic Google ids for a coursework or material item.</summary>
    public static string ItemId(int ordinal) => $"8{ordinal:D11}";

    /// <summary>A synthetic coursework or material title — invented, as TC-4 requires.</summary>
    public static string Title(int ordinal) => $"Test Item {ordinal}";

    /// <summary>Synthetic Google ids for a submission.</summary>
    public static string SubmissionId(int ordinal) => $"9{ordinal:D11}";
}
