using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's <c>submission</c> table (US-015 entity model §7). Stages changes; never saves — the use
/// case owns the transaction boundary through <see cref="IUnitOfWork"/> (AD-7).
/// </summary>
public interface ISubmissionRepository
{
    /// <summary>One query per course rather than one per item.</summary>
    Task<IReadOnlyList<Submission>> GetByCourseWorkIdsAsync(
        IReadOnlyCollection<long> courseWorkIds,
        CancellationToken cancellationToken);

    void Add(Submission submission);
}
