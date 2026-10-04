using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec FR-004, FR-013, FR-014, AC-001, AC-002, AC-003, AC-006, AC-007, AC-009, api-design §2.6: creating and
/// changing a template — what is stored, the evaluation order (read-only guard first), the audit rows and the
/// refusals that write nothing.
/// </summary>
public sealed class ReportTemplateSaveTests
{
    private const string RequestId = "r-1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ReportTemplateFormBuilder RichForm(string name = "  Journal Two  ") =>
        ReportTemplateFormBuilder.Valid(name)
            .Set("view", "short")
            .Set("hideMaterials", "true")
            .Set("hoursPerLesson", "3")
            .Ranges(ReportTemplateTestData.TwoRow)
            .Mark(ReportCellState.NotAssigned, "own", "none")
            .Mark(ReportCellState.Returned, "empty")
            .LateMark("own", "late");

    private static ReportTemplateSettings RichSettings() =>
        ReportTemplateTestData.Settings(
            ReportView.Short,
            true,
            3,
            ReportTemplateTestData.TwoRow,
            new Dictionary<ReportCellState, ReportMark>
            {
                [ReportCellState.NotAssigned] = new(ReportMarkKind.Own, "none"),
                [ReportCellState.Returned] = new(ReportMarkKind.Empty, null),
            },
            new ReportLateMark(ReportLateMarkKind.Own, "late"));

    private static Task<ReportTemplateSaveResult> CreateAsync(
        ReportTemplateWorld world,
        ReportTemplateFormBuilder form,
        long actorId = ReportTemplateWorld.DeanId,
        AppRole role = AppRole.Dean) =>
        world.Save.CreateAsync(actorId, role, form.Input(), RequestId, Ct);

    private static Task<ReportTemplateSaveResult> ChangeAsync(
        ReportTemplateWorld world,
        string? reference,
        ReportTemplateFormBuilder form,
        long actorId = ReportTemplateWorld.DeanId,
        AppRole role = AppRole.Dean) =>
        world.Save.ChangeAsync(actorId, role, reference, form.Input(), RequestId, Ct);

    private static void AssertNothingWritten(ReportTemplateWorld world)
    {
        Assert.Empty(world.Templates.Added);
        Assert.Empty(world.Templates.MarkedChanged);
        Assert.Empty(world.Templates.Removed);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    /// <summary>AC-002, FR-004: a valid form creates the template, trimmed, with the actor as its author.</summary>
    [Fact]
    public async Task AValidForm_CreatesTheTemplate_WithTheActorAsAuthor()
    {
        var world = new ReportTemplateWorld();

        var result = await CreateAsync(world, RichForm(), ReportTemplateWorld.AdminId, AppRole.Admin);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Null(result.Form);
        var template = Assert.Single(world.Templates.Added);
        Assert.Equal(template.Id, result.TemplateId);
        Assert.True(template.Id > 0);
        Assert.Equal("Journal Two", template.Name);
        Assert.Equal(ReportTemplateWorld.AdminId, template.AuthorId);
        ReportTemplateExpectations.AssertSame(RichSettings(), template);
        Assert.Equal(1, world.Work.Transactions);
        // db-design §2.4: the template is saved first so the audit row can carry its generated id, in one transaction.
        Assert.Equal(2, world.Work.Commits);
        Assert.Equal([(1, 0), (1, 1)], world.Work.Staged);
    }

    /// <summary>AC-002, FR-004: an unchanged copy of the built-in template saves as an ordinary template.</summary>
    [Fact]
    public async Task ACopyOfTheBuiltIn_IsSavedAsAnOrdinaryTemplate()
    {
        var world = new ReportTemplateWorld();

        var result = await CreateAsync(world, ReportTemplateExpectations.BuiltInCopyForm());

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        var template = Assert.Single(world.Templates.Added);
        Assert.Equal(ReportTemplateExpectations.BuiltInCopyName, template.Name);
        ReportTemplateExpectations.AssertSame(ReportTemplateExpectations.BuiltIn(), template);
    }

    /// <summary>AC-007, FR-013: the create row names action, actor, role and the template id, no text.</summary>
    [Fact]
    public async Task TheCreate_IsAuditedWithTheTemplateIdOnly()
    {
        var world = new ReportTemplateWorld();

        var result = await CreateAsync(world, RichForm(), ReportTemplateWorld.AdminId, AppRole.Admin);

        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateCreated,
            AuditOutcome.Succeeded,
            ReportTemplateWorld.AdminId,
            AppRole.Admin,
            result.TemplateId);
        Assert.Equal(RequestId, world.Audit.Written.Single().RequestId);
    }

    /// <summary>AC-002: a change replaces name and settings, keeps the author and tells the repository.</summary>
    [Fact]
    public async Task AChange_ReplacesTheSettings_KeepsTheAuthor()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Old Name", authorId: ReportTemplateWorld.DeanId);

