using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-073 test support: reads the rules of spec FR-002 to FR-007 out of the source of <c>password-toggle.js</c>. The
/// project has no tool that runs a script, so each method returns what is wrong with the source, empty when it complies;
/// the behaviour itself is proved by the AC-008 walk and read by SECURITY_REVIEW. The source form these rules read is
/// listed in <c>docs/tests/US-073-test-strategy.md</c>.
/// <para>
/// Words in the right order do not fix logic: a negation or <c>||</c> for <c>&amp;&amp;</c> keeps every word. So each function
/// that decides something is fixed whole below, spaces aside. Changing one on purpose means changing its text here in the
/// same step and walking AC-008 again, because the walk proved this text and nothing runs it.
/// </para>
/// </summary>
public static class PasswordToggleScriptRules
{
    /// <summary>FR-003: the closed list of entry kinds.</summary>
    public static readonly IReadOnlyList<string> EntryKinds =
        ["insertText", "insertCompositionText", "insertFromComposition", "insertFromPaste"];

    /// <summary>FR-003: the Enter kind, which is not an entry kind.</summary>
    public const string EnterKind = "insertLineBreak";

    private const string FilledCheck = "isFilled(";
    private const string EmptyCheck = "isEmpty(";
    private const string SetFilled = "setState(field, FILLED)";
    private const string SetEmpty = "setState(field, EMPTY)";
    private const string EnteredRoute = @"\bfield\.awaitingEntry\s*\?\s*ENTERED\s*:\s*FILLED\b";

    /// <summary>FR-002, the states' values: a state named as another would change every comparison.</summary>
    private const string StatesDeclaration = """var EMPTY = "EMPTY", ENTERED = "ENTERED", FILLED = "FILLED";""";

    /// <summary>FR-002: a field starts unmarked, so no entry can be assumed before a <c>beforeinput</c>.</summary>
    private const string FieldDeclaration = "var field = { input: input, control: control, state: EMPTY, awaitingEntry: false };";

    private const string EntryKindsDeclaration = """var ENTRY_KINDS = ["insertText", "insertCompositionText", "insertFromComposition", "insertFromPaste"];""";

    private const string EnterKindDeclaration = """var ENTER_KIND = "insertLineBreak";""";

    /// <summary>FR-004: only a page really restored from the cache is worked out anew here; the first load has its own call.</summary>
    private const string PageShowCondition = "if (event.persisted) { fields.forEach(settle); }";

    /// <summary>FR-003: an undo or any other kind is not entry.</summary>
    private const string IsEntryKindBody = "return ENTRY_KINDS.indexOf(kind) !== -1;";

    /// <summary>FR-003: a missing kind is not a deletion.</summary>
    private const string IsDeletionKindBody = """return typeof kind === "string" && kind.indexOf("delete") === 0;""";

    private const string ShowBody = """
        field.input.type = "text";
        field.control.setAttribute("aria-pressed", "true");
        field.control.setAttribute("aria-label", field.control.dataset.hideName);
        """;

    private const string HideBody = """
        field.input.type = "password";
        field.control.setAttribute("aria-pressed", "false");
        field.control.setAttribute("aria-label", field.control.dataset.showName);
        """;

    /// <summary>FR-002, FR-007.3.</summary>
    private const string SetStateBody = """
        var hadFocus = document.activeElement === field.control;
        if (state !== ENTERED) { hide(field); }
        field.state = state;
        field.control.hidden = state !== ENTERED;
        if (hadFocus && state !== ENTERED) { field.input.focus(); }
        """;

    /// <summary>FR-004.2.</summary>
    private const string SettleBody = "setState(field, isEmpty(field.input) && !isFilled(field.input) ? EMPTY : FILLED);";

    /// <summary>FR-005.</summary>
    private const string OnBeforeInputBody = """
        var kind = event.inputType;
        if (isEntryKind(kind)) {
            if (isEmpty(field.input) && !isFilled(field.input)) {
                setState(field, EMPTY);
                field.awaitingEntry = true;
            } else if (field.state === EMPTY) {
                setState(field, FILLED);
            }
            return;
        }
        if (!isDeletionKind(kind) && kind !== ENTER_KIND) {
            setState(field, FILLED);
        }
        """;

