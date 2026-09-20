using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// The installation's audit trail (US-008 entity model §3.4). Add only: it exposes no update and no delete, so
/// there is no way to reach a mutator the entity does not have (PC-9, SC-11).
/// </summary>
public sealed class AuditEventRepository(ClassroomAgentDbContext db) : IAuditEventRepository
{
    public void Add(AuditEvent auditEvent) => db.AuditEvents.Add(auditEvent);
}
