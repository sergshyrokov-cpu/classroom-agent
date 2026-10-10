using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-017 FR-006, VR-002, I-5, I-6; db-design §2, §3, §4, §7 (AC-013): against real PostgreSQL (TC-2), a failed run
/// stores its diagnosis code and the number of courses committed, a later completed run clears the code, and the
/// "Last synchronization" query shows a value an earlier version wrote — or any text that is not exactly a declared
/// name — as <c>Unexpected</c>. The Story adds no migration, so the model has no pending change.
/// </summary>
public sealed class SyncDiagnosisPersistenceTests(PostgreSqlFixture database)
{
    public static TheoryData<SyncDiagnosis> AllDiagnoses => new(Enum.GetValues<SyncDiagnosis>());

    /// <summary>
    /// A host that is not in read-only mode, so the commit backstop lets these direct writes through (BR-025), and
    /// that has no saved connection, so its own scheduled run is skipped and never touches <c>sync_state</c>
    /// (spec I-8).
    /// </summary>
    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        SyncHostExtensions.StartAsync(database, ct, connection: SeededConnection.None);

    /// <summary>Begins a first run and commits it, as the use case does before it reads anything.</summary>
    private static async Task BeginFirstRunAsync(InstallationTestHost host, Guid runId, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISyncStateRepository>()
            .Add(SyncState.BeginFirstRun(runId, host.Time.GetUtcNow()));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }

    private static async Task<SyncHostExtensions.SyncStateRow> OnlyRowAsync(InstallationTestHost host, CancellationToken ct) =>
        Assert.Single(await host.SyncStatesAsync(ct));

    private static async Task<LastSynchronizationView> ViewAsync(InstallationTestHost host, CancellationToken ct)
    {
        using var scope = host.CreateScope();
        return await new GetLastSynchronizationQuery(
                scope.ServiceProvider.GetRequiredService<ISyncStateRepository>(),
                scope.ServiceProvider.GetRequiredService<SchoolTimeZone>())
            .ExecuteAsync(ct);
    }