    /// <summary>FR-006 steps 1 to 4 in order, step 5 by falling through, then the mark cleared.</summary>
    private const string OnInputBody = """
        var kind = event.inputType;
        if (isFilled(field.input) || kind === ENTER_KIND || !(isEntryKind(kind) || isDeletionKind(kind))) {
            setState(field, FILLED);
        } else if (isEmpty(field.input)) {
            setState(field, EMPTY);
        } else if (isEntryKind(kind) && field.state === EMPTY) {
            setState(field, field.awaitingEntry ? ENTERED : FILLED);
        } else if (isDeletionKind(kind) && field.state === EMPTY) {
            setState(field, FILLED);
        }
        field.awaitingEntry = false;
        """;

    /// <summary>FR-007.1.</summary>
    private const string OnChangeBody = "if (isFilled(field.input)) { setState(field, FILLED); }";

    /// <summary>FR-007.2.</summary>
    private const string OnPressBody = """
        if (isFilled(field.input)) {
            setState(field, FILLED);
            return;
        }
        if (isEmpty(field.input)) {
            setState(field, EMPTY);
            return;
        }
        if (field.state !== ENTERED) {
            return;
        }
        if (field.input.type === "password") {
            show(field);
        } else {
            hide(field);
        }
        """;

    /// <summary>Source text with every run of white space made one space, so only the code's tokens are compared.</summary>
    private static string Spaced(string source) => Regex.Replace(source, @"\s+", " ").Trim();

    /// <summary>What is wrong when a function's whole body differs from the text fixed for it.</summary>
    private static IEnumerable<string> BodyProblems(string code, string name, string expected)
    {
        var body = FunctionBody(code, name);
        if (body is null)
        {
            yield return $"no function {name}";
        }
        else if (Spaced(body) != Spaced(expected))
        {
            yield return $"{name} is not the text fixed for it. Expected: {Spaced(expected)} Found: {Spaced(body)}";
        }
    }

    /// <summary>What is wrong when a statement fixed outside any helper is not in the source as written.</summary>
    private static IEnumerable<string> StatementProblems(string code, string statement)
    {
        if (!Spaced(code).Contains(Spaced(statement), StringComparison.Ordinal))
        {
            yield return $"the source does not hold: {statement}";
        }
    }

    /// <summary>The text between the braces of a named function; null when there is none.</summary>
    public static string? FunctionBody(string code, string name)
    {
        var header = Regex.Match(code, $@"\bfunction\s+{Regex.Escape(name)}\s*\(");
        if (!header.Success)
        {
            return null;
        }

        var open = code.IndexOf('{', header.Index);
        if (open < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}' && --depth == 0)
            {
                return code[(open + 1)..i];
            }
        }

