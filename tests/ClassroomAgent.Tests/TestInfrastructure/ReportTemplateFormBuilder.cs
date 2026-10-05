using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The posted template form of api-design §2.7, as name/value pairs. <see cref="Valid"/> is the form of a legitimate
/// browser for a full, no-conversion template; every test changes only what it is about. The pairs keep their order,
/// and a field can be repeated or dropped, so the structural ("tampered") cases can be built too.
/// </summary>
public sealed class ReportTemplateFormBuilder
{
    private readonly List<KeyValuePair<string, string?>> _fields = [];

    private ReportTemplateFormBuilder()
    {
    }

    /// <summary>
    /// A valid form: the given name, full view, materials shown, no conversion, 2 hours, every mark program, names from
    /// the profile (US-042 VR-001 — the form always sends one of the two).
    /// </summary>
    public static ReportTemplateFormBuilder Valid(string name = "Test Template One")
    {
        var form = new ReportTemplateFormBuilder()
            .Set("name", name)
            .Set("view", "full")
            .Set("hideMaterials", "false")
            .Set("scaleMode", "none")
            .Set("hoursPerLesson", "2");
        foreach (var state in ReportTemplateTestData.States)
        {
            form.Set($"marks[{state}].kind", "program").Set($"marks[{state}].text", string.Empty);
        }

        return form.Set("lateMark.kind", "program").Set("lateMark.text", string.Empty)
            .Set(ReportTemplateTestData.NamesField, "profile");
    }

    /// <summary>Replaces the value of a field (every occurrence), or appends it when absent.</summary>
    public ReportTemplateFormBuilder Set(string name, string? value)
    {
        var index = _fields.FindIndex(f => f.Key == name);
        if (index < 0)
        {
            _fields.Add(new(name, value));
        }
        else
        {
            _fields.RemoveAll(f => f.Key == name);
            _fields.Insert(index, new(name, value));
        }

        return this;
    }

    /// <summary>Appends another occurrence of a field — a tampered form (api-design §2.5).</summary>
    public ReportTemplateFormBuilder Repeat(string name, string? value)
    {
        _fields.Add(new(name, value));
        return this;
    }

    public ReportTemplateFormBuilder Remove(string name)
    {
        _fields.RemoveAll(f => f.Key == name);
        return this;
    }

    public ReportTemplateFormBuilder Mark(ReportCellState state, string kind, string? text = null) =>
        Set($"marks[{state}].kind", kind).Set($"marks[{state}].text", text ?? string.Empty);

    public ReportTemplateFormBuilder LateMark(string kind, string? text = null) =>
        Set("lateMark.kind", kind).Set("lateMark.text", text ?? string.Empty);

    /// <summary>Switches to ranges and replaces every scale row with <paramref name="rows"/>, indexed from 0 as submitted.</summary>
    public ReportTemplateFormBuilder Ranges(IEnumerable<(string From, string To, string Label)> rows)
    {
        _fields.RemoveAll(f => f.Key.StartsWith("scale[", StringComparison.Ordinal));
        Set("scaleMode", "ranges");
        var i = 0;
        foreach (var (from, to, label) in rows)
        {
            _fields.Add(new($"scale[{i}].from", from));
            _fields.Add(new($"scale[{i}].to", to));
            _fields.Add(new($"scale[{i}].label", label));
            i++;
        }

        return this;
    }

    public ReportTemplateFormBuilder Ranges(IEnumerable<(int From, int To, string Label)> rows) =>
        Ranges(rows.Select(r => (r.From.ToString(System.Globalization.CultureInfo.InvariantCulture),
            r.To.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Label)));

    public ReportTemplateFormBuilder TwelvePoint() => Ranges(ReportTemplateTestData.TwelvePoint);

    public IReadOnlyList<KeyValuePair<string, string?>> Fields => _fields;

    /// <summary>The form as the use cases take it.</summary>
    public ReportTemplateFormInput Input() => new(_fields.ToList());

    /// <summary>The form as an HTTP body (the antiforgery token is added by <see cref="FormClient"/>).</summary>
    public IEnumerable<KeyValuePair<string, string>> Http() =>
        _fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value ?? string.Empty));
}
