using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec FR-004, FR-013, AC-001, AC-002, AC-006, AC-007, api-design §2.6: deleting a template — the audit row,
/// the second delete, the built-in and malformed-reference refusals, and the read-only order.
/// </summary>
public sealed class ReportTemplateDeleteTests
{
    private const string RequestId = "r-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<ReportTemplateOutcome> DeleteAsync(
        ReportTemplateWorld world,
        string? reference,
        long actorId = ReportTemplateWorld.DeanId,
        AppRole role = AppRole.Dean) =>
        world.Delete.ExecuteAsync(actorId, role, reference, RequestId, Ct);

    /// <summary>AC-002, AC-007: a delete removes the template and writes one audit row.</summary>
    [Fact]
    public async Task ATemplate_IsDeleted_AndASecondDeleteIsNotFound()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate();

        var first = await DeleteAsync(world, template.Id.ToString());
        var second = await DeleteAsync(world, template.Id.ToString());

        Assert.Equal(ReportTemplateOutcome.Succeeded, first);
        Assert.Equal(ReportTemplateOutcome.NotFound, second);
        Assert.Equal([template], world.Templates.Removed);
        Assert.Empty(world.Templates.Stored);
        Assert.Equal(1, world.Work.Commits);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateDeleted,
            AuditOutcome.Succeeded,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            template.Id);
    }

    /// <summary>AC-007: the delete row names the acting account, whatever its role.</summary>
    [Fact]
    public async Task TheDelete_IsAudited()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate(authorId: ReportTemplateWorld.DeanId);

        await DeleteAsync(world, template.Id.ToString(), ReportTemplateWorld.AdminId, AppRole.Admin);

        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateDeleted,
            AuditOutcome.Succeeded,
            ReportTemplateWorld.AdminId,
            AppRole.Admin,
            template.Id);
        Assert.Equal(RequestId, world.Audit.Written.Single().RequestId);
    }

    /// <summary>AC-002: an unknown id is "not found", nothing written or audited.</summary>
    [Fact]
    public async Task DeletingAMissingTemplate_IsNotFound_AndNotAudited()
    {
        var world = new ReportTemplateWorld();

        var outcome = await DeleteAsync(world, "999");

        Assert.Equal(ReportTemplateOutcome.NotFound, outcome);
        Assert.Empty(world.Templates.Removed);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>AC-001: the built-in is refused before any read and is not audited.</summary>
    [Fact]
    public async Task DeletingTheBuiltIn_IsRefused_AndNotAudited()
    {
        var world = new ReportTemplateWorld();

        var outcome = await DeleteAsync(world, ReportTemplateTestData.BuiltInKey);

        Assert.Equal(ReportTemplateOutcome.BuiltInNotChangeable, outcome);
        Assert.Empty(world.Templates.Calls);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>VR-006: a malformed reference is refused before any read, not audited outside read-only mode.</summary>
    [Theory]
    [InlineData("Academic-Journal")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("abc")]
    [InlineData("99999999999999999999")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AMalformedReference_IsRefused_BeforeAnyRead(string? reference)
    {
        var world = new ReportTemplateWorld();

        var outcome = await DeleteAsync(world, reference);

        Assert.Equal(ReportTemplateOutcome.ReferenceMalformed, outcome);
        Assert.Empty(world.Templates.Calls);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>AC-006: the guard runs first; nothing is read or removed; one refused row is committed.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheGuardRunsFirst_AndOnlyTheRefusalIsWritten()
    {
        var world = new ReportTemplateWorld(readOnly: true);
        var template = world.SeedTemplate();

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => DeleteAsync(world, template.Id.ToString()));

        Assert.Empty(world.Templates.Calls);
        Assert.Empty(world.Templates.Removed);
        Assert.Single(world.Templates.Stored);
        Assert.Equal(1, world.Work.Commits);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateDeleted,
            AuditOutcome.Refused,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            template.Id,
            AuditRefusalCategory.ReadOnlyMode);
        Assert.Equal([DeleteReportTemplateUseCase.Operation], world.ReadOnly.Operations);
    }

    /// <summary>AC-007: the refused row's target is the reference only when it is an id.</summary>
    [Theory]
    [InlineData("7", 7L)]
    [InlineData("academic-journal", null)]
    [InlineData("abc", null)]
    [InlineData("0", null)]
    public async Task TheReadOnlyRefusal_TargetsTheReferenceOnlyWhenItIsAnId(string reference, long? targetId)
    {
        var world = new ReportTemplateWorld(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => DeleteAsync(world, reference));

        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateDeleted,
            AuditOutcome.Refused,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            targetId,
            AuditRefusalCategory.ReadOnlyMode);
    }
}
