using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.PasswordFieldMarkup;
using static ClassroomAgent.Tests.TestInfrastructure.PasswordToggleScriptRules;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-073 AC-001 to AC-007, read from the source of both hosts: the script keeps the three states of spec FR-002 by the
/// rules of FR-003 to FR-007. No test runs the script or reproduces a browser's filling, so the behaviour is proved by
/// the AC-008 walk and SECURITY_REVIEW reads the script too. The second half proves that the rules can pass and that
/// each one reports the defect it exists for.
/// </summary>
public sealed class PasswordToggleStateSourceTests
{
    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    private static string Script(string host) => Styling.ReadRequired(Styling.FileOf(host, ScriptPath));

    private static readonly Dictionary<string, Func<string, IReadOnlyList<string>>> Rules = new(StringComparer.Ordinal)
    {
        ["kinds"] = KindProblems,
        ["autofill"] = AutofillProblems,
        ["entered"] = EnteredRouteProblems,
        ["visibility"] = VisibilityProblems,
        ["beforeinput"] = BeforeInputProblems,
        ["input"] = InputProblems,
        ["press"] = ChangeAndPressProblems,
        ["load"] = LoadAndReturnProblems,
        ["scan"] = ScriptProblems,
    };

    // ---- The scripts of both hosts -------------------------------------------------------------------------------

    /// <summary>AC-001, AC-006, FR-003: entry kinds are exactly the closed list, so an undo, a drop or a replacement is never entry.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheEntryKinds_AreExactlyTheClosedList(string host) =>
        Assert.Empty(KindProblems(Script(host)));

    /// <summary>AC-003, FR-003: filling is told by <c>:autofill</c>, and a throwing call answers filled (Scope 6).</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void Filling_IsToldByAutofill_AndDoubtAnswersFilled(string host) =>
        Assert.Empty(AutofillProblems(Script(host)));

    /// <summary>AC-001, AC-003, AC-004, FR-006 step 3, SC-2: a marked entry into an empty field is the one route to <c>ENTERED</c>.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheOnlyRouteToEntered_IsAMarkedEntry(string host) =>
        Assert.Empty(EnteredRouteProblems(Script(host)));

    /// <summary>AC-001, AC-002, AC-005, FR-002, FR-007.3: the control is visible only in <c>ENTERED</c>, leaving it hides the field first, and the focus moves to the field.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheControl_IsVisibleOnlyInEntered(string host) =>
        Assert.Empty(VisibilityProblems(Script(host)));

    /// <summary>AC-004, AC-006, FR-005: <c>beforeinput</c> marks an entry into an empty field, and any other kind goes to <c>FILLED</c> before the text changes.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void BeforeInput_FollowsTheRules(string host) =>
        Assert.Empty(BeforeInputProblems(Script(host)));

    /// <summary>AC-002, AC-004, AC-005, AC-006, FR-006: <c>input</c> checks filled, then empty, then entry, and clears the mark.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void Input_ChecksInTheSpecifiedOrder(string host) =>
        Assert.Empty(InputProblems(Script(host)));

    /// <summary>AC-003, AC-005, FR-007.1, FR-007.2: <c>change</c> and a press re-check, and only a press of a field still <c>ENTERED</c> shows it.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void ChangeAndPress_ReCheck_BeforeAnythingIsShown(string host) =>
        Assert.Empty(ChangeAndPressProblems(Script(host)));

    /// <summary>AC-003, FR-004: text there at load or restored on "back" is never <c>ENTERED</c>, and the wrapper is ready whatever the state.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void LoadAndReturn_WorkOutTheStateAnew(string host) =>
        Assert.Empty(LoadAndReturnProblems(Script(host)));

    /// <summary>AC-007, FR-008.1: the value is compared with the empty string in one helper, which exists.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheValue_IsComparedInOneHelper(string host) =>
        Assert.Equal(1, ValueHelperCount(Script(host)));

    /// <summary>AC-007, FR-010: the two files stay identical apart from line endings. Green before implementation: they are identical today.</summary>
    [Fact]
    public void TheTwoScripts_AreTheSame_ApartFromLineEndings() =>
        Assert.Equal(
            Script(Styling.Installation).ReplaceLineEndings("\n"),
            Script(Styling.ControlPlane).ReplaceLineEndings("\n"));

