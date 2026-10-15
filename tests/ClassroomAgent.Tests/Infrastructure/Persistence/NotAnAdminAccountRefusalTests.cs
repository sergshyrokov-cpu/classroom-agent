using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Infrastructure.Persistence;

/// <summary>
/// US-051 AC-008, FR-008: the audit table accepts the new refusal category <c>not_an_admin_account</c>, still rejects
/// an unknown one, and the category round-trips between the enum member and its stored code (db-design §2, §3, §6;
/// migration <c>AddNotAnAdminAccountRefusal</c>; TC-2).
/// </summary>
public sealed class NotAnAdminAccountRefusalTests(PostgreSqlFixture database)
{
    private const string Insert =
        """
        INSERT INTO audit_event (occurred_at, actor_type, actor_id, actor_role, action, target_type, target_id,
                                 outcome, refusal_category, request_id, created_at, updated_at)
        VALUES (now(), 'app_user', 1, 'dean', 'admin_sign_in', NULL, NULL, 'refused', @refusalCategory,
                'request-id', now(), now())
        """;

    /// <summary>AC-008: the row of AC-002 — a Dean actor, Admin sign-in, refused, the new code — is accepted.</summary>
    [Fact]
    public async Task TheNewCategory_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        await host.ExecuteAsync(Insert, ct, ("refusalCategory", SignInTestData.RefusalCategories.NotAnAdminAccount));

        var row = Assert.Single(await host.AuditRowsAsync(ct));
        Assert.Equal(SignInTestData.RefusalCategories.NotAnAdminAccount, row.RefusalCategory);
        Assert.Equal("dean", row.ActorRole);
    }

    /// <summary>AC-008, VR-003: the list stays closed — a near miss of the new code is still rejected.</summary>
    [Theory]
    [InlineData("not_admin_account")]
    [InlineData("NotAnAdminAccount")]
    public async Task AnUnknownCategory_IsStillRejected(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.ExecuteAsync(Insert, ct, ("refusalCategory", category)));

        Assert.Contains("ck_audit_event_refusal_category_value", Text(failure), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// db-design §2, §6: written through the production mapping as <see cref="AuditRefusalCategory.NotAnAdminAccount"/>,
    /// stored as <c>not_an_admin_account</c>, read back as the same member.
    /// </summary>
    [Fact]
    public async Task TheCategory_RoundTripsThroughTheMapping()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        using (var scope = host.CreateScope())
        {
            var services = scope.ServiceProvider;
            services.GetRequiredService<IAuditEventRepository>().Add(AuditEvent.AdminSignInRefused(
                42,
                AppRole.Dean,
                AuditRefusalCategory.NotAnAdminAccount,
                host.Time.GetUtcNow(),
                "r-round-trip"));
            using (services.GetRequiredService<ServiceWriteScope>().Declare(PermittedServiceWrite.AuditEvent))
            {
                await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
            }
        }

        Assert.Equal(SignInTestData.RefusalCategories.NotAnAdminAccount, Assert.Single(await host.AuditRowsAsync(ct)).RefusalCategory);
        using var readScope = host.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>()
            .AuditEvents.AsNoTracking().SingleAsync(e => e.RequestId == "r-round-trip", ct);
        Assert.Equal(AuditRefusalCategory.NotAnAdminAccount, stored.RefusalCategory);
        Assert.Equal(AppRole.Dean, stored.ActorRole);
    }

    private static string Text(Exception exception)
    {
        var text = new System.Text.StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }
}
