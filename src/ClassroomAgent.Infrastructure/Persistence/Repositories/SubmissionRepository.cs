using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>submission</c> table (US-015 entity model §7). Stages changes; never saves (AD-7).
/// </summary>
/// <remarks>
/// TEST_WRITING compile-only skeleton (OD-012): every member throws <see cref="NotImplementedException"/>.
/// IMPLEMENTATION replaces every body below and must also remove the <c>CS9113</c> suppression, no longer needed
/// once <c>db</c> is read.
/// </remarks>
#pragma warning disable CS9113 // Parameter 'db' is unread — every body below throws until IMPLEMENTATION.
public sealed class SubmissionRepository(ClassroomAgentDbContext db) : ISubmissionRepository
#pragma warning restore CS9113
{
    public Task<IReadOnlyList<Submission>> GetByCourseWorkIdsAsync(
        IReadOnlyCollection<long> courseWorkIds,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void Add(Submission submission) => throw new NotImplementedException();
}
