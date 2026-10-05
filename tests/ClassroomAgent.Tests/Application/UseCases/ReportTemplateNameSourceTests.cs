using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-042 AC-005, AC-008, AC-010 (spec FR-002, FR-007, VR-001; api-design §2.3; entity model §1.3, §1.4): the template
/// setting "names" — its value on the new, copy and change forms, how create and change store it, the refusal of a
/// tampered value as a field error, the audit row, and the aggregate that holds it.
/// </summary>
public sealed class ReportTemplateNameSourceTests
{
    private const string RequestId = "r-1";

    private const string BuiltInName = "Академічний журнал";

    private const string Suffix = " (копія)";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ReportTemplateFormBuilder Form(string names, string name = "Test Template Names") =>
        ReportTemplateFormBuilder.Valid(name).Set(ReportTemplateTestData.NamesField, names);

    private static ReportTemplateSettings WithSource(ReportNameSource source) =>
        ReportTemplateTestData.Settings(nameSource: source);

    private static void AssertNothingWritten(ReportTemplateWorld world)
    {
        Assert.Empty(world.Templates.Added);
        Assert.Empty(world.Templates.MarkedChanged);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    // ---------- The aggregate (entity model §1.3, §1.4)

    /// <summary>AC-005, D-7: the built-in "Academic journal" uses the profile.</summary>
    [Fact]
    public void TheBuiltIn_UsesTheProfile()
    {
        Assert.Equal(ReportNameSource.Profile, BuiltInReportTemplates.AcademicJournal.NameSource);
    }

    /// <summary>Entity model §1.4: create and change store the setting, and the settings read it back.</summary>
    [Fact]
    public void ATemplate_StoresTheSetting_AndChangeReplacesIt()
    {
        var template = ReportTemplate.Create("Test Template One", WithSource(ReportNameSource.Email), ReportTemplateWorld.DeanId);
        Assert.Equal(ReportNameSource.Email, template.NameSource);
        Assert.Equal(ReportNameSource.Email, template.ToSettings().NameSource);

        template.Change("Test Template One", WithSource(ReportNameSource.Profile));

        Assert.Equal(ReportNameSource.Profile, template.NameSource);
        Assert.Equal(ReportNameSource.Profile, template.ToSettings().NameSource);
    }

    /// <summary>Entity model §1.4: an undefined value is a programming error.</summary>
    [Fact]
    public void AnUndefinedSetting_IsRejectedByTheAggregate()
    {
        var exception = Record.Exception(() =>
            ReportTemplate.Create("Test Template One", WithSource((ReportNameSource)7), ReportTemplateWorld.DeanId));

        Assert.IsType<ArgumentException>(exception, exactMatch: false);
    }

    // ---------- The forms (spec FR-002)

    /// <summary>AC-005, spec I-2: a new template starts with the profile.</summary>
    [Fact]
    public void TheNewForm_StartsWithTheProfile()
    {
        var world = new ReportTemplateWorld();

        var form = world.Forms.New();

        Assert.Equal("profile", form.Values.Names);
    }

    /// <summary>AC-005: a copy of the built-in takes "profile"; a copy of a created template takes its value.</summary>
    [Fact]
    public async Task ACopy_TakesTheSourcesSetting()
    {
        var world = new ReportTemplateWorld();
        var email = world.SeedTemplate("Test Template Email", WithSource(ReportNameSource.Email));

        var ofBuiltIn = await world.Forms.CopyAsync(ReportTemplateTestData.BuiltInKey, BuiltInName, Suffix, Ct);
        var ofEmail = await world.Forms.CopyAsync(email.Id.ToString(CultureInfo.InvariantCulture), BuiltInName, Suffix, Ct);

        Assert.Equal("profile", ofBuiltIn.Form!.Values.Names);
        Assert.Equal("email", ofEmail.Form!.Values.Names);
    }

    /// <summary>AC-005: the change form shows the stored setting.</summary>
    [Theory]
    [InlineData(ReportNameSource.Profile, "profile")]
    [InlineData(ReportNameSource.Email, "email")]
    public async Task TheChangeForm_ShowsTheStoredSetting(ReportNameSource stored, string shown)
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate(settings: WithSource(stored));

        var result = await world.Forms.EditAsync(template.Id.ToString(CultureInfo.InvariantCulture), Ct);

        Assert.Equal(shown, result.Form!.Values.Names);
    }

    // ---------- The saves

    /// <summary>AC-005: create stores the chosen source, both values.</summary>
    [Theory]
    [InlineData("profile", ReportNameSource.Profile)]
    [InlineData("email", ReportNameSource.Email)]
    public async Task Create_StoresTheChosenSource(string names, ReportNameSource stored)
    {
        var world = new ReportTemplateWorld();

        var result = await world.Save.CreateAsync(ReportTemplateWorld.DeanId, AppRole.Dean, Form(names).Input(), RequestId, Ct);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Equal(stored, Assert.Single(world.Templates.Added).NameSource);
    }

    /// <summary>
    /// AC-005, AC-008: a copy switches both ways; each change writes exactly one <c>ReportTemplateChanged</c> row with
    /// the template id only.
    /// </summary>
    [Theory]
    [InlineData(ReportNameSource.Profile, "email", ReportNameSource.Email)]
    [InlineData(ReportNameSource.Email, "profile", ReportNameSource.Profile)]
    public async Task Change_SwitchesTheSource_AndAuditsTheTemplateIdOnly(
        ReportNameSource before, string names, ReportNameSource after)
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Test Template Names", WithSource(before));

