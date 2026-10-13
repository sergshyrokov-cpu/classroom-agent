using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Ports;

/// <summary>The counters of the Database page (US-024 spec FR-002 … FR-004, db-design §3.1); reads only.</summary>
public interface IDatabaseStatisticsSource
{
    Task<DatabaseCounts> GetCountsAsync(CancellationToken cancellationToken);
}
