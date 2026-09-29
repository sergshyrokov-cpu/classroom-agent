using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>course_work</c> table (US-015 entity model §7). Stages changes; never saves (AD-7).
/// </summary>
/// <remarks>
/// TEST_WRITING compile-only skeleton (OD-012): every member throws <see cref="NotImplementedException"/>.
/// IMPLEMENTATION replaces every body below and must also remove the <c>CS9113</c> suppression, no longer needed
/// once <c>db</c> is read.
/// </remarks>
#pragma warning disable CS9113 // Parameter 'db' is unread — every body below throws until IMPLEMENTATION.
public sealed class CourseWorkRepository(ClassroomAgentDbContext db) : ICourseWorkRepository
#pragma warning restore CS9113
{
    public Task<IReadOnlyList<CourseWork>> GetByCourseAsync(long courseId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void Add(CourseWork courseWork) => throw new NotImplementedException();
}
