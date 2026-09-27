namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// A person's role on a Classroom course roster (US-014 entity model §5; <c>trebovaniya.md</c> §3). Exactly two
/// members, mapped to the codes <c>teacher</c> / <c>student</c> by a value converter, with
/// <c>ck_course_membership_role</c> behind it.
/// </summary>
/// <remarks>
/// This is <b>not</b> an application permission: §3 is explicit that the Classroom role does not coincide with
/// the role in the application (<c>AppUser</c>) and in the first version serves as a statistic, not an access
/// right. Any authorization code reading this enum is a finding.
/// </remarks>
public enum ClassroomRole
{
    Teacher,
    Student,
}
