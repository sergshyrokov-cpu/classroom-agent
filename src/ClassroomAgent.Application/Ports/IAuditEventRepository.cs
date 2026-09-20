using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// The installation's audit trail (US-008 entity model §3.4; SC-11). Add only: a row is never updated, and it is
/// deleted only by the retention purge, which is EPIC-10 (PC-9, PC-11).
/// </summary>
public interface IAuditEventRepository
{
    void Add(AuditEvent auditEvent);
}
