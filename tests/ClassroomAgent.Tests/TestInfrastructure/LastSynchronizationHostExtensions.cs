using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the US-017 block over HTTP: an Admin signed in with a chosen UI language and a <c>sync_state</c> row seeded
/// directly, the way <c>SyncStateSchemaTests</c> and <c>SyncDiagnosisPersistenceTests</c> seed one.
/// </summary>
public static class LastSynchronizationHostExtensions
{
    /// <summary>
    /// A started installation with an Admin signed in whose stored language is <paramref name="language"/>. The first
    /// sign-in creates the account; the second carries the stored language into the session (the US-039
    /// <c>DateFormatTests</c> pattern).
    /// </summary>
    public static async Task<(InstallationTestHost Host, FormClient Admin)> StartAdminAsync(
        PostgreSqlFixture database,
        string language,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly)
    {
        var (host, first, _) = await WorkspaceConnectionHostExtensions.StartSignedInAsync(database, cancellationToken, cause);
        first.Dispose();
        await host.ExecuteAsync(
            "UPDATE app_user SET ui_language = @language",
            cancellationToken,
            ("language", language));
        var (admin, callback) = await host.SignInWithGoogleAsync(cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, callback.Status);
        return (host, admin);
    }

    /// <summary>A <c>failed</c> row carrying <paramref name="lastError"/>, as the schema allows it.</summary>
    public static Task SeedFailedAsync(
        this InstallationTestHost host,
        string lastError,
        CancellationToken cancellationToken,
        DateTimeOffset? lastSuccessfulRunAt = null) =>
        SeedAsync(
            host,
            "failed",
            LastSynchronizationTestData.Started,
            LastSynchronizationTestData.Finished,
            lastError,
            lastSuccessfulRunAt,
            cancellationToken);

    /// <summary>A <c>completed</c> row whose last success is its own finish.</summary>
    public static Task SeedCompletedAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        SeedAsync(
            host,
            "completed",
            LastSynchronizationTestData.Started,
            LastSynchronizationTestData.Finished,
            null,
            LastSynchronizationTestData.Finished,
            cancellationToken);

    /// <summary>A <c>running</c> row, with an earlier success behind it.</summary>
    public static Task SeedRunningAsync(this InstallationTestHost host, CancellationToken cancellationToken) =>
        SeedAsync(
            host,
            "running",
            LastSynchronizationTestData.Started,
            null,
            null,
            LastSynchronizationTestData.EarlierSuccess,
            cancellationToken);

    private static Task SeedAsync(
        InstallationTestHost host,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt,
        string? lastError,
        DateTimeOffset? lastSuccessfulRunAt,
        CancellationToken cancellationToken) =>
        host.ExecuteAsync(
            """
            INSERT INTO sync_state (singleton, status, run_id, started_at, finished_at, processed_count,
                                    last_error, last_successful_run_at, created_at, updated_at)
            VALUES (true, @status, @runId, @startedAt, @finishedAt, 0, @lastError, @lastSuccessfulRunAt, @stamp, @stamp)
            """,
            cancellationToken,
            ("status", status),
            ("runId", Guid.NewGuid()),
            ("startedAt", startedAt),
            ("finishedAt", finishedAt),
            ("lastError", lastError),
            ("lastSuccessfulRunAt", lastSuccessfulRunAt),
            ("stamp", host.Time.GetUtcNow()));
}
