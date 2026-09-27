using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's <c>classroom_participant</c> table (US-014 entity model §7). Stages changes; never saves
/// (AD-7).
/// </summary>
/// <remarks>
/// Compile-only skeleton created at TEST_WRITING under US-014 OD-012; IMPLEMENTATION owns it from here. There is
/// no <c>DbSet&lt;ClassroomParticipant&gt;</c> and no EF Core configuration yet — mapping without a migration
/// would desync the model from the database, so both members throw until IMPLEMENTATION adds the mapping and the
/// migration together (PC-2).
/// </remarks>
#pragma warning disable CS9113 // Parameter is unread: db is not yet used by this compile-only skeleton.
public sealed class ClassroomParticipantRepository(ClassroomAgentDbContext db) : IClassroomParticipantRepository
#pragma warning restore CS9113
{
    public Task<IReadOnlyList<ClassroomParticipant>> GetByGoogleUserIdsAsync(
        IReadOnlyCollection<string> googleUserIds,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void Add(ClassroomParticipant participant) => throw new NotImplementedException();
}
