using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec VR-001 .. VR-005, AC-003, AC-009, api-design §2.5 .. §2.7: every field error key with its field name
/// and row number, the boundaries, the tampered-form cases, and that a refused form writes nothing and hands the
/// entered values back. Driven through <c>SaveReportTemplateUseCase.CreateAsync</c> with in-memory ports.
/// </summary>
public sealed class ReportTemplateFormValidationTests
{
    private static string Long(int length) => new('x', length);

    private static async Task<(ReportTemplateWorld World, ReportTemplateSaveResult Result)> SubmitAsync(
        ReportTemplateFormBuilder form,
        Action<ReportTemplateWorld>? seed = null)
    {
        var world = new ReportTemplateWorld();
        seed?.Invoke(world);
        var result = await world.Save.CreateAsync(
            ReportTemplateWorld.DeanId,
            AppRole.Dean,
            form.Input(),
            "r-1",
            TestContext.Current.CancellationToken);
        return (world, result);
    }

    private static void AssertRefusedField(
        ReportTemplateWorld world,
        ReportTemplateSaveResult result,
        string field,
        ReportTemplateFieldErrorKey key,
        int? rowNumber,
        string enteredName)
    {
        Assert.Equal(ReportTemplateOutcome.FieldsInvalid, result.Outcome);
        Assert.Null(result.TemplateId);
        var form = Assert.IsType<ReportTemplateFormPageModel>(result.Form);
        Assert.Contains(new ReportTemplateFieldError(field, key, rowNumber), form.FieldErrors);
        Assert.Equal(ReportTemplateFormMode.Create, form.Mode);
        Assert.Equal(enteredName, form.Values.Name);
        Assert.Empty(world.Templates.Added);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }

    private static async Task AssertFieldErrorAsync(
        ReportTemplateFormBuilder form,
        string field,
        ReportTemplateFieldErrorKey key,
        int? rowNumber = null,
        string enteredName = "Test Template One",
        Action<ReportTemplateWorld>? seed = null)
    {
        var (world, result) = await SubmitAsync(form, seed);
        AssertRefusedField(world, result, field, key, rowNumber, enteredName);
    }

    private static async Task AssertSucceedsAsync(ReportTemplateFormBuilder form)
    {
        var (world, result) = await SubmitAsync(form);
        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Single(world.Templates.Added);
    }

    public static TheoryData<string, ReportTemplateFieldErrorKey> BadNames => new()
    {
        { "", ReportTemplateFieldErrorKey.NameRequired },
        { "   ", ReportTemplateFieldErrorKey.NameRequired },
        { Long(101), ReportTemplateFieldErrorKey.NameTooLong },
        { "  " + Long(101) + "  ", ReportTemplateFieldErrorKey.NameTooLong },
        { "\u0007", ReportTemplateFieldErrorKey.TextInvalidCharacters },
        { "a\u0001b", ReportTemplateFieldErrorKey.TextInvalidCharacters },
    };

    /// <summary>VR-001: required, at most 100 after trimming, no control characters; the trimmed value is handed back.</summary>
    [Theory]
    [MemberData(nameof(BadNames))]
    public Task TheName_IsValidated(string name, ReportTemplateFieldErrorKey key) =>
        AssertFieldErrorAsync(ReportTemplateFormBuilder.Valid(name), "name", key, enteredName: name.Trim());

    /// <summary>VR-001: exactly 100 characters is fine, also with surrounding spaces.</summary>
    [Theory]
    [InlineData(100, "")]
    [InlineData(100, "  ")]
    [InlineData(1, "")]
    public async Task TheNameBoundaries_AreAccepted(int length, string padding)
    {
        var (world, result) = await SubmitAsync(ReportTemplateFormBuilder.Valid(padding + Long(length) + padding));

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        Assert.Equal(Long(length), world.Templates.Added.Single().Name);
    }