        var result = await ChangeAsync(world, template.Id.ToString(), RichForm());

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Equal(template.Id, result.TemplateId);
        Assert.Equal("Journal Two", template.Name);
        Assert.Equal(ReportTemplateWorld.DeanId, template.AuthorId);
        ReportTemplateExpectations.AssertSame(RichSettings(), template);
        Assert.Contains(template, world.Templates.MarkedChanged);
        Assert.Empty(world.Templates.Added);
        Assert.Equal(1, world.Work.Commits);
    }

    /// <summary>AC-007: the change row.</summary>
    [Fact]
    public async Task TheChange_IsAudited()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate();

        await ChangeAsync(world, template.Id.ToString(), RichForm());

        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateChanged,
            AuditOutcome.Succeeded,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            template.Id);
    }

    /// <summary>AC-002: any account may change a template another account created; the author stays.</summary>
    [Fact]
    public async Task AnotherAccount_CanChangeTheTemplate()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate(authorId: ReportTemplateWorld.DeanId);

        var result = await ChangeAsync(
            world,
            template.Id.ToString(),
            RichForm(),
            ReportTemplateWorld.AdminId,
            AppRole.Admin);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Equal(ReportTemplateWorld.DeanId, template.AuthorId);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateChanged,
            AuditOutcome.Succeeded,
            ReportTemplateWorld.AdminId,
            AppRole.Admin,
            template.Id);
    }

    /// <summary>VR-001: a change may keep the template's own name; the uniqueness check excludes it.</summary>
    [Fact]
    public async Task AChange_MayKeepItsOwnName()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Test Template One");

        var result = await ChangeAsync(world, template.Id.ToString(), ReportTemplateFormBuilder.Valid(" test template one "));

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Contains((long?)template.Id, world.Templates.NameChecksExcept);
    }

    /// <summary>VR-001: a change to another template's name is refused with the change form.</summary>
    [Fact]
    public async Task AChange_ToAnotherTemplatesName_IsRefused_WithTheChangeForm()
    {
        var world = new ReportTemplateWorld();
        world.SeedTemplate("Taken", authorId: ReportTemplateWorld.DeanId);
        var template = world.SeedTemplate("Mine");

        var result = await ChangeAsync(world, template.Id.ToString(), ReportTemplateFormBuilder.Valid("taken"));

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Equal(ReportTemplateFormMode.Change, form.Mode);
        Assert.Equal(template.Id.ToString(), form.Reference);
        Assert.Contains(new ReportTemplateFieldError("name", ReportTemplateFieldErrorKey.NameNotUnique, null), form.FieldErrors);
        Assert.Equal("Mine", template.Name);
        Assert.Empty(world.Templates.MarkedChanged);
        Assert.Empty(world.Audit.Written);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>VR-004, AC-003: with no conversion any scale rows sent are ignored and not stored.</summary>
    [Fact]
    public async Task NoConversion_IgnoresAnyScaleRowsSent()
    {
        var world = new ReportTemplateWorld();
        var form = ReportTemplateFormBuilder.Valid()
            .Ranges([("abc", "-5", ""), ("0", "100", "ok")])
            .Set("scaleMode", "none");

        var result = await CreateAsync(world, form);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        var template = Assert.Single(world.Templates.Added);
        Assert.Equal(ReportScaleMode.None, template.ScaleMode);
        Assert.Empty(template.ScaleRows);
    }

    /// <summary>db-design §2.1: a duplicate name found only at commit is reported as a name error and nothing is saved.</summary>
    [Fact]
    public async Task ADuplicateAtCommit_IsReportedAsNameNotUnique()
    {
        var world = new ReportTemplateWorld();
        world.Work.FailNextWithDuplicateName = true;

        var result = await CreateAsync(world, ReportTemplateFormBuilder.Valid(" Raced "));

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Equal(ReportTemplateFormMode.Create, form.Mode);
        Assert.Equal("Raced", form.Values.Name);
        Assert.Contains(new ReportTemplateFieldError("name", ReportTemplateFieldErrorKey.NameNotUnique, null), form.FieldErrors);
        Assert.Null(result.TemplateId);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>db-design §2.1: the same for a change.</summary>
    [Fact]
    public async Task ADuplicateAtCommit_OnAChange_IsReportedAsNameNotUnique()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate();
        world.Work.FailNextWithDuplicateName = true;

        var result = await ChangeAsync(world, template.Id.ToString(), ReportTemplateFormBuilder.Valid("Raced"));

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Equal(ReportTemplateFormMode.Change, form.Mode);
        Assert.Contains(new ReportTemplateFieldError("name", ReportTemplateFieldErrorKey.NameNotUnique, null), form.FieldErrors);
        Assert.Equal(0, world.Work.Commits);
    }

    /// <summary>AC-007, AC-009: a validation failure writes no template and no audit row, on create and on change.</summary>
    [Fact]
    public async Task AValidationFailure_WritesNothing()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Keep");

        var created = await CreateAsync(world, ReportTemplateFormBuilder.Valid().Set("hoursPerLesson", "0"));
        var changed = await ChangeAsync(
            world,
            template.Id.ToString(),
            ReportTemplateFormBuilder.Valid("Other").Set("hoursPerLesson", "0"));

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, created.Outcome);
        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, changed.Outcome);
        Assert.Equal("Keep", template.Name);
        AssertNothingWritten(world);
    }

    /// <summary>AC-001: the built-in template is refused on change, nothing is written, nothing audited, no read.</summary>
    [Fact]
    public async Task ChangingTheBuiltIn_IsRefused_AndNotAudited()
    {
        var world = new ReportTemplateWorld();

        var result = await ChangeAsync(world, ReportTemplateTestData.BuiltInKey, ReportTemplateFormBuilder.Valid());

        Assert.Equal(ReportTemplateOutcome.BuiltInNotChangeable, result.Outcome);
        Assert.Null(result.TemplateId);
        Assert.Null(result.Form);
        Assert.Empty(world.Templates.Calls);
        AssertNothingWritten(world);
    }

    /// <summary>AC-002: an unknown template is "not found", not audited.</summary>
    [Fact]
    public async Task ChangingAMissingTemplate_IsNotFound()
    {
        var world = new ReportTemplateWorld();

        var result = await ChangeAsync(world, "999", ReportTemplateFormBuilder.Valid());

        Assert.Equal(ReportTemplateOutcome.NotFound, result.Outcome);
        Assert.Null(result.Form);
        AssertNothingWritten(world);
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

        var result = await ChangeAsync(world, reference, ReportTemplateFormBuilder.Valid());

        Assert.Equal(ReportTemplateOutcome.ReferenceMalformed, result.Outcome);
        Assert.Empty(world.Templates.Calls);
        AssertNothingWritten(world);
    }

    /// <summary>api-design §2.6: a tampered form on a change is "form malformed", after the reference is known to exist.</summary>
    [Fact]
    public async Task AMalformedForm_OnAChange_IsRefused_AndWritesNothing()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate();

        var result = await ChangeAsync(world, template.Id.ToString(), ReportTemplateFormBuilder.Valid().Remove("view"));

        Assert.Equal(ReportTemplateOutcome.FormMalformed, result.Outcome);
        AssertNothingWritten(world);
    }

    /// <summary>AC-006: on create the guard runs first, only the refusal is written, committed once.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheGuardRunsFirst_AndOnlyTheRefusalIsWritten_OnCreate()
    {
        var world = new ReportTemplateWorld(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(() => CreateAsync(world, ReportTemplateFormBuilder.Valid()));

        Assert.Empty(world.Templates.Calls);
        Assert.Empty(world.Templates.Added);
        Assert.Equal(1, world.Work.Commits);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateCreated,
            AuditOutcome.Refused,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            null,
            AuditRefusalCategory.ReadOnlyMode);
        Assert.Equal([SaveReportTemplateUseCase.CreateOperation], world.ReadOnly.Operations);
    }

    /// <summary>AC-006: on change the guard runs first, even for a malformed form, and nothing is read.</summary>
    [Fact]
    public async Task InReadOnlyMode_TheGuardRunsFirst_AndOnlyTheRefusalIsWritten_OnChange()
    {
        var world = new ReportTemplateWorld(readOnly: true);
        var template = world.SeedTemplate();

        await Assert.ThrowsAsync<ReadOnlyModeException>(
            () => ChangeAsync(world, template.Id.ToString(), ReportTemplateFormBuilder.Valid().Remove("view")));

        Assert.Empty(world.Templates.Calls);
        Assert.Empty(world.Templates.MarkedChanged);
        Assert.Equal(1, world.Work.Commits);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateChanged,
            AuditOutcome.Refused,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            template.Id,
            AuditRefusalCategory.ReadOnlyMode);
        Assert.Equal([SaveReportTemplateUseCase.ChangeOperation], world.ReadOnly.Operations);
    }

    /// <summary>AC-007: the refused row's target is the reference only when it is an id.</summary>
    [Theory]
    [InlineData("7", 7L)]
    [InlineData("academic-journal", null)]
    [InlineData("abc", null)]
    [InlineData("0", null)]
    [InlineData("99999999999999999999", null)]
    public async Task TheReadOnlyRefusal_TargetsTheReferenceOnlyWhenItIsAnId(string reference, long? targetId)
    {
        var world = new ReportTemplateWorld(readOnly: true);

        await Assert.ThrowsAsync<ReadOnlyModeException>(
            () => ChangeAsync(world, reference, ReportTemplateFormBuilder.Valid()));

        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateChanged,
            AuditOutcome.Refused,
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            targetId,
            AuditRefusalCategory.ReadOnlyMode);
    }
}
