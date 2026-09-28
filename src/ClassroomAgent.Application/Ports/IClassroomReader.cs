using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// Reads courses and rosters from Google Classroom (US-014 entity model §6), implemented in
/// <c>Infrastructure/Google</c> (AD-4, TC-4, <c>package-map.md</c>). It carries the <see cref="IGoogleDataPort"/>
/// marker, so US-007 FR-007 binds it: a use case holding it also takes <c>IReadOnlyModeGuard</c> and calls it
/// first (FR-015).
/// </summary>
public interface IClassroomReader : IGoogleDataPort
{
    /// <summary>
    /// The school's courses, the adapter following the continuation token so the caller never sees a page
    /// (VR-005). Streaming rather than a list keeps one course's transaction independent of the whole school
    /// being in memory (FR-012).
    /// </summary>
    /// <param name="impersonationUser">
    /// The school's technical account, which every Classroom call is made on behalf of (BR-015, BR-031, S-03).
    /// The use case passes the address it already read from the connection; the adapter holds none
    /// (entity model §0, §6).
    /// </param>
    /// <param name="cancellationToken">Cancelled at host shutdown, which is a normal stop (FR-014).</param>
    IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(string impersonationUser, CancellationToken cancellationToken);

    /// <summary>
    /// Both rosters of one course, each fully paged. A returned <see cref="CourseRoster"/> means the read
    /// succeeded; an exception means the roster is unknown (I-6, I-7).
    /// </summary>
    /// <param name="impersonationUser">The school's technical account, as above.</param>
    /// <param name="courseGoogleId">The Classroom course id whose rosters are read.</param>
    /// <param name="cancellationToken">Cancelled at host shutdown.</param>
    Task<CourseRoster> ReadRosterAsync(
        string impersonationUser,
        string courseGoogleId,
        CancellationToken cancellationToken);
}
