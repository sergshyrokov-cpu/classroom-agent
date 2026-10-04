using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// Shared expectations of the US-027 unit tests: settings compared member by member (the record holds collections, so
/// its own equality is reference-based) and the settings of the built-in copy, spelled out independently of
/// production code (spec FR-006).
/// </summary>
internal static class ReportTemplateExpectations
{
    public const string BuiltInCopyName = "Академічний журнал (копія)";

    /// <summary>Spec FR-006 written out: short, materials hidden, 2 hours, 12-point ranges, "—" for not assigned.</summary>
    public static ReportTemplateSettings BuiltIn()
    {
        var marks = ReportTemplateTestData.States.ToDictionary(
            s => s,
            s => s == ReportCellState.NotAssigned
                ? new ReportMark(ReportMarkKind.Own, "—")
                : new ReportMark(ReportMarkKind.Empty, null));
        return new ReportTemplateSettings(
            ReportView.Short,
            true,
            2,
            ReportScaleMode.Ranges,
            ReportTemplateTestData.TwelvePoint.Select(r => new ReportScaleRow(r.From, r.To, r.Label)).ToList(),
            marks,
            new ReportLateMark(ReportLateMarkKind.Hidden, null));
    }

    /// <summary>The form a browser posts for an unchanged copy of the built-in template.</summary>
    public static ReportTemplateFormBuilder BuiltInCopyForm(string name = BuiltInCopyName)
    {
        var form = ReportTemplateFormBuilder.Valid(name)
            .Set("view", "short")
            .Set("hideMaterials", "true")
            .Set("hoursPerLesson", "2")
            .TwelvePoint()
            .LateMark("hidden");
        foreach (var state in ReportTemplateTestData.States)
        {
            if (state == ReportCellState.NotAssigned)
            {
                form.Mark(state, "own", "—");
            }
            else
            {
                form.Mark(state, "empty");
            }
        }

        return form;
    }

    public static void AssertSame(ReportTemplateSettings expected, ReportTemplateSettings actual)
    {
        Assert.Equal(expected.View, actual.View);
        Assert.Equal(expected.HideMaterials, actual.HideMaterials);
        Assert.Equal(expected.HoursPerLesson, actual.HoursPerLesson);
        Assert.Equal(expected.ScaleMode, actual.ScaleMode);
        Assert.Equal(expected.ScaleRows.ToList(), actual.ScaleRows.ToList());
        Assert.Equal(ReportTemplateTestData.States.Count, actual.Marks.Count);
        foreach (var state in ReportTemplateTestData.States)
        {
            Assert.Equal(expected.Marks[state], actual.Marks[state]);
        }

        Assert.Equal(expected.LateMark, actual.LateMark);
    }

    public static void AssertSame(ReportTemplateSettings expected, ReportTemplate actual) =>
        AssertSame(expected, actual.ToSettings());

    /// <summary>Exactly one audit row, of the given shape (spec FR-013).</summary>
    public static void AssertSingleAuditRow(
        ReportTemplateWorld world,
        AuditAction action,
        AuditOutcome outcome,
        long actorId,
        AppRole role,
        long? targetId,
        AuditRefusalCategory? category = null)
    {
        var row = Assert.Single(world.Audit.Written);
        Assert.Equal(action, row.Action);
        Assert.Equal(outcome, row.Outcome);
        Assert.Equal(AuditActorType.AppUser, row.ActorType);
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal(role, row.ActorRole);
        if (outcome == AuditOutcome.Succeeded)
        {
            Assert.Equal(AuditTargetType.ReportTemplate, row.TargetType);
        }

        Assert.Equal(targetId, row.TargetId);
        Assert.Equal(category, row.RefusalCategory);
    }
}