    // ---- The rules are not vacuous ---------------------------------------------------------------------------------

    /// <summary>The rules can all pass at once. The sample below is never run or deployed.</summary>
    [Fact]
    public void AConformingScript_PassesEveryRule()
    {
        Assert.All(Rules, rule => Assert.Empty(rule.Value(ConformingScript)));
        Assert.Equal(1, ValueHelperCount(ConformingScript));
    }

    /// <summary>Each defect below is reported by the rule that exists for it.</summary>
    [Theory]
    // FR-003: the closed list.
    [InlineData("kinds", "\"insertFromPaste\"]", "\"insertFromPaste\", \"historyUndo\"]")]
    [InlineData("kinds", "\"insertFromPaste\"]", "\"insertFromPaste\", \"insertFromDrop\"]")]
    [InlineData("kinds", "\"insertFromPaste\"]", "\"insertFromPaste\", \"insertReplacementText\"]")]
    [InlineData("kinds", "\"insertFromPaste\"]", "\"insertFromPaste\", \"insertLineBreak\"]")]
    [InlineData("kinds", ", \"insertFromPaste\"]", "]")]
    [InlineData("kinds", "ENTRY_KINDS.indexOf(kind) !== -1", "kind.indexOf(\"insert\") === 0")]
    [InlineData("kinds", "kind.indexOf(\"delete\") === 0", "kind === \"deleteContentBackward\"")]
    // FR-003: in doubt.
    [InlineData("autofill", "return true;", "return false;")]
    [InlineData("autofill", ":autofill", ":-webkit-autofill")]
    // FR-006 step 3, SC-2: one route to ENTERED.
    [InlineData("entered", "field.awaitingEntry ? ENTERED : FILLED", "ENTERED")]
    [InlineData("entered", "? EMPTY : FILLED", "? EMPTY : ENTERED")]
    [InlineData("entered", "field.awaitingEntry = true;", "field.awaitingEntry = true; setState(field, ENTERED);")]
    [InlineData("entered", "field.awaitingEntry = true;", "field.awaitingEntry = true; field.state = \"ENTERED\";")]
    [InlineData("entered", "field.awaitingEntry = true;", "")]
    // FR-002, FR-007.3: visibility.
    [InlineData("visibility", "field.control.hidden = state !== ENTERED;", "field.control.hidden = false;")]
    [InlineData("visibility", "field.control.hidden = state !== ENTERED;", "field.control.hidden = state === FILLED;")]
    [InlineData("visibility", "var fields = [];", "var fields = []; document.querySelector('.password-toggle').removeAttribute('hidden');")]
    [InlineData("visibility", "if (state !== ENTERED) { hide(field); } field.state = state; field.control.hidden = state !== ENTERED;", "field.state = state; field.control.hidden = state !== ENTERED; if (state !== ENTERED) { hide(field); }")]
    [InlineData("visibility", "if (state !== ENTERED) { hide(field); }", "")]
    [InlineData("visibility", "field.input.focus();", "")]
    // FR-005: beforeinput.
    [InlineData("beforeinput", "if (!isDeletionKind(kind) && kind !== ENTER_KIND) { setState(field, FILLED); }", "")]
    [InlineData("beforeinput", "else if (field.state === EMPTY) { setState(field, FILLED); }", "")]
    [InlineData("beforeinput", "input.addEventListener(\"beforeinput\", function (event) { onBeforeInput(field, event); });", "")]
    // FR-006: input.
    [InlineData("input", "function onInput(field, event) {", "function onInput(field, event) { if (isEmpty(field.input)) { return; }")]
    [InlineData("input", "isFilled(field.input) || kind === ENTER_KIND", "kind === ENTER_KIND")]
    [InlineData("input", "isFilled(field.input) || kind === ENTER_KIND", "isFilled(field.input)")]
    [InlineData("input", "if (isEntryKind(kind) && field.state === EMPTY)", "if (isEntryKind(kind))")]
    [InlineData("input", "if (isEntryKind(kind) && field.state === EMPTY)", "if (field.state === EMPTY)")]
    [InlineData("input", "else if (isDeletionKind(kind) && field.state === EMPTY) { setState(field, FILLED); }", "")]
    [InlineData("input", "field.awaitingEntry = false;", "")]
    // FR-007: change and a press.
    [InlineData("press", "if (isFilled(field.input)) { setState(field, FILLED); return; }", "")]
    [InlineData("press", "if (isEmpty(field.input)) { setState(field, EMPTY); return; }", "")]
    [InlineData("press", "if (field.state !== ENTERED) { return; }", "")]
    [InlineData("press", "input.addEventListener(\"change\", function () { onChange(field); });", "")]
    [InlineData("press", "var fields = [];", "var fields = []; function reveal(field) { show(field); }")]
    // FR-004: load and return.
    [InlineData("load", "isEmpty(field.input) && !isFilled(field.input) ? EMPTY : FILLED", "isEmpty(field.input) ? EMPTY : FILLED")]
    [InlineData("load", "if (event.persisted) { fields.forEach(settle); }", "")]
    [InlineData("load", "settle(field);", "")]
    [InlineData("load", "wrapper.classList.add(\"password-field--ready\");", "")]
    // FR-005, FR-008: the listeners never cancel, and the value has one use.
    [InlineData("scan", "var kind = event.inputType;", "var kind = event.inputType; event.preventDefault();")]
    [InlineData("scan", "return input.value === \"\";", "return input.value.length === 0;")]
    // Logic, not words: every word the older rows look for is still there. A to L are the defects of the US-073 verification.
    [InlineData("kinds", "return ENTRY_KINDS.indexOf(kind) !== -1;", "return true;")] // A
    [InlineData("kinds", "return ENTRY_KINDS.indexOf(kind) !== -1;", "return ENTRY_KINDS.indexOf(kind) === -1;")] // B
    [InlineData("kinds", "return typeof kind === \"string\" && kind.indexOf(\"delete\") === 0;", "return kind === undefined || kind.indexOf(\"delete\") === 0;")] // K
    [InlineData("beforeinput", "if (isEmpty(field.input) && !isFilled(field.input)) {", "if (isEmpty(field.input) || !isFilled(field.input)) {")] // C
    [InlineData("beforeinput", "if (isEmpty(field.input) && !isFilled(field.input)) {", "if (isEmpty(field.input) && isFilled(field.input)) {")] // D
    [InlineData("beforeinput", "if (!isDeletionKind(kind) && kind !== ENTER_KIND)", "if (isDeletionKind(kind) && kind !== ENTER_KIND)")] // J
    [InlineData("beforeinput", "else if (field.state === EMPTY)", "else if (field.state !== EMPTY)")]
    [InlineData("input", " || !(isEntryKind(kind) || isDeletionKind(kind))", "")] // E
    [InlineData("input", "if (isFilled(field.input) || kind === ENTER_KIND", "if (!isFilled(field.input) || kind === ENTER_KIND")] // F
    [InlineData("input", "else if (isEmpty(field.input))", "else if (!isEmpty(field.input))")]
    [InlineData("input", "else if (isDeletionKind(kind) && field.state === EMPTY)", "else if (isDeletionKind(kind) && field.state !== EMPTY)")]
    [InlineData("press", "if (field.state !== ENTERED) { return; }", "if (field.state === ENTERED) { return; }")] // G
    [InlineData("press", "if (isFilled(field.input)) { setState(field, FILLED); return; }", "if (!isFilled(field.input)) { setState(field, FILLED); return; }")] // H
    [InlineData("press", "if (isFilled(field.input)) { setState(field, FILLED); }", "if (!isFilled(field.input)) { setState(field, FILLED); }")] // I
    [InlineData("press", "if (isEmpty(field.input)) { setState(field, EMPTY); return; }", "if (!isEmpty(field.input)) { setState(field, EMPTY); return; }")]
    [InlineData("press", "if (field.input.type === \"password\") {", "if (field.input.type === \"text\") {")]
    [InlineData("visibility", "if (state !== ENTERED) { hide(field); }", "if (state === ENTERED) { hide(field); }")] // L
    [InlineData("visibility", "field.input.type = \"password\";", "")]
    [InlineData("visibility", "if (hadFocus && state !== ENTERED) {", "if (hadFocus && state === ENTERED) {")]
    [InlineData("entered", "FILLED = \"FILLED\"", "FILLED = \"ENTERED\"")]
    [InlineData("entered", "state: EMPTY, awaitingEntry: false", "state: EMPTY, awaitingEntry: true")]
    [InlineData("load", "if (event.persisted)", "if (!event.persisted)")]
    public void ADefect_IsReportedByItsRule(string rule, string conforming, string defective)
    {
        Assert.Contains(conforming, ConformingScript, StringComparison.Ordinal);
        Assert.NotEmpty(Rules[rule](ConformingScript.Replace(conforming, defective, StringComparison.Ordinal)));
    }