    /// <summary>VR-001: equal to a created template's name, ignoring case and surrounding spaces, is refused.</summary>
    [Theory]
    [InlineData("Test Template One")]
    [InlineData(" test template ONE ")]
    [InlineData("TEST TEMPLATE ONE")]
    public Task ADuplicateName_IsRefused_IgnoringCaseAndSpaces(string name) =>
        AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid(name),
            "name",
            ReportTemplateFieldErrorKey.NameNotUnique,
            enteredName: name.Trim(),
            seed: world => world.SeedTemplate("Test Template One"));

    /// <summary>VR-001, FR-006: the built-in's name is not reserved.</summary>
    [Fact]
    public Task TheBuiltInsName_MayBeUsed() =>
        AssertSucceedsAsync(ReportTemplateFormBuilder.Valid("Академічний журнал"));

    public static TheoryData<string, string, ReportTemplateFieldErrorKey?> MarkTexts => new()
    {
        { "marks[NotAssigned]", "", ReportTemplateFieldErrorKey.MarkTextRequired },
        { "marks[NotAssigned]", Long(31), ReportTemplateFieldErrorKey.MarkTextTooLong },
        { "marks[NotAssigned]", "\u0007", ReportTemplateFieldErrorKey.TextInvalidCharacters },
        { "marks[NotAssigned]", Long(30), null },
        { "marks[NotAssigned]", "x", null },
        { "lateMark", "", ReportTemplateFieldErrorKey.MarkTextRequired },
        { "lateMark", Long(31), ReportTemplateFieldErrorKey.MarkTextTooLong },
        { "lateMark", "\u0007", ReportTemplateFieldErrorKey.TextInvalidCharacters },
        { "lateMark", Long(30), null },
    };

    /// <summary>VR-003: own text is required, 1 to 30 characters, no control characters; the field is the text input.</summary>
    [Theory]
    [MemberData(nameof(MarkTexts))]
    public async Task MarkTexts_AreValidated(string target, string text, ReportTemplateFieldErrorKey? key)
    {
        var form = ReportTemplateFormBuilder.Valid();
        if (target == "lateMark")
        {
            form.LateMark("own", text);
        }
        else
        {
            form.Mark(ReportCellState.NotAssigned, "own", text);
        }

        if (key is null)
        {
            await AssertSucceedsAsync(form);
        }
        else
        {
            await AssertFieldErrorAsync(form, target + ".text", key.Value);
        }
    }

    /// <summary>VR-003: text sent with another kind is ignored, even when it would be invalid, and is not stored.</summary>
    [Fact]
    public async Task TextSentWithAnotherKind_IsIgnored_AndNotStored()
    {
        var form = ReportTemplateFormBuilder.Valid()
            .Mark(ReportCellState.NotAssigned, "program", Long(31))
            .Mark(ReportCellState.TurnedIn, "empty", "\u0007")
            .LateMark("hidden", "ignored");

        var (world, result) = await SubmitAsync(form);

        Assert.Equal(ReportTemplateOutcome.Succeeded, result.Outcome);
        var settings = world.Templates.Added.Single().ToSettings();
        Assert.Null(settings.Marks[ReportCellState.NotAssigned].Text);
        Assert.Null(settings.Marks[ReportCellState.TurnedIn].Text);
        Assert.Null(settings.LateMark.Text);
    }

    /// <summary>VR-005: a whole number 1 to 10; empty is "required", anything else "out of range".</summary>
    [Theory]
    [InlineData("1", null)]
    [InlineData("10", null)]
    [InlineData("0", ReportTemplateFieldErrorKey.HoursOutOfRange)]
    [InlineData("11", ReportTemplateFieldErrorKey.HoursOutOfRange)]
    [InlineData("2.5", ReportTemplateFieldErrorKey.HoursOutOfRange)]
    [InlineData("abc", ReportTemplateFieldErrorKey.HoursOutOfRange)]
    [InlineData("", ReportTemplateFieldErrorKey.HoursRequired)]
    public async Task Hours_AreValidated(string hours, ReportTemplateFieldErrorKey? key)
    {
        var form = ReportTemplateFormBuilder.Valid().Set("hoursPerLesson", hours);

        if (key is null)
        {
            await AssertSucceedsAsync(form);
        }
        else
        {
            await AssertFieldErrorAsync(form, "hoursPerLesson", key.Value);
        }
    }

    /// <summary>VR-004: ranges with no row at all.</summary>
    [Fact]
    public Task ARangesTable_WithoutRows_IsRefused() =>
        AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Ranges(Array.Empty<(string, string, string)>()),
            "scale",
            ReportTemplateFieldErrorKey.ScaleNoRows);

    /// <summary>VR-004: more than 101 rows.</summary>
    [Fact]
    public Task ARangesTable_WithTooManyRows_IsRefused() =>
        AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Ranges(
                Enumerable.Range(0, 102).Select(i => (Math.Min(i, 100), Math.Min(i, 100), "x")).ToList()),
            "scale",
            ReportTemplateFieldErrorKey.ScaleTooManyRows);

    /// <summary>VR-004: one row covering everything, and 101 single-point rows, are valid.</summary>
    [Fact]
    public async Task OneRowCoveringEverything_AndOneHundredAndOneRows_AreValid()
    {
        await AssertSucceedsAsync(ReportTemplateFormBuilder.Valid().Ranges([(0, 100, "all")]));
        await AssertSucceedsAsync(
            ReportTemplateFormBuilder.Valid().Ranges(Enumerable.Range(0, 101).Select(i => (i, i, "x")).ToList()));
    }

    public static TheoryData<string, string, string, ReportTemplateFieldErrorKey, string> BadRows => new()
    {
        { "", "100", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "from" },
        { "abc", "100", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "from" },
        { "-1", "100", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "from" },
        { "101", "100", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "from" },
        { "1.5", "100", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "from" },
        { "50", "", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "to" },
        { "50", "x", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "to" },
        { "50", "101", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "to" },
        { "50", "-1", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "to" },
        { "50", "99.5", "b", ReportTemplateFieldErrorKey.ScaleBoundInvalid, "to" },
        { "60", "50", "b", ReportTemplateFieldErrorKey.ScaleRowInverted, "from" },
        { "50", "100", "", ReportTemplateFieldErrorKey.ScaleLabelRequired, "label" },
        { "50", "100", "xxxxxxxxxxx", ReportTemplateFieldErrorKey.ScaleLabelTooLong, "label" },
        { "50", "100", "\u0007", ReportTemplateFieldErrorKey.TextInvalidCharacters, "label" },
    };

    /// <summary>VR-004: the second submitted row (index 1) is the faulty one; the error names it by number and field.</summary>
    [Theory]
    [MemberData(nameof(BadRows))]
    public Task ScaleRows_AreValidated(
        string from,
        string to,
        string label,
        ReportTemplateFieldErrorKey key,
        string fieldSuffix) =>
        AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Ranges([("0", "49", "a"), (from, to, label)]),
            $"scale[1].{fieldSuffix}",
            key,
            rowNumber: 2);

    /// <summary>VR-004: a label of 10 characters is the longest allowed; labels may repeat.</summary>
    [Fact]
    public Task TheLabelBoundary_AndRepeatedLabels_AreAccepted() =>
        AssertSucceedsAsync(
            ReportTemplateFormBuilder.Valid().Ranges([(0, 49, "same"), (50, 100, "same")]));

    public static TheoryData<string[], int> OverlappingScales => new()
    {
        { ["0-50-a", "50-100-b"], 2 },
        { ["0-60-a", "40-100-b"], 2 },
        { ["40-100-b", "0-60-a"], 1 },
    };

    /// <summary>VR-004, AC-003: an overlap names the row that starts too early, by its submitted number.</summary>
    [Theory]
    [MemberData(nameof(OverlappingScales))]
    public Task AnOverlappingScale_IsRefused_NamingTheRow(string[] rows, int rowNumber) =>
        AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Ranges(Parse(rows)),
            "scale",
            ReportTemplateFieldErrorKey.ScaleOverlap,
            rowNumber);

    public static TheoryData<string[], int> ScalesWithAGap => new()
    {
        { ["0-40-a", "50-100-b"], 2 },
        { ["50-100-b", "0-40-a"], 1 },
        { ["5-100-a"], 1 },
        { ["10-50-a", "51-100-b"], 1 },
        { ["51-100-b", "10-50-a"], 2 },
        { ["0-50-a", "51-99-b"], 2 },
        { ["51-99-b", "0-50-a"], 1 },
        { ["0-99-a"], 1 },
    };

    /// <summary>
    /// VR-004, AC-003: an inner gap names the row after it; a table not starting at 0 names the row with the lowest
    /// <c>from</c>; one not ending at 100 names the row with the highest <c>from</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ScalesWithAGap))]
    public Task AScaleWithAGap_IsRefused_NamingTheRow(string[] rows, int rowNumber) =>
        AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Ranges(Parse(rows)),
            "scale",
            ReportTemplateFieldErrorKey.ScaleGap,
            rowNumber);

    private static IEnumerable<(string, string, string)> Parse(IEnumerable<string> rows) =>
        rows.Select(r => r.Split('-')).Select(p => (p[0], p[1], p[2])).ToList();

    /// <summary>VR-004: with no conversion any rows are ignored and nothing about them is validated.</summary>
    [Fact]
    public async Task NoConversion_ValidatesNothingAboutScaleRows()
    {
        var form = ReportTemplateFormBuilder.Valid()
            .Ranges([("abc", "-5", ""), ("60", "50", Long(11))])
            .Set("scaleMode", "none");

        await AssertSucceedsAsync(form);
    }

    /// <summary>VR-001, VR-003, VR-004: a control character is refused in every text input.</summary>
    [Theory]
    [InlineData("\u0007")]
    [InlineData("\u0001")]
    public async Task ControlCharacters_AreRefusedInEveryText(string control)
    {
        await AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid(control),
            "name",
            ReportTemplateFieldErrorKey.TextInvalidCharacters,
            enteredName: control);
        await AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Mark(ReportCellState.Returned, "own", control),
            "marks[Returned].text",
            ReportTemplateFieldErrorKey.TextInvalidCharacters);
        await AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().LateMark("own", control),
            "lateMark.text",
            ReportTemplateFieldErrorKey.TextInvalidCharacters);
        await AssertFieldErrorAsync(
            ReportTemplateFormBuilder.Valid().Ranges([("0", "100", control)]),
            "scale[0].label",
            ReportTemplateFieldErrorKey.TextInvalidCharacters,
            rowNumber: 1);
    }

    /// <summary>api-design §2.5: every problem is listed, not only the first.</summary>
    [Fact]
    public async Task EveryProblem_IsListed()
    {
        var (_, result) = await SubmitAsync(
            ReportTemplateFormBuilder.Valid("").Set("hoursPerLesson", "0").LateMark("own", ""));

        var errors = Assert.IsType<ReportTemplateFormPageModel>(result.Form).FieldErrors;
        Assert.Contains(new ReportTemplateFieldError("name", ReportTemplateFieldErrorKey.NameRequired, null), errors);
        Assert.Contains(new ReportTemplateFieldError("hoursPerLesson", ReportTemplateFieldErrorKey.HoursOutOfRange, null), errors);
        Assert.Contains(new ReportTemplateFieldError("lateMark.text", ReportTemplateFieldErrorKey.MarkTextRequired, null), errors);
    }

    /// <summary>api-design §2.5: a refused form hands back every entered value so the browser can re-render it.</summary>
    [Fact]
    public async Task ARefusedForm_HandsBackTheEnteredValues()
    {
        var form = ReportTemplateFormBuilder.Valid("  Mine  ")
            .Set("view", "short")
            .Set("hideMaterials", "true")
            .Set("hoursPerLesson", "11")
            .Mark(ReportCellState.NotAssigned, "own", "—")
            .LateMark("hidden")
            .Ranges([("0", "49", "low"), ("50", "100", "high")]);

        var (_, result) = await SubmitAsync(form);

        var values = Assert.IsType<ReportTemplateFormPageModel>(result.Form).Values;
        Assert.Equal("Mine", values.Name);
        Assert.Equal("short", values.View);
        Assert.Equal("true", values.HideMaterials);
        Assert.Equal("ranges", values.ScaleMode);
        Assert.Equal("11", values.HoursPerLesson);
        Assert.Equal(
            [new ReportTemplateScaleRowValues("0", "49", "low"), new ReportTemplateScaleRowValues("50", "100", "high")],
            values.Scale);
        Assert.Equal(new ReportTemplateMarkValues("own", "—"), values.Marks[ReportCellState.NotAssigned]);
        Assert.Equal("hidden", values.LateMark.Kind);
    }

    /// <summary>api-design §2.7: unknown field names such as the antiforgery token are ignored.</summary>
    [Fact]
    public Task UnknownFields_AreIgnored() =>
        AssertSucceedsAsync(ReportTemplateFormBuilder.Valid().Set("__RequestVerificationToken", "t"));

    private static ReportTemplateFormBuilder Tamper(string id)
    {
        var form = ReportTemplateFormBuilder.Valid();
        switch (id)
        {
            case "view-bad": return form.Set("view", "Full");
            case "view-missing": return form.Remove("view");
            case "hide-bad": return form.Set("hideMaterials", "yes");
            case "hide-missing": return form.Remove("hideMaterials");
            case "scale-bad": return form.Set("scaleMode", "range");
            case "scale-missing": return form.Remove("scaleMode");
            case "kind-bad": return form.Set("marks[NotAssigned].kind", "other");
            case "kind-hidden-on-a-state": return form.Set("marks[NotAssigned].kind", "hidden");
            case "kind-missing": return form.Remove("marks[NotAssigned].kind");
            case "late-kind-bad": return form.Set("lateMark.kind", "empty");
            case "late-kind-missing": return form.Remove("lateMark.kind");
            case "mark-key-unknown": return form.Set("marks[Bogus].kind", "program");
            case "member-missing": return form.Remove("marks[TurnedIn].kind").Remove("marks[TurnedIn].text");
            case "name-repeated": return form.Repeat("name", "Other");
            case "view-repeated": return form.Repeat("view", "full");
            case "hours-repeated": return form.Repeat("hoursPerLesson", "2");
            case "mark-repeated": return form.Repeat("marks[TurnedIn].kind", "program");
            case "late-repeated": return form.Repeat("lateMark.kind", "program");
            case "index-negative": return form.Ranges([("0", "100", "a")]).Repeat("scale[-1].from", "0");
            case "index-not-decimal": return form.Ranges([("0", "100", "a")]).Repeat("scale[a].from", "0");
            case "index-repeated": return form.Ranges([("0", "100", "a")]).Repeat("scale[0].from", "0");
            default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
        }
    }

    /// <summary>VR-003, api-design §2.5: a form no browser sends is malformed, not a field error.</summary>
    [Theory]
    [InlineData("view-bad")]
    [InlineData("view-missing")]
    [InlineData("hide-bad")]
    [InlineData("hide-missing")]
    [InlineData("scale-bad")]
    [InlineData("scale-missing")]
    [InlineData("kind-bad")]
    [InlineData("kind-hidden-on-a-state")]
    [InlineData("kind-missing")]
    [InlineData("late-kind-bad")]
    [InlineData("late-kind-missing")]
    [InlineData("mark-key-unknown")]
    [InlineData("member-missing")]
    [InlineData("name-repeated")]
    [InlineData("view-repeated")]
    [InlineData("hours-repeated")]
    [InlineData("mark-repeated")]
    [InlineData("late-repeated")]
    [InlineData("index-negative")]
    [InlineData("index-not-decimal")]
    [InlineData("index-repeated")]
    public async Task ATamperedForm_IsMalformed(string tampering)
    {
        var (world, result) = await SubmitAsync(Tamper(tampering));

        Assert.Equal(ReportTemplateOutcome.FormMalformed, result.Outcome);
        Assert.Null(result.Form);
        Assert.Null(result.TemplateId);
        Assert.Empty(world.Templates.Added);
        Assert.Equal(0, world.Work.Commits);
        Assert.Empty(world.Audit.Written);
    }
}
