using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClassroomAgent.Tests.Web.Persistence;

/// <summary>
/// US-019 db-design §2: the new audit action and its shape constraint, against real PostgreSQL (PC-2, TC-2), and
/// the two domain factories of entity model §2.2.
/// </summary>
public sealed class SynchronizationRequestAuditSchemaTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (@stamp, 'app_user', (SELECT id FROM app_user LIMIT 1), @role, @action, @targetType, @targetId,
                @outcome, @category, 'r-1', @stamp, @stamp)
        """;

    /// <summary>db-design §2.1–2.3: both factory rows are stored, with no target and the role given.</summary>
    [Fact]
    public async Task TheTwoFactoryRows_AreStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        var actor = (await host.AppUsersAsync(ct)).Single().Id;
        var now = host.Time.GetUtcNow();

        using (var scope = host.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
            context.Add(AuditEvent.SynchronizationRequested(actor, AppRole.Dean, now, "r-1"));
            context.Add(AuditEvent.SynchronizationRequestRefused(
                actor, AppRole.Dean, AuditRefusalCategory.ReadOnlyMode, now, "r-2"));
            await context.SaveChangesAsync(ct);
        }

        var rows = await host.AuditRowsAsync(ct);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(SynchronizationRequestTestData.Audit.Action, r.Action);
            Assert.Null(r.TargetType);
            Assert.Null(r.TargetId);
            Assert.Equal(SynchronizationRequestTestData.Audit.RoleDean, r.ActorRole);
        });
        Assert.Equal(SynchronizationRequestTestData.Audit.Succeeded, rows[0].Outcome);
        Assert.Equal(SynchronizationRequestTestData.Audit.Refused, rows[1].Outcome);
        Assert.Equal(SynchronizationRequestTestData.Audit.ReadOnlyMode, rows[1].RefusalCategory);
    }

    /// <summary>db-design §2.3: a target, or a category outside the two, violates the shape constraint.</summary>
    [Theory]
    [InlineData("app_user", 1L, "succeeded", null)]
    [InlineData(null, null, "refused", "wrong_password")]
    public async Task TheShapeConstraint_RejectsAMalformedRow(
        string? targetType,
        long? targetId,
        string outcome,
        string? category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await host.InsertAppUserAsync(ct);

        var insert = async () => await host.ExecuteAsync(
            Insert,
            ct,
            ("stamp", host.Time.GetUtcNow()),
            ("role", SynchronizationRequestTestData.Audit.RoleAdmin),
            ("action", SynchronizationRequestTestData.Audit.Action),
            ("targetType", targetType),
            ("targetId", targetId),
            ("outcome", outcome),
            ("category", category));

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("ck_audit_event_sync_request_shape", error.ConstraintName);
    }

    /// <summary>Entity model §2.2: the refused factory accepts only the two categories of the use case.</summary>
    [Theory]
    [InlineData(AuditRefusalCategory.WrongPassword)]
    [InlineData(AuditRefusalCategory.DomainMismatch)]
    public void TheRefusedFactory_RejectsOtherCategories(AuditRefusalCategory category)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditEvent.SynchronizationRequestRefused(
            1, AppRole.Admin, category, DateTimeOffset.UnixEpoch, "r-1"));
    }

    /// <summary>Entity model §2.2: the role comes from the session, so both factories record the given one.</summary>
    [Theory]
    [InlineData(AppRole.Admin)]
    [InlineData(AppRole.Dean)]
    public void TheFactories_RecordTheGivenRole(AppRole role)
    {
        var accepted = AuditEvent.SynchronizationRequested(7, role, DateTimeOffset.UnixEpoch, "r-1");
        var refused = AuditEvent.SynchronizationRequestRefused(
            7, role, AuditRefusalCategory.ConnectionNotUsable, DateTimeOffset.UnixEpoch, "r-2");

        Assert.Equal(role, accepted.ActorRole);
        Assert.Equal(AuditActorType.AppUser, accepted.ActorType);
        Assert.Equal(7, accepted.ActorId);
        Assert.Null(accepted.TargetType);
        Assert.Null(accepted.TargetId);
        Assert.Equal(AuditAction.SynchronizationRequested, accepted.Action);
        Assert.Equal(AuditOutcome.Succeeded, accepted.Outcome);

        Assert.Equal(role, refused.ActorRole);
        Assert.Equal(AuditActorType.AppUser, refused.ActorType);
        Assert.Null(refused.TargetType);
        Assert.Null(refused.TargetId);
        Assert.Equal(AuditAction.SynchronizationRequested, refused.Action);
        Assert.Equal(AuditOutcome.Refused, refused.Outcome);
        Assert.Equal(AuditRefusalCategory.ConnectionNotUsable, refused.RefusalCategory);
    }
}
