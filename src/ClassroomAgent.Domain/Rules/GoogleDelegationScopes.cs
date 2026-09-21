namespace ClassroomAgent.Domain.Rules;

/// <summary>
/// The OAuth scopes a school authorises for domain-wide delegation (US-010 spec FR-004;
/// <c>trebovaniya.md</c> §6, fixed in v25). A **requirement, not a configuration**: no installation may change
/// the list and no school may authorise a different set, so it lives in code with a test tying it to the
/// requirement text.
/// </summary>
/// <remarks>
/// In <c>Domain</c> because both sides need it and only <c>Domain</c> is referenced by both: US-010 renders the
/// list through <c>Application</c>, and US-011 will call Google with the same list from <c>Infrastructure</c>
/// (spec I-2). It holds plain strings and no Google SDK type, so AD-4 is untouched.
///
/// Every scope is read-only — the program never writes to Google Workspace (NFR-021, BR-030, SC-8). Two scopes
/// the prototype requested are deliberately absent: <c>drive.file</c>, which also grants writes, and
/// <c>classroom.profile.photos</c>, which no Epic uses and which is personal data of minors (§6, v25). The
/// identity scopes of the Admin's own sign-in (<c>openid</c>, <c>email</c>, <c>profile</c>) are absent too:
/// delegation and sign-in are different mechanisms (§6, v78).
/// </remarks>
public static class GoogleDelegationScopes
{
    /// <summary>List and cards of courses.</summary>
    public const string CoursesReadonly = "https://www.googleapis.com/auth/classroom.courses.readonly";

    /// <summary>Course rosters — the participants.</summary>
    public const string RostersReadonly = "https://www.googleapis.com/auth/classroom.rosters.readonly";

    /// <summary>Participants' email addresses.</summary>
    public const string ProfileEmails = "https://www.googleapis.com/auth/classroom.profile.emails";

    /// <summary>Student coursework and submissions.</summary>
    public const string CourseWorkStudentsReadonly = "https://www.googleapis.com/auth/classroom.coursework.students.readonly";

    /// <summary>Course materials — added in v24; without it the prototype silently saw none.</summary>
    public const string CourseWorkMaterialsReadonly = "https://www.googleapis.com/auth/classroom.courseworkmaterials.readonly";

    /// <summary>Meet <c>call_ended</c> events of the Admin Reports audit log (Epic 4).</summary>
    public const string AdminReportsAuditReadonly = "https://www.googleapis.com/auth/admin.reports.audit.readonly";

    /// <summary>The six scopes of §6, in the order US-010 openapi fixes. Rendered as full URIs, which is the form the Google console accepts (spec I-4).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        CoursesReadonly,
        RostersReadonly,
        ProfileEmails,
        CourseWorkStudentsReadonly,
        CourseWorkMaterialsReadonly,
        AdminReportsAuditReadonly,
    ];
}
