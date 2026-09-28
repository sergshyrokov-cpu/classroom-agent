namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A course's optional fields as one parameter (US-014 entity model §1.2), so
/// <see cref="Course.Import"/> and
/// <see cref="Course.UpdateFrom"/> do not grow a twelve-argument signature. It
/// holds no Google SDK type (AD-4). It lives in <c>Domain</c> because <see cref="Course"/> takes it and
/// <c>Domain</c> references nothing (entity-model §0, AD-3).
/// </summary>
/// <param name="Name">The course's name; required.</param>
/// <param name="Section">§3's "секция".</param>
/// <param name="DescriptionHeading">Classroom's separate heading field.</param>
/// <param name="Description">§3's description.</param>
/// <param name="Room">§3's room.</param>
/// <param name="OwnerGoogleId">The owner's Google id, a value and not a reference (OD-004).</param>
/// <param name="CreationTime">Google's creation time, stored as given (I-1).</param>
/// <param name="UpdateTime">Google's update time.</param>
/// <param name="AlternateLink">§3's course link.</param>
/// <param name="TeacherFolderId">§3's teacher folder id.</param>
/// <param name="TeacherFolderTitle">§3's teacher folder title.</param>
/// <param name="CalendarId">§3's calendar id.</param>
public sealed record CourseDetails(
    string Name,
    string? Section,
    string? DescriptionHeading,
    string? Description,
    string? Room,
    string? OwnerGoogleId,
    DateTimeOffset? CreationTime,
    DateTimeOffset? UpdateTime,
    string? AlternateLink,
    string? TeacherFolderId,
    string? TeacherFolderTitle,
    string? CalendarId);
