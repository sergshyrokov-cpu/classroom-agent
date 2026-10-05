using System.Globalization;
using System.Text.RegularExpressions;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// Reads the posted template form of api-design §2.7 (US-027 spec VR-002, VR-003, VR-004). A form no browser sends —
/// an unknown enumerated value, a missing or unknown state, a repeated field, a bad row index — does not read: the
/// caller answers "form malformed", not a field error. Unknown field names (the antiforgery token) are ignored.
/// </summary>
internal static partial class ReportTemplateFormReader
{
    private static readonly HashSet<string> SingleFields = new(StringComparer.Ordinal)
    {
        "name", "view", "hideMaterials", "scaleMode", "hoursPerLesson", "lateMark.kind", "lateMark.text",
    };

    [GeneratedRegex(@"^marks\[([^\]]*)\]\.(kind|text)$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkKey();

    [GeneratedRegex(@"^scale\[([0-9]{1,9})\]\.(from|to|label)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScaleKey();

    public static bool TryRead(ReportTemplateFormInput input, out ParsedReportTemplateForm? form)
    {
        ArgumentNullException.ThrowIfNull(input);
        form = null;

        var singles = new Dictionary<string, string>(StringComparer.Ordinal);
        var markFields = new Dictionary<(ReportCellState State, string Member), string>();
        var scaleFields = new Dictionary<(int Index, string Member), string>();
        var stateNames = Enum.GetNames<ReportCellState>();
        var names = new List<string?>();

        // The scale rows are read only with "ranges": with "no conversion" they are ignored (VR-004).
        var ranges = input.Fields.Any(f => f.Key == "scaleMode" && f.Value == "ranges");

        foreach (var (key, rawValue) in input.Fields)
        {
            var value = rawValue ?? string.Empty;
            if (key == NameSourceCode.FieldName)
            {
                names.Add(value);
            }
            else if (SingleFields.Contains(key))
            {
                if (!singles.TryAdd(key, value))
                {
                    return false;
                }
            }
            else if (key.StartsWith("marks[", StringComparison.Ordinal))
            {
                var match = MarkKey().Match(key);
                if (!match.Success
                    || !stateNames.Contains(match.Groups[1].Value, StringComparer.Ordinal)
                    || !markFields.TryAdd((Enum.Parse<ReportCellState>(match.Groups[1].Value), match.Groups[2].Value), value))
                {
                    return false;
                }
            }
            else if (ranges && key.StartsWith("scale[", StringComparison.Ordinal))
            {
                var match = ScaleKey().Match(key);
                if (!match.Success
                    || !scaleFields.TryAdd(
                        (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), match.Groups[2].Value),
                        value))
                {
                    return false;
                }
            }
        }

        if (!singles.TryGetValue("name", out var name)
            || !singles.TryGetValue("hoursPerLesson", out var hours)
            || !TryView(singles.GetValueOrDefault("view"), out var view)
            || !TryBool(singles.GetValueOrDefault("hideMaterials"), out var hideMaterials)
            || !TryScaleMode(singles.GetValueOrDefault("scaleMode"), out var scaleMode)
            || !TryLateKind(singles.GetValueOrDefault("lateMark.kind"), out var lateKind)
            || !TryMarks(markFields, out var marks)
            || !TryScaleRows(scaleFields, out var rows))
        {
            return false;
        }

        form = new ParsedReportTemplateForm(
            name.Trim(),
            view,
            hideMaterials,
            hours.Trim(),
            scaleMode,
            rows,
            marks,
            new ParsedReportTemplateForm.LateMarkEntry(
                lateKind,
                singles.GetValueOrDefault("lateMark.text", string.Empty).Trim()),
            NameSourceCode.TryParseSingle(names, out var nameSource) ? nameSource : null);
        return true;
    }

    private static bool TryView(string? text, out ReportView view)
    {
        view = text == "full" ? ReportView.Full : ReportView.Short;
        return text is "full" or "short";
    }

    private static bool TryBool(string? text, out bool value)
    {
        value = text == "true";
        return text is "true" or "false";
    }

    private static bool TryScaleMode(string? text, out ReportScaleMode mode)
    {
        mode = text == "ranges" ? ReportScaleMode.Ranges : ReportScaleMode.None;
        return text is "none" or "ranges";
    }

    private static bool TryLateKind(string? text, out ReportLateMarkKind kind)
    {
        kind = text switch
        {
            "own" => ReportLateMarkKind.Own,
            "hidden" => ReportLateMarkKind.Hidden,
            _ => ReportLateMarkKind.Program,
        };
        return text is "program" or "own" or "hidden";
    }

    private static bool TryMarks(
        Dictionary<(ReportCellState State, string Member), string> fields,
        out Dictionary<ReportCellState, ParsedReportTemplateForm.MarkEntry> marks)
    {
        marks = [];
        foreach (var state in Enum.GetValues<ReportCellState>())
        {
            if (!fields.TryGetValue((state, "kind"), out var kindText))
            {
                return false;
            }

            ReportMarkKind kind;
            switch (kindText)
            {
                case "program":
                    kind = ReportMarkKind.Program;
                    break;
                case "own":
                    kind = ReportMarkKind.Own;
                    break;
                case "empty":
                    kind = ReportMarkKind.Empty;
                    break;
                default:
                    return false;
            }

            marks[state] = new ParsedReportTemplateForm.MarkEntry(
                kind,
                fields.GetValueOrDefault((state, "text"), string.Empty).Trim());
        }

        return true;
    }

    private static bool TryScaleRows(
        Dictionary<(int Index, string Member), string> fields,
        out List<ParsedReportTemplateForm.ScaleRowEntry> rows)
    {
        rows = [];
        foreach (var index in fields.Keys.Select(k => k.Index).Distinct().Order())
        {
            if (!fields.TryGetValue((index, "from"), out var from)
                || !fields.TryGetValue((index, "to"), out var to)
                || !fields.TryGetValue((index, "label"), out var label))
            {
                return false;
            }

            rows.Add(new ParsedReportTemplateForm.ScaleRowEntry(index + 1, from.Trim(), to.Trim(), label.Trim()));
        }

        return true;
    }
}
