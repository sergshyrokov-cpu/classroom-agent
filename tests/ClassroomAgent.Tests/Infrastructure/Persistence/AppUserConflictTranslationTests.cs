using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-048 FR-005: the real unit of work over real PostgreSQL (TC-2) turns a concurrency-stamp mismatch on an
/// <c>app_user</c> row into <see cref="AppUserConflictException"/> — never a provider exception (AD-3, AD-4) — and,
/// before throwing, removes the failed account entry and the staged audit rows, so the next read in the same scope
/// returns the current row and a later save does not carry the discarded run's row.
/// </summary>
public sealed class AppUserConflictTranslationTests(PostgreSqlFixture database)
{
    private static readonly DateTimeOffset At = InstallationTestHost.DefaultStart;

    [Fact]
    public async Task AStaleAppUserSave_IsAnAppUserConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await host.InsertDeanAsync(ct);
        using var scope = host.CreateScope();
        var services = scope.ServiceProvider;
        var stale = await FindDeanAsync(services, ct);

        await TouchDeanRowAsync(host, ct);
        stale.RecordFailedSignIn(At, 5, TimeSpan.FromMinutes(15));

        await Assert.ThrowsAsync<AppUserConflictException>(
            () => services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct));
    }

    [Fact]
    public async Task AfterTheConflict_TheNextReadReturnsTheCurrentRow_AndNoStagedAuditRowSurvives()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartWritableAsync(ct);
        await host.InsertDeanAsync(ct);
        using var scope = host.CreateScope();
        var services = scope.ServiceProvider;
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var stale = await FindDeanAsync(services, ct);

        await host.ExecuteAsync(
            "UPDATE app_user SET access_failed_count = 3, concurrency_stamp = @stamp WHERE role = 'dean'",
            ct,
            ("stamp", Guid.NewGuid().ToString("N")));
        stale.RecordFailedSignIn(At, 5, TimeSpan.FromMinutes(15));
        services.GetRequiredService<IAuditEventRepository>().Add(
            AuditEvent.DeanSignInRefused(stale.Id, AuditRefusalCategory.WrongPassword, At, "r-discarded"));
        await Assert.ThrowsAsync<AppUserConflictException>(() => unitOfWork.SaveChangesAsync(ct));

        var current = await FindDeanAsync(services, ct);
        Assert.Equal(3, current.AccessFailedCount);

        current.RecordFailedSignIn(At, 5, TimeSpan.FromMinutes(15));
        services.GetRequiredService<IAuditEventRepository>().Add(
            AuditEvent.DeanSignInRefused(current.Id, AuditRefusalCategory.WrongPassword, At, "r-kept"));
        await unitOfWork.SaveChangesAsync(ct);

        var rows = (await host.AuditRowsAsync(ct)).Where(r => r.Action == "dean_sign_in").ToList();
        Assert.Equal("r-kept", Assert.Single(rows).RequestId);
        Assert.Equal(4, Assert.Single(await host.AppUsersAsync(ct), u => u.Role == "dean").AccessFailedCount);
    }

    private Task<InstallationTestHost> StartWritableAsync(CancellationToken ct) =>
        ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);

    private static async Task<AppUser> FindDeanAsync(IServiceProvider services, CancellationToken ct)
    {
        var dean = await services.GetRequiredService<IAppUserRepository>()
            .FindByNormalizedEmailAsync(DeanAccountTestData.DeanEmail, ct);
        Assert.NotNull(dean);
        return dean;
    }

    private static Task TouchDeanRowAsync(InstallationTestHost host, CancellationToken ct) =>
        host.ExecuteAsync(
            "UPDATE app_user SET concurrency_stamp = @stamp WHERE role = 'dean'",
            ct,
            ("stamp", Guid.NewGuid().ToString("N")));
}
