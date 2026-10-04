using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec FR-006, FR-015, VR-001 .. VR-005, AC-001, AC-011: what <see cref="ReportTemplate"/> refuses, what it
/// stores, the built-in settings and the 12-point preset, and that no use case of this Story is given a Google port.
/// </summary>
public sealed class ReportTemplateInvariantTests
{
    private static ReportTemplateSettings Valid() => ReportTemplateTestData.Settings();

    private static ReportTemplateSettings WithMark(ReportCellState state, ReportMark mark)
    {
        var settings = Valid();
        var marks = settings.Marks.ToDictionary(p => p.Key, p => p.Value);
        marks[state] = mark;
        return settings with { Marks = marks };
    }

    private static void AssertRefused(Func<ReportTemplate> create) => Assert.ThrowsAny<ArgumentException>(create);

    /// <summary>FR-006, AC-001: the built-in template holds exactly the settings the specification lists.</summary>
    [Fact]
    public void TheAcademicJournal_HasTheSettingsOfFr006()
    {
        var settings = BuiltInReportTemplates.AcademicJournal;

        Assert.Equal(ReportView.Short, settings.View);
        Assert.True(settings.HideMaterials);
        Assert.Equal(2, settings.HoursPerLesson);
        Assert.Equal(ReportScaleMode.Ranges, settings.ScaleMode);
        ReportTemplateExpectations.AssertSame(ReportTemplateExpectations.BuiltIn(), settings);
        Assert.Equal(new ReportMark(ReportMarkKind.Own, "—"), settings.Marks[ReportCellState.NotAssigned]);
        Assert.Null(settings.LateMark.Text);
    }

    /// <summary>FR-015, OD-002: the 12-point preset.</summary>
    [Fact]
    public void TheTwelvePointPreset_HasTheTwelveRowsOfOd002()
    {
        var expected = ReportTemplateTestData.TwelvePoint.Select(r => new ReportScaleRow(r.From, r.To, r.Label)).ToList();

        Assert.Equal(expected, TwelvePointScale.Rows.ToList());
    }

    /// <summary>VR-001: the name is trimmed, its normalized form is upper-case, the author is stored.</summary>
    [Fact]
    public void Create_TrimsTheName_AndStoresTheAuthor()
    {
        var template = ReportTemplate.Create("  Ab  ", Valid(), 7);

        Assert.Equal("Ab", template.Name);
        Assert.Equal("AB", template.NormalizedName);
        Assert.Equal(7, template.AuthorId);
    }

    /// <summary>VR-001: the normalization used for uniqueness ignores case and surrounding spaces.</summary>
    [Fact]
    public void Normalize_TrimsAndIgnoresCase() => Assert.Equal("AB", ReportTemplate.Normalize(" Ab "));

    /// <summary>FR-003: a created template always holds exactly nine marks, one per state, and its scale rows in order.</summary>
    [Fact]
    public void Create_StoresNineMarks_AndTheScaleRowsOrderedByFrom()
    {
        var scrambled = ReportTemplateTestData.TwelvePoint.Reverse().ToList();

        var template = ReportTemplate.Create("T", ReportTemplateTestData.Settings(scale: scrambled), 7);

        Assert.Equal(9, template.Marks.Count);
        Assert.Equal(ReportTemplateTestData.States.OrderBy(s => s), template.Marks.Select(m => m.State).OrderBy(s => s));
        Assert.Equal(ReportScaleMode.Ranges, template.ScaleMode);
        Assert.Equal(
            ReportTemplateTestData.TwelvePoint.Select(r => r.From),
            template.ScaleRows.Select(r => r.FromPercent));
    }

    /// <summary>The settings round-trip through the entity, including own marks, the late mark and the scale.</summary>
    [Fact]
    public void ToSettings_ReturnsWhatWasStored()
    {
        var settings = ReportTemplateTestData.Settings(
            ReportView.Short,
            true,
            5,
            ReportTemplateTestData.TwelvePoint,
            new Dictionary<ReportCellState, ReportMark>
            {
                [ReportCellState.TurnedIn] = new(ReportMarkKind.Own, "ok"),
                [ReportCellState.Returned] = new(ReportMarkKind.Empty, null),
            },
            new ReportLateMark(ReportLateMarkKind.Own, "late"));

        var template = ReportTemplate.Create("T", settings, 7);

        ReportTemplateExpectations.AssertSame(settings, template);
    }

    /// <summary>VR-001, AC-002: a change replaces name and settings and keeps the author.</summary>
    [Fact]
    public void Change_ReplacesTheNameAndSettings_AndKeepsTheAuthor()
    {
        var template = ReportTemplate.Create("Old", Valid(), 7);
        var changed = ReportTemplateTestData.Settings(ReportView.Short, true, 4, ReportTemplateTestData.TwoRow);

        template.Change("  New  ", changed);

        Assert.Equal("New", template.Name);
        Assert.Equal("NEW", template.NormalizedName);
        Assert.Equal(7, template.AuthorId);
        ReportTemplateExpectations.AssertSame(changed, template);
    }