        var result = await world.Save.ChangeAsync(
            ReportTemplateWorld.AdminId,
            AppRole.Admin,
            template.Id.ToString(CultureInfo.InvariantCulture),
            Form(names).Input(),
            RequestId,
            Ct);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Equal(after, template.NameSource);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world,
            AuditAction.ReportTemplateChanged,
            AuditOutcome.Succeeded,
            ReportTemplateWorld.AdminId,
            AppRole.Admin,
            template.Id);
    }

    /// <summary>AC-008, spec FR-007: saving with the setting unchanged still writes the one usual row.</summary>
    [Fact]
    public async Task Change_WithTheSettingUnchanged_IsAuditedTheSameWay()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Test Template Names", WithSource(ReportNameSource.Email));

        await world.Save.ChangeAsync(
            ReportTemplateWorld.DeanId, AppRole.Dean, template.Id.ToString(CultureInfo.InvariantCulture), Form("email").Input(), RequestId, Ct);

        Assert.Equal(ReportNameSource.Email, template.NameSource);
        ReportTemplateExpectations.AssertSingleAuditRow(
            world, AuditAction.ReportTemplateChanged, AuditOutcome.Succeeded, ReportTemplateWorld.DeanId, AppRole.Dean, template.Id);
    }

    /// <summary>AC-005, US-027 FR-006: the built-in template's setting cannot be changed.</summary>
    [Fact]
    public async Task TheBuiltInsSetting_CannotBeChanged()
    {
        var world = new ReportTemplateWorld();

        var result = await world.Save.ChangeAsync(
            ReportTemplateWorld.DeanId, AppRole.Dean, ReportTemplateTestData.BuiltInKey, Form("email").Input(), RequestId, Ct);

        Assert.Equal(ReportTemplateOutcome.BuiltInNotChangeable, result.Outcome);
        Assert.Equal(ReportNameSource.Profile, BuiltInReportTemplates.AcademicJournal.NameSource);
        AssertNothingWritten(world);
    }

    /// <summary>AC-010, VR-001: what a tampered form may send instead of exactly one of the two values.</summary>
    public static TheoryData<string?[]> InvalidNames => new()
    {
        Array.Empty<string?>(),
        new string?[] { "" },
        new string?[] { "Email" },
        new string?[] { " profile" },
        new string?[] { "full" },
        new string?[] { "zz-bad-zz" },
        new string?[] { "email", "email" },
        new string?[] { "profile", "email" },
    };

    private static ReportTemplateFormBuilder Tampered(string?[] names)
    {
        var form = ReportTemplateFormBuilder.Valid("Test Template Names").Remove(ReportTemplateTestData.NamesField);
        foreach (var value in names)
        {
            form.Repeat(ReportTemplateTestData.NamesField, value);
        }

        return form;
    }

    /// <summary>
    /// AC-010, VR-001, api-design §2.3: on create a missing, empty, repeated or unknown value re-renders the form with
    /// <c>NameSourceInvalid</c> on <c>names</c>, the other values as entered and <c>names</c> empty; nothing is saved
    /// or audited.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Create_WithAnInvalidSource_ReRendersTheForm(string?[] names)
    {
        var world = new ReportTemplateWorld();

        var result = await world.Save.CreateAsync(ReportTemplateWorld.DeanId, AppRole.Dean, Tampered(names).Input(), RequestId, Ct);

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Contains(new ReportTemplateFieldError("names", ReportTemplateFieldErrorKey.NameSourceInvalid, null), form.FieldErrors);
        Assert.Equal(string.Empty, form.Values.Names);
        Assert.Equal("Test Template Names", form.Values.Name);
        Assert.Equal("full", form.Values.View);
        AssertNothingWritten(world);
    }

    /// <summary>AC-010, VR-001: the same refusal on change; the stored setting is untouched.</summary>
    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Change_WithAnInvalidSource_ReRendersTheForm(string?[] names)
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Test Template Names", WithSource(ReportNameSource.Email));

        var result = await world.Save.ChangeAsync(
            ReportTemplateWorld.DeanId, AppRole.Dean, template.Id.ToString(CultureInfo.InvariantCulture), Tampered(names).Input(), RequestId, Ct);

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Contains(new ReportTemplateFieldError("names", ReportTemplateFieldErrorKey.NameSourceInvalid, null), form.FieldErrors);
        Assert.Equal(string.Empty, form.Values.Names);
        Assert.Equal(ReportNameSource.Email, template.NameSource);
        AssertNothingWritten(world);
    }

    /// <summary>
    /// Api-design §2.3: the source is checked with the other fields — an invalid name and an invalid source are both
    /// reported at once.
    /// </summary>
    [Fact]
    public async Task AnInvalidSource_IsReportedWithTheOtherFieldErrors()
    {
        var world = new ReportTemplateWorld();
        var form = Tampered(["bad"]).Set("name", "   ");

        var result = await world.Save.CreateAsync(ReportTemplateWorld.DeanId, AppRole.Dean, form.Input(), RequestId, Ct);

        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        var errors = result.Form!.FieldErrors.Select(e => e.Key).ToList();
        Assert.Contains(ReportTemplateFieldErrorKey.NameRequired, errors);
        Assert.Contains(ReportTemplateFieldErrorKey.NameSourceInvalid, errors);
    }
}
