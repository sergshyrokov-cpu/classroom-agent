using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-024: the counters port, substituted (TC-1). It answers <see cref="Counts"/> and counts its calls, so a test
/// proves the query reads the counters once per page (spec FR-011, AC-017).
/// </summary>
public sealed class FakeDatabaseStatisticsSource : IDatabaseStatisticsSource
{
    public DatabaseCounts Counts { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public int Calls { get; private set; }

    public Task<DatabaseCounts> GetCountsAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Counts);
    }
}
