using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec FR-004, FR-006, FR-007, AC-001, AC-002, api-design §2.3, §2.4: the new, copy and edit forms and the
/// deletion page — defaults, prefilled values, the built-in copy and the refusals.
/// </summary>
public sealed class ReportTemplateFormQueryTests
{
    private const string BuiltInName = "Академічний журнал";

    private const string Suffix = " (копія)";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ReportTemplateSettings SeededSettings() =>
        ReportTemplateTestData.Settings(
            ReportView.Short,
            true,
            3,
            ReportTemplateTestData.TwoRow,
            new Dictionary<ReportCellState, ReportMark>
            {
                [ReportCellState.TurnedIn] = new(ReportMarkKind.Own, "ok"),
                [ReportCellState.Returned] = new(ReportMarkKind.Empty, null),
            },
            new ReportLateMark(ReportLateMarkKind.Hidden, null));

    private static void AssertSeededValues(ReportTemplateFormValues values)
    {
        Assert.Equal("short", values.View);
        Assert.Equal("true", values.HideMaterials);
        Assert.Equal("ranges", values.ScaleMode);
        Assert.Equal("3", values.HoursPerLesson);
        Assert.Equal(
            [new ReportTemplateScaleRowValues("0", "49", "low"), new ReportTemplateScaleRowValues("50", "100", "high")],
            values.Scale);
        Assert.Equal(ReportTemplateTestData.States.Count, values.Marks.Count);
        Assert.Equal("own", values.Marks[ReportCellState.TurnedIn].Kind);
        Assert.Equal("ok", values.Marks[ReportCellState.TurnedIn].Text);
        Assert.Equal("empty", values.Marks[ReportCellState.Returned].Kind);
        Assert.True(string.IsNullOrEmpty(values.Marks[ReportCellState.Returned].Text));
        Assert.Equal("program", values.Marks[ReportCellState.NotAssigned].Kind);
        Assert.Equal("hidden", values.LateMark.Kind);
        Assert.All(
            values.Marks.Values.Where(m => m.Kind != "own"),
            m => Assert.True(string.IsNullOrEmpty(m.Text)));
    }

    /// <summary>FR-007, AC-002: the new form's defaults.</summary>
    [Fact]
    public void TheNewForm_HasTheDefaultsOfFr007()
    {
        var world = new ReportTemplateWorld();

        var form = world.Forms.New();

        Assert.Equal(ReportTemplateFormMode.Create, form.Mode);
        Assert.Null(form.Reference);
        Assert.Equal(string.Empty, form.Values.Name);
        Assert.Equal("full", form.Values.View);
        Assert.Equal("false", form.Values.HideMaterials);
        Assert.Equal("none", form.Values.ScaleMode);
        Assert.Equal("2", form.Values.HoursPerLesson);
        Assert.Empty(form.Values.Scale);
        Assert.Equal(ReportTemplateTestData.States.Count, form.Values.Marks.Count);
        Assert.All(form.Values.Marks.Values, m =>
        {
            Assert.Equal("program", m.Kind);
            Assert.True(string.IsNullOrEmpty(m.Text));
        });
        Assert.Equal("program", form.Values.LateMark.Kind);
        Assert.True(string.IsNullOrEmpty(form.Values.LateMark.Text));
        Assert.Empty(form.FieldErrors);
        Assert.Equal(
            ReportTemplateTestData.TwelvePoint.Select(r => new ScaleRange(r.From, r.To, r.Label)).ToList(),
            form.TwelvePointPreset.ToList());
        Assert.Empty(world.Templates.Calls);
    }

    /// <summary>FR-004, FR-006, AC-002: the copy form of the built-in carries the translated name plus the suffix and the FR-006 values.</summary>
    [Fact]
    public async Task TheCopyFormOfTheBuiltIn_IsPrefilled_WithTheTranslatedNameAndSuffix()
    {
        var world = new ReportTemplateWorld();

        var result = await world.Forms.CopyAsync(ReportTemplateTestData.BuiltInKey, BuiltInName, Suffix, Ct);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Equal(ReportTemplateFormMode.Copy, form.Mode);
        Assert.Null(form.Reference);
        Assert.Equal("Академічний журнал (копія)", form.Values.Name);
        Assert.Equal("short", form.Values.View);
        Assert.Equal("true", form.Values.HideMaterials);
        Assert.Equal("ranges", form.Values.ScaleMode);
        Assert.Equal("2", form.Values.HoursPerLesson);
        Assert.Equal(
            ReportTemplateTestData.TwelvePoint.Select(r => new ReportTemplateScaleRowValues(
                r.From.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.To.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.Label)).ToList(),
            form.Values.Scale.ToList());
        foreach (var state in ReportTemplateTestData.States)
        {
            if (state == ReportCellState.NotAssigned)
            {
                Assert.Equal(new ReportTemplateMarkValues("own", "—"), form.Values.Marks[state]);
            }
            else
            {
                Assert.Equal("empty", form.Values.Marks[state].Kind);
            }
        }

        Assert.Equal("hidden", form.Values.LateMark.Kind);
        Assert.Empty(form.FieldErrors);
        Assert.Empty(world.Templates.Calls);
    }