    /// <summary>A failed row written the way an earlier version could have left it, replacing any row there.</summary>
    private static async Task WriteFailedRowRawAsync(InstallationTestHost host, string lastError, CancellationToken ct)
    {
        await host.ExecuteAsync("DELETE FROM sync_state", ct);
        var at = host.Time.GetUtcNow();
        await host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, created_at, updated_at)
            VALUES (true, 'failed', @runId, @at, @at, 0, @lastError, NULL, @at, @at)
            """,
            ct,
            ("runId", Guid.NewGuid()),
            ("at", at),
            ("lastError", lastError));
    }

    /// <summary>
    /// db-design §2, §3, I-5: a failed run stores the diagnosis name in <c>last_error</c> and the courses committed
    /// in <c>processed_count</c>; both round-trip through the real repository and the query.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllDiagnoses))]
    public async Task AFailedRun_StoresTheCodeAndTheCount_AndTheyRoundTrip(SyncDiagnosis diagnosis)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        var runId = Guid.NewGuid();
        await BeginFirstRunAsync(host, runId, ct);
        host.Time.Advance(TimeSpan.FromSeconds(30));

        using (var scope = host.CreateScope())
        {
            var state = await scope.ServiceProvider.GetRequiredService<ISyncStateRepository>().GetAsync(ct);
            state!.FailRun(host.Time.GetUtcNow(), 2, diagnosis, SyncStep.Classroom);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        var row = await OnlyRowAsync(host, ct);
        Assert.Equal(SyncTestData.Status.Failed, row.Status);
        Assert.Equal(diagnosis.ToString(), row.LastError);
        Assert.Equal(2, row.ProcessedCount);
        Assert.Null(row.LastSuccessfulRunAt);
        var view = await ViewAsync(host, ct);
        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(diagnosis, view.Diagnosis);
    }

    /// <summary>
    /// FR-006: a completed run after a failed one sets <c>last_error</c> back to NULL. The failed row is the
    /// precondition, so the NULL is the clearing and not an untouched column.
    /// </summary>
    [Fact]
    public async Task ACompletedRun_AfterAFailedOne_NullsTheError()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await BeginFirstRunAsync(host, Guid.NewGuid(), ct);
        using (var scope = host.CreateScope())
        {
            var state = await scope.ServiceProvider.GetRequiredService<ISyncStateRepository>().GetAsync(ct);
            state!.FailRun(host.Time.GetUtcNow(), 0, SyncDiagnosis.KeyRejected, SyncStep.Classroom);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        Assert.Equal("KeyRejected", (await OnlyRowAsync(host, ct)).LastError);
        host.Time.Advance(TimeSpan.FromHours(1));
        using (var scope = host.CreateScope())
        {
            var state = await scope.ServiceProvider.GetRequiredService<ISyncStateRepository>().GetAsync(ct);
            state!.BeginRun(Guid.NewGuid(), host.Time.GetUtcNow());
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            host.Time.Advance(TimeSpan.FromSeconds(10));
            state.CompleteRun(host.Time.GetUtcNow(), 4, host.Time.GetUtcNow());
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
        }

        var row = await OnlyRowAsync(host, ct);
        Assert.Equal(SyncTestData.Status.Completed, row.Status);
        Assert.Null(row.LastError);
        Assert.Equal(4, row.ProcessedCount);
        var view = await ViewAsync(host, ct);
        Assert.Equal(LastSynchronizationStatus.Completed, view.Status);
        Assert.Null(view.Diagnosis);
    }

    /// <summary>
    /// AC-013, db-design §4, VR-002: a legacy <c>RunFailed:…</c> value, numeric text and a differently cased name,
    /// written by raw SQL, are shown as <c>Unexpected</c> with the status <c>Failed</c>. Each case first proves, on
    /// the same database, that a correctly spelled name written the same way maps to itself (the control), so the
    /// legacy result is not what every raw value gets.
    /// </summary>
    [Theory]
    [InlineData("RunFailed:HttpRequestException")]
    [InlineData("3")]
    [InlineData("scopenotauthorized")]
    public async Task ALegacyOrMisspelledValue_IsReadAsUnexpected(string stored)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await WriteFailedRowRawAsync(host, "ScopeNotAuthorized", ct);
        var control = await ViewAsync(host, ct);
        Assert.Equal(LastSynchronizationStatus.Failed, control.Status);
        Assert.Equal(SyncDiagnosis.ScopeNotAuthorized, control.Diagnosis);

        await WriteFailedRowRawAsync(host, stored, ct);

        var view = await ViewAsync(host, ct);
        Assert.Equal(LastSynchronizationStatus.Failed, view.Status);
        Assert.Equal(SyncDiagnosis.Unexpected, view.Diagnosis);
    }

    /// <summary>
    /// db-design §2, §4: the empty string cannot be a legacy value — <c>ck_sync_state_error_length</c> (1..512)
    /// already refuses it, and a failed row may not hold NULL either (<c>ck_sync_state_terminal_fields</c>). The
    /// "empty" case of the mapping is therefore unreachable from the database, and the test pins that.
    /// </summary>
    [Fact]
    public async Task AnEmptyError_CannotBeStored_SoItIsNeverRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await WriteFailedRowRawAsync(host, "ScopeNotAuthorized", ct);

        var empty = async () => await WriteFailedRowRawAsync(host, string.Empty, ct);

        var error = await Assert.ThrowsAsync<PostgresException>(empty);
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(SyncTestData.Constraints.ErrorLength, error.ConstraintName);
    }

    /// <summary>
    /// db-design §1, §7: the Story changes values, not schema — the EF model has no pending change against the
    /// migrations, so no migration is needed (the <c>InstallationDatabaseTests</c> pattern).
    /// </summary>
    [Fact]
    public async Task Model_HasNoPendingChanges_SoNoMigrationIsNeeded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        using var scope = host.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