        return null;
    }

    /// <summary>FR-003: entry kinds are exactly the closed list, the Enter kind stays out of it, every other kind goes unnamed, deletions are told by prefix.</summary>
    public static IReadOnlyList<string> KindProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(StatementProblems(code, EntryKindsDeclaration));
        problems.AddRange(StatementProblems(code, EnterKindDeclaration));
        problems.AddRange(BodyProblems(code, "isEntryKind", IsEntryKindBody));
        problems.AddRange(BodyProblems(code, "isDeletionKind", IsDeletionKindBody));

        var lists = Regex.Matches(code, @"\[[^\[\]]*[""']insertText[""'][^\[\]]*\]");
        if (lists.Count != 1)
        {
            problems.Add($"expected one array literal holding the entry kinds, found {lists.Count}");
        }
        else
        {
            var listed = Regex.Matches(lists[0].Value, @"[""'](?<k>[^""']*)[""']").Select(m => m.Groups["k"].Value).Order(StringComparer.Ordinal).ToList();
            if (!listed.SequenceEqual(EntryKinds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                problems.Add($"the entry kinds are not the closed list: {string.Join(", ", listed)}");
            }
        }

        var named = Regex.Matches(code, @"[""'](?<k>(?:insert|history)[A-Za-z]*)[""']").Select(m => m.Groups["k"].Value).ToList();
        problems.AddRange(named.Where(k => k != EnterKind && !EntryKinds.Contains(k, StringComparer.Ordinal)).Select(k => $"a kind outside the rules is named: {k}"));

        if (named.Count(k => k == EnterKind) != 1)
        {
            problems.Add($"{EnterKind} must be named exactly once");
        }

        if (Regex.IsMatch(code, @"/\^(?:insert|history)"))
        {
            problems.Add("a kind is matched by a prefix pattern");
        }

        if (!Regex.IsMatch(code, @"indexOf\(\s*[""']delete[""']\s*\)\s*===\s*0|startsWith\(\s*[""']delete[""']\s*\)|/\^delete/"))
        {
            problems.Add("deletion kinds are not told by the prefix 'delete'");
        }

        if (!code.Contains(".inputType", StringComparison.Ordinal))
        {
            problems.Add("the script does not read inputType");
        }

        return problems;
    }

    /// <summary>FR-003: filled means <c>:autofill</c>, and a browser that does not know the selector answers filled.</summary>
    public static IReadOnlyList<string> AutofillProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();

        var body = FunctionBody(code, "isFilled");
        if (body is null)
        {
            return ["no function isFilled"];
        }

        if (!Regex.IsMatch(body, @"^\s*try\s*\{\s*return\s+input\.matches\(\s*[""']:autofill[""']\s*\)\s*;?\s*\}\s*catch\s*(?:\(\s*\w*\s*\))?\s*\{\s*return\s+true\s*;?\s*\}\s*$"))
        {
            problems.Add("isFilled is not one :autofill check that answers true when the call throws");
        }

        if (Regex.Matches(code, ":autofill").Count != 1)
        {
            problems.Add(":autofill must be named exactly once");
        }

        return problems;
    }

    /// <summary>FR-002, FR-006 step 3, SC-2: the one route to <c>ENTERED</c>, and the one place the state is written.</summary>
    public static IReadOnlyList<string> EnteredRouteProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(StatementProblems(code, StatesDeclaration));
        problems.AddRange(StatementProblems(code, FieldDeclaration));

        const string declaration = @"\bENTERED\s*=\s*[""']ENTERED[""']";
        if (Regex.Matches(code, declaration).Count != 1)
        {
            problems.Add("the state ENTERED is not declared exactly once");
        }

        var rest = Regex.Replace(Regex.Replace(code, declaration, " "), @"[=!]==\s*ENTERED\b", " ");
        var uses = Regex.Matches(rest, @"\bENTERED\b").Count;
        if (uses != 1)
        {
            problems.Add($"expected one route to ENTERED, found {uses}");
        }

        var onInput = FunctionBody(code, "onInput");
        if (onInput is null || !Regex.IsMatch(onInput, EnteredRoute))
        {
            problems.Add("onInput does not hold 'field.awaitingEntry ? ENTERED : FILLED'");
        }

        var setState = FunctionBody(code, "setState");
        if (Regex.Matches(code, @"\.state\s*=(?!=)").Count != 1 || setState is null || !Regex.IsMatch(setState, @"\.state\s*=(?!=)"))
        {
            problems.Add("the state must be written exactly once, in setState");
        }

        var onBeforeInput = FunctionBody(code, "onBeforeInput");
        if (Regex.Matches(code, @"awaitingEntry\s*=\s*true").Count != 1 || onBeforeInput is null || !Regex.IsMatch(onBeforeInput, @"awaitingEntry\s*=\s*true"))
        {
            problems.Add("the mark must be set exactly once, in onBeforeInput");
        }

        return problems;
    }

    /// <summary>FR-002, FR-007.3, AC-001, AC-005: the control is visible only in <c>ENTERED</c>, leaving it hides the field first, and the focus is not dropped.</summary>
    public static IReadOnlyList<string> VisibilityProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(BodyProblems(code, "setState", SetStateBody));
        problems.AddRange(BodyProblems(code, "show", ShowBody));
        problems.AddRange(BodyProblems(code, "hide", HideBody));

        if (Regex.IsMatch(code, @"removeAttribute\(\s*[""']hidden|setAttribute\(\s*[""']hidden|toggleAttribute|\.hidden\s*=\s*false"))
        {
            problems.Add("the control's hidden state is written by something other than setState");
        }

        if (Regex.Matches(code, @"\.hidden\s*=(?!=)").Count != 1)
        {
            problems.Add("the control's hidden state must be written exactly once");
        }

        var body = FunctionBody(code, "setState");
        if (body is null)
        {
            problems.Add("no function setState");
            return problems;
        }

        var write = Regex.Match(body, @"field\.control\.hidden\s*=\s*state\s*!==\s*ENTERED\s*;");
        if (!write.Success)
        {
            problems.Add("setState does not hold 'field.control.hidden = state !== ENTERED;'");
            return problems;
        }

        var hide = body.IndexOf("hide(field)", StringComparison.Ordinal);
        if (hide < 0 || hide > write.Index)
        {
            problems.Add("setState does not hide the field before it hides the control");
        }

        var focusRead = body.IndexOf("document.activeElement", StringComparison.Ordinal);
        var focusMove = body.IndexOf(".focus()", StringComparison.Ordinal);
        if (focusRead < 0 || focusRead > write.Index || focusMove < write.Index)
        {
            problems.Add("setState does not read the focus before hiding the control and move it to the field after");
        }

        return problems;
    }

    /// <summary>FR-005: the rules of <c>beforeinput</c>.</summary>
    public static IReadOnlyList<string> BeforeInputProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(BodyProblems(code, "onBeforeInput", OnBeforeInputBody));

        if (!HasListener(code, "beforeinput", "onBeforeInput"))
        {
            problems.Add("no beforeinput listener calling onBeforeInput");
        }

        var body = FunctionBody(code, "onBeforeInput");
        if (body is null)
        {
            problems.Add("no function onBeforeInput");
            return problems;
        }

        foreach (var expected in new[] { "isEntryKind(", "isDeletionKind(", "ENTER_KIND", EmptyCheck, FilledCheck, SetEmpty })
        {
            if (!body.Contains(expected, StringComparison.Ordinal))
            {
                problems.Add($"onBeforeInput does not hold {expected}");
            }
        }

        if (Regex.Matches(body, Regex.Escape(SetFilled)).Count < 2)
        {
            problems.Add("onBeforeInput must go to FILLED both for hidden text under an entry kind and for any other kind");
        }

        return problems;
    }

    /// <summary>FR-006: the order of the checks of <c>input</c>, and the mark cleared after them.</summary>
    public static IReadOnlyList<string> InputProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(BodyProblems(code, "onInput", OnInputBody));

        if (!HasListener(code, "input", "onInput"))
        {
            problems.Add("no input listener calling onInput");
        }

        var body = FunctionBody(code, "onInput");
        if (body is null)
        {
            problems.Add("no function onInput");
            return problems;
        }

        var filled = body.IndexOf(FilledCheck, StringComparison.Ordinal);
        var empty = body.IndexOf(EmptyCheck, StringComparison.Ordinal);
        var enterKind = body.IndexOf("ENTER_KIND", StringComparison.Ordinal);
        var route = Regex.Match(body, EnteredRoute);

        if (filled < 0 || empty < 0 || filled > empty)
        {
            problems.Add("step 1 (filled) does not come before step 2 (empty)");
        }

        if (enterKind < 0 || enterKind > empty)
        {
            problems.Add("the Enter kind is not part of step 1");
        }

        if (!route.Success)
        {
            problems.Add("step 3 is missing");
            return problems;
        }

        if (empty > route.Index)
        {
            problems.Add("step 2 (empty) does not come before step 3");
        }

        var condition = body[Math.Max(0, body.LastIndexOf("if", route.Index, StringComparison.Ordinal))..route.Index];
        if (!condition.Contains("isEntryKind(", StringComparison.Ordinal) || !Regex.IsMatch(condition, @"\.state\s*===\s*EMPTY\b"))
        {
            problems.Add("step 3 is not limited to an entry kind in state EMPTY");
        }

        var after = body[route.Index..];
        if (!Regex.IsMatch(after, @"isDeletionKind\(\w+\)\s*&&\s*field\.state\s*===\s*EMPTY\b"))
        {
            problems.Add("step 4 (a deletion in state EMPTY with text left) is missing");
        }

        if (!Regex.IsMatch(after, @"awaitingEntry\s*=\s*false"))
        {
            problems.Add("the mark is not cleared after the checks");
        }

        return problems;
    }

    /// <summary>FR-007.1, FR-007.2, AC-003: <c>change</c> and a press re-check, and only a press of a field still <c>ENTERED</c> shows it.</summary>
    public static IReadOnlyList<string> ChangeAndPressProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(BodyProblems(code, "onChange", OnChangeBody));
        problems.AddRange(BodyProblems(code, "onPress", OnPressBody));

        if (!HasListener(code, "change", "onChange"))
        {
            problems.Add("no change listener calling onChange");
        }

        if (!HasListener(code, "click", "onPress"))
        {
            problems.Add("no click listener calling onPress");
        }

        var onChange = FunctionBody(code, "onChange");
        if (onChange is null || !onChange.Contains(FilledCheck, StringComparison.Ordinal) || !onChange.Contains(SetFilled, StringComparison.Ordinal))
        {
            problems.Add("onChange does not go to FILLED when the browser filled the field");
        }

        if (Regex.Matches(code, @"(?<!function\s)\bshow\(field\)").Count != 1)
        {
            problems.Add("show(field) must be called exactly once");
        }

        if (Regex.Matches(code, @"\.type\s*=\s*[""']text[""']").Count != 1)
        {
            problems.Add("the field must be switched to text in exactly one place");
        }

        var onPress = FunctionBody(code, "onPress");
        if (onPress is null)
        {
            problems.Add("no function onPress");
            return problems;
        }

        var filled = onPress.IndexOf(FilledCheck, StringComparison.Ordinal);
        var empty = onPress.IndexOf(EmptyCheck, StringComparison.Ordinal);
        var guard = Regex.Match(onPress, @"[=!]==\s*ENTERED\b");
        var show = onPress.IndexOf("show(field)", StringComparison.Ordinal);

        if (filled < 0 || empty < filled || !guard.Success || guard.Index < empty || show < guard.Index)
        {
            problems.Add("a press does not check filled, then empty, then ENTERED before it shows the field");
        }

        foreach (var expected in new[] { SetFilled, SetEmpty })
        {
            if (!onPress.Contains(expected, StringComparison.Ordinal))
            {
                problems.Add($"onPress does not hold {expected}");
            }
        }

        return problems;
    }

    /// <summary>FR-004: at load and on a return from the back-forward cache the state is worked out anew, and the wrapper is marked ready whatever the state.</summary>
    public static IReadOnlyList<string> LoadAndReturnProblems(string script)
    {
        var code = PasswordFieldMarkup.ScriptCode(script);
        var problems = new List<string>();
        problems.AddRange(BodyProblems(code, "settle", SettleBody));
        problems.AddRange(StatementProblems(code, PageShowCondition));

        var settle = FunctionBody(code, "settle");
        if (settle is null)
        {
            problems.Add("no function settle");
        }
        else if (!Regex.IsMatch(settle, @"isEmpty\(\s*field\.input\s*\)\s*&&\s*!\s*isFilled\(\s*field\.input\s*\)\s*\?\s*EMPTY\s*:\s*FILLED\b"))
        {
            problems.Add("settle does not give EMPTY for an empty unfilled field and FILLED otherwise");
        }

        var pageShow = Regex.Match(code, @"\bwindow\s*\.\s*addEventListener\(\s*[""']pageshow[""']");
        if (!pageShow.Success)
        {
            problems.Add("no pageshow listener on the window");
        }
        else if (!Regex.IsMatch(code[pageShow.Index..], @"\.persisted\b[\s\S]*?\bsettle\b"))
        {
            problems.Add("pageshow does not work out every field's state anew when persisted");
        }

        var firstCall = Regex.Match(code, @"(?<!function\s)\bsettle\(field\)");
        if (!firstCall.Success)
        {
            problems.Add("the state is not worked out for each field at load");
        }

        var setState = FunctionBody(code, "setState");
        if (setState is not null && setState.Contains(PasswordFieldMarkup.ReadyClass, StringComparison.Ordinal))
        {
            problems.Add("the ready class depends on the state");
        }

        if (!Regex.IsMatch(code, $@"classList\.add\(\s*[""']{Regex.Escape(PasswordFieldMarkup.ReadyClass)}[""']"))
        {
            problems.Add("the wrapper is not marked ready");
        }

        return problems;
    }

    private static bool HasListener(string code, string eventName, string handler) =>
        Regex.IsMatch(code, $@"\.addEventListener\(\s*[""']{eventName}[""']\s*,\s*function\s*\([^)]*\)\s*\{{\s*{handler}\(field\b");
}