    /// <summary>VR-001 .. VR-005: the boundary values that are still valid.</summary>
    [Fact]
    public void TheBoundaryValues_AreAccepted()
    {
        var rows = Enumerable.Range(0, 101).Select(i => (i, i, "x")).ToList();
        var settings = ReportTemplateTestData.Settings(hours: 10, scale: rows) with
        {
            Marks = ReportTemplateTestData.States.ToDictionary(
                s => s,
                _ => new ReportMark(ReportMarkKind.Own, new string('m', 30))),
        };

        var template = ReportTemplate.Create(new string('n', 100), settings, 1);

        Assert.Equal(100, template.Name.Length);
        Assert.Equal(10, template.HoursPerLesson);
        Assert.Equal(101, template.ScaleRows.Count);
        Assert.Equal(1, ReportTemplate.Create("T", ReportTemplateTestData.Settings(hours: 1), 1).HoursPerLesson);
        Assert.Equal(
            10,
            ReportTemplate.Create(
                "T",
                ReportTemplateTestData.Settings(scale: [(0, 100, new string('x', 10))]),
                1).ScaleRows.Single().Label.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RefusesAnEmptyName(string name) => AssertRefused(() => ReportTemplate.Create(name, Valid(), 1));

    [Fact]
    public void Create_RefusesANameOver100Characters() =>
        AssertRefused(() => ReportTemplate.Create(new string('n', 101), Valid(), 1));

    [Fact]
    public void Change_RefusesAnEmptyName_AndAnOverlongName()
    {
        var template = ReportTemplate.Create("T", Valid(), 1);

        Assert.ThrowsAny<ArgumentException>(() => template.Change(" ", Valid()));
        Assert.ThrowsAny<ArgumentException>(() => template.Change(new string('n', 101), Valid()));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Create_RefusesANonPositiveAuthor(long authorId) =>
        AssertRefused(() => ReportTemplate.Create("T", Valid(), authorId));

    [Fact]
    public void Create_RefusesFewerThanNineMarks()
    {
        var settings = Valid();
        var eight = settings.Marks.Take(8).ToDictionary(p => p.Key, p => p.Value);

        AssertRefused(() => ReportTemplate.Create("T", settings with { Marks = eight }, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_RefusesAnOwnMarkWithoutText(string? text) =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            WithMark(ReportCellState.NotAssigned, new ReportMark(ReportMarkKind.Own, text)),
            1));

    [Fact]
    public void Create_RefusesTextOnAMarkThatIsNotOwn()
    {
        AssertRefused(() => ReportTemplate.Create(
            "T",
            WithMark(ReportCellState.NotAssigned, new ReportMark(ReportMarkKind.Program, "x")),
            1));
        AssertRefused(() => ReportTemplate.Create(
            "T",
            WithMark(ReportCellState.NotAssigned, new ReportMark(ReportMarkKind.Empty, "x")),
            1));
        AssertRefused(() => ReportTemplate.Create(
            "T",
            Valid() with { LateMark = new ReportLateMark(ReportLateMarkKind.Hidden, "x") },
            1));
    }

    [Fact]
    public void Create_RefusesAnOwnLateMarkWithoutText() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            Valid() with { LateMark = new ReportLateMark(ReportLateMarkKind.Own, null) },
            1));

    [Fact]
    public void Create_RefusesAMarkTextOver30Characters() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            WithMark(ReportCellState.NotAssigned, new ReportMark(ReportMarkKind.Own, new string('m', 31))),
            1));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_RefusesHoursOutsideOneToTen(int hours) =>
        AssertRefused(() => ReportTemplate.Create("T", ReportTemplateTestData.Settings(hours: hours), 1));

    [Fact]
    public void Create_RefusesNoConversionWithRows() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            Valid() with { ScaleRows = [new ReportScaleRow(0, 100, "x")] },
            1));

    [Fact]
    public void Create_RefusesRangesWithoutRows() =>
        AssertRefused(() => ReportTemplate.Create("T", Valid() with { ScaleMode = ReportScaleMode.Ranges }, 1));

    [Fact]
    public void Create_RefusesAScaleWithAGap() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            ReportTemplateTestData.Settings(scale: [(0, 40, "a"), (50, 100, "b")]),
            1));

    [Fact]
    public void Create_RefusesAScaleWithAnOverlap() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            ReportTemplateTestData.Settings(scale: [(0, 50, "a"), (50, 100, "b")]),
            1));

    [Fact]
    public void Create_RefusesAScaleThatDoesNotSpanZeroToHundred()
    {
        AssertRefused(() => ReportTemplate.Create(
            "T",
            ReportTemplateTestData.Settings(scale: [(5, 100, "a")]),
            1));
        AssertRefused(() => ReportTemplate.Create(
            "T",
            ReportTemplateTestData.Settings(scale: [(0, 99, "a")]),
            1));
    }

    [Fact]
    public void Create_RefusesAnEmptyScaleLabel() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            ReportTemplateTestData.Settings(scale: [(0, 100, "")]),
            1));

    [Fact]
    public void Create_RefusesAScaleLabelOver10Characters() =>
        AssertRefused(() => ReportTemplate.Create(
            "T",
            ReportTemplateTestData.Settings(scale: [(0, 100, new string('x', 11))]),
            1));

    /// <summary>
    /// AC-011, SC-13: viewing and editing templates and the report read only the installation's own database; no
    /// constructor of this Story's use cases takes a Google port, a secret store or the Control Plane client.
    /// </summary>
    [Fact]
    public void NoUseCaseOfThisStory_TakesAGooglePort()
    {
        Type[] forbidden =
        [
            typeof(IClassroomReader),
            typeof(IGoogleDataPort),
            typeof(IGoogleAccessProbe),
            typeof(ISecretStore),
            typeof(IControlPlaneClient),
        ];
        Type[] useCases =
        [
            typeof(ListReportTemplatesQuery),
            typeof(GetReportTemplateFormQuery),
            typeof(SaveReportTemplateUseCase),
            typeof(DeleteReportTemplateUseCase),
            typeof(GetReportQuery),
        ];

        foreach (var useCase in useCases)
        {
            foreach (var constructor in useCase.GetConstructors())
            {
                Assert.DoesNotContain(
                    constructor.GetParameters().Select(p => p.ParameterType),
                    type => forbidden.Contains(type));
            }
        }
    }
}