    /// <summary>Test data for the rules above: the source form they read, not a program anything runs.</summary>
    private const string ConformingScript = """
        (function () {
            "use strict";

            var EMPTY = "EMPTY", ENTERED = "ENTERED", FILLED = "FILLED";
            var ENTRY_KINDS = ["insertText", "insertCompositionText", "insertFromComposition", "insertFromPaste"];
            var ENTER_KIND = "insertLineBreak";
            var fields = [];

            function isEmpty(input) {
                return input.value === "";
            }

            function isFilled(input) {
                try { return input.matches(":autofill"); } catch (e) { return true; }
            }

            function isEntryKind(kind) {
                return ENTRY_KINDS.indexOf(kind) !== -1;
            }

            function isDeletionKind(kind) {
                return typeof kind === "string" && kind.indexOf("delete") === 0;
            }

            function show(field) {
                field.input.type = "text";
                field.control.setAttribute("aria-pressed", "true");
                field.control.setAttribute("aria-label", field.control.dataset.hideName);
            }

            function hide(field) {
                field.input.type = "password";
                field.control.setAttribute("aria-pressed", "false");
                field.control.setAttribute("aria-label", field.control.dataset.showName);
            }

            function hideAll() {
                fields.forEach(hide);
            }

            function setState(field, state) {
                var hadFocus = document.activeElement === field.control;
                if (state !== ENTERED) { hide(field); } field.state = state; field.control.hidden = state !== ENTERED;
                if (hadFocus && state !== ENTERED) {
                    field.input.focus();
                }
            }

            function settle(field) {
                setState(field, isEmpty(field.input) && !isFilled(field.input) ? EMPTY : FILLED);
            }

            function onBeforeInput(field, event) {
                var kind = event.inputType;
                if (isEntryKind(kind)) {
                    if (isEmpty(field.input) && !isFilled(field.input)) {
                        setState(field, EMPTY);
                        field.awaitingEntry = true;
                    } else if (field.state === EMPTY) { setState(field, FILLED); }
                    return;
                }
                if (!isDeletionKind(kind) && kind !== ENTER_KIND) { setState(field, FILLED); }
            }

            function onInput(field, event) {
                var kind = event.inputType;
                if (isFilled(field.input) || kind === ENTER_KIND || !(isEntryKind(kind) || isDeletionKind(kind))) {
                    setState(field, FILLED);
                } else if (isEmpty(field.input)) {
                    setState(field, EMPTY);
                } else if (isEntryKind(kind) && field.state === EMPTY) {
                    setState(field, field.awaitingEntry ? ENTERED : FILLED);
                } else if (isDeletionKind(kind) && field.state === EMPTY) { setState(field, FILLED); }
                field.awaitingEntry = false;
            }

            function onChange(field) {
                if (isFilled(field.input)) { setState(field, FILLED); }
            }

            function onPress(field) {
                if (isFilled(field.input)) { setState(field, FILLED); return; }
                if (isEmpty(field.input)) { setState(field, EMPTY); return; }
                if (field.state !== ENTERED) { return; }
                if (field.input.type === "password") {
                    show(field);
                } else {
                    hide(field);
                }
            }

            document.querySelectorAll(".password-field").forEach(function (wrapper) {
                var input = wrapper.querySelector("input");
                var control = wrapper.querySelector(".password-toggle");
                if (input === null || control === null) {
                    return;
                }
                var field = { input: input, control: control, state: EMPTY, awaitingEntry: false };
                fields.push(field);
                input.addEventListener("beforeinput", function (event) { onBeforeInput(field, event); });
                input.addEventListener("input", function (event) { onInput(field, event); });
                input.addEventListener("change", function () { onChange(field); });
                control.addEventListener("click", function () { onPress(field); });
                wrapper.classList.add("password-field--ready");
                settle(field);
            });

            document.addEventListener("submit", hideAll);
            window.addEventListener("pagehide", hideAll);
            window.addEventListener("pageshow", function (event) {
                if (event.persisted) { fields.forEach(settle); }
            });
        })();
        """;
}