    /// <summary>FR-004, AC-002: the copy form of a created template carries its settings and its name plus the suffix.</summary>
    [Fact]
    public async Task TheCopyFormOfACreatedTemplate_CarriesItsSettings()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate("Seeded", SeededSettings());

        var result = await world.Forms.CopyAsync(template.Id.ToString(), BuiltInName, Suffix, Ct);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Equal(ReportTemplateFormMode.Copy, form.Mode);
        Assert.Null(form.Reference);
        Assert.Equal("Seeded (копія)", form.Values.Name);
        AssertSeededValues(form.Values);
    }

    /// <summary>VR-006: a copy of an unknown or malformed template.</summary>
    [Fact]
    public async Task TheCopyForm_OfAMissingOrMalformedTemplate_IsRefused()
    {
        var world = new ReportTemplateWorld();

        var missing = await world.Forms.CopyAsync("999", BuiltInName, Suffix, Ct);
        var malformed = await world.Forms.CopyAsync("abc", BuiltInName, Suffix, Ct);

        Assert.Equal(ReportTemplateOutcome.NotFound, missing.Outcome);
        Assert.Null(missing.Form);
        Assert.Equal(ReportTemplateOutcome.ReferenceMalformed, malformed.Outcome);
        Assert.Null(malformed.Form);
    }

    /// <summary>FR-004, AC-002: the edit form shows the template; the built-in, unknown and malformed are refused.</summary>
    [Fact]
    public async Task TheEditForm_AndTheDeletePage_ShowTheTemplate()
    {
        var world = new ReportTemplateWorld();
        var template = world.SeedTemplate(" Seeded  Name ", SeededSettings());
        var id = template.Id.ToString();

        var edit = await world.Forms.EditAsync(id, Ct);
        var deletion = await world.Forms.DeletionAsync(id, Ct);

        Assert.Equal(ReportTemplateOutcome.Succeeded, edit.Outcome);
        var form = Assert.IsType<ReportTemplateFormPageModel>(edit.Form);
        Assert.Equal(ReportTemplateFormMode.Change, form.Mode);
        Assert.Equal(id, form.Reference);
        Assert.Equal(template.Name, form.Values.Name);
        Assert.Empty(form.FieldErrors);
        AssertSeededValues(form.Values);
        Assert.Equal(ReportTemplateOutcome.Succeeded, deletion.Outcome);
        Assert.Equal(new ReportTemplateDeletePageModel(id, template.Name), deletion.Page);
    }

    /// <summary>AC-001: the edit form of the built-in is refused.</summary>
    [Fact]
    public async Task TheEditFormOfTheBuiltIn_IsRefused()
    {
        var world = new ReportTemplateWorld();

        var result = await world.Forms.EditAsync(ReportTemplateTestData.BuiltInKey, Ct);

        Assert.Equal(ReportTemplateOutcome.BuiltInNotChangeable, result.Outcome);
        Assert.Null(result.Form);
    }

    /// <summary>AC-001: the deletion page of the built-in is refused.</summary>
    [Fact]
    public async Task TheDeletionPageOfTheBuiltIn_IsRefused()
    {
        var world = new ReportTemplateWorld();

        var result = await world.Forms.DeletionAsync(ReportTemplateTestData.BuiltInKey, Ct);

        Assert.Equal(ReportTemplateOutcome.BuiltInNotChangeable, result.Outcome);
        Assert.Null(result.Page);
    }

    /// <summary>VR-006: edit and deletion of an unknown or malformed template.</summary>
    [Fact]
    public async Task TheEditForm_AndTheDeletePage_OfAMissingOrMalformedTemplate_AreRefused()
    {
        var world = new ReportTemplateWorld();

        var editMissing = await world.Forms.EditAsync("999", Ct);
        var editMalformed = await world.Forms.EditAsync("0", Ct);
        var deletionMissing = await world.Forms.DeletionAsync("999", Ct);
        var deletionMalformed = await world.Forms.DeletionAsync("abc", Ct);

        Assert.Equal(ReportTemplateOutcome.NotFound, editMissing.Outcome);
        Assert.Null(editMissing.Form);
        Assert.Equal(ReportTemplateOutcome.ReferenceMalformed, editMalformed.Outcome);
        Assert.Null(editMalformed.Form);
        Assert.Equal(ReportTemplateOutcome.NotFound, deletionMissing.Outcome);
        Assert.Null(deletionMissing.Page);
        Assert.Equal(ReportTemplateOutcome.ReferenceMalformed, deletionMalformed.Outcome);
        Assert.Null(deletionMalformed.Page);
    }
}
