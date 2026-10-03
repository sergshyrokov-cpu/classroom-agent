using ClassroomAgent.Application.Ports;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-037 AC-007, spec FR-008, SC-11, PC-9, db-design §6: the retention purge is the only code that deletes an audit
/// row — or any row at all with a set-based delete — and no use case, endpoint or screen can change one.
/// </summary>
public sealed class AuditDeletionConfinementTests
{
    private const string PurgeStore = "RetentionPurgeStore.cs";

    /// <summary>The audit repository stays add-only.</summary>
    [Fact]
    public void TheAuditRepository_CanOnlyAdd()
    {
        Assert.Equal(new[] { "Add" }, typeof(IAuditEventRepository).GetMethods().Select(m => m.Name).ToArray());
    }

    /// <summary>Set-based deletes exist in the purge store only (db-design §6: the backstop does not see them).</summary>
    [Fact]
    public void SetBasedDeletes_ExistOnlyInThePurgeStore()
    {
        var offenders = ProductionSources()
            .Where(f => Path.GetFileName(f) != PurgeStore)
            .Where(f => File.ReadAllText(f).Contains("ExecuteDelete", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
        Assert.Contains(
            ProductionSources(),
            f => Path.GetFileName(f) == PurgeStore && File.ReadAllText(f).Contains("ExecuteDelete", StringComparison.Ordinal));
    }

    /// <summary>No code removes, updates or deletes an audit row through the change tracker or raw SQL.</summary>
    [Fact]
    public void NoOtherCode_TouchesAuditRows()
    {
        var offenders = ProductionSources()
            .Where(f => Path.GetFileName(f) != PurgeStore)
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("AuditEvents.Remove", StringComparison.Ordinal)
                       || text.Contains("AuditEvents.Update", StringComparison.Ordinal)
                       || text.Contains("ExecuteUpdate", StringComparison.Ordinal)
                       || text.Contains("DELETE FROM audit_event", StringComparison.OrdinalIgnoreCase)
                       || text.Contains("UPDATE audit_event", StringComparison.OrdinalIgnoreCase);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The installation's production code, Control Plane excluded (its audit rows are never purged — SC-11 v45 — and
    /// it has its own tests). Migrations are excluded: they define the schema, not behaviour.
    /// </summary>
    private static IEnumerable<string> ProductionSources()
    {
        var src = Path.Combine(StaticFiles.RepositoryRoot(), "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains("ClassroomAgent.ControlPlane", StringComparison.Ordinal));
    }
}
