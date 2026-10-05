using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// A report template created in the school (US-027 entity model §2.1; <c>trebovaniya.md</c> §3 <c>ReportTemplate</c>,
/// v84) — school-own data, referencing nothing in the mirror. The author is a bare account id (db-design §2.1).
/// </summary>
/// <remarks>
/// <see cref="Create"/> and <see cref="Change"/> throw <see cref="ArgumentException"/> on a broken invariant: a
/// programming error, because <c>Application</c> validates the form first (spec §6).
/// </remarks>
public sealed class ReportTemplate
{
    public const int MaxNameLength = 100;

    public const int MaxMarkTextLength = 30;

    public const int MaxScaleLabelLength = 10;

    public const int MinHoursPerLesson = 1;

    public const int MaxHoursPerLesson = 10;

    /// <summary>VR-004: one row per whole percent at most.</summary>
    public const int MaxScaleRows = 101;

    private readonly List<ReportTemplateMark> marks = [];

    private readonly List<ReportTemplateScaleRow> scaleRows = [];

    private ReportTemplate()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>Trimmed, as written (VR-001).</summary>
    public string Name { get; private set; }

    /// <summary><see cref="Name"/> upper-cased invariant — the uniqueness key (db-design §2.1).</summary>
    public string NormalizedName { get; private set; }

    /// <summary>The creating <c>AppUser</c>'s id; no navigation, no foreign key (db-design §2.1, PC-11).</summary>
    public long AuthorId { get; private set; }

    public ReportView View { get; private set; }

    public bool HideMaterials { get; private set; }

    public int HoursPerLesson { get; private set; }

    public ReportScaleMode ScaleMode { get; private set; }

    public ReportLateMarkKind LateMarkKind { get; private set; }

    public string? LateMarkText { get; private set; }

    /// <summary>US-042 skeleton (OD-001): the template setting "names" (entity model §1.4). Completed at IMPLEMENTATION.</summary>
    public ReportNameSource NameSource => throw new NotImplementedException();

    public IReadOnlyList<ReportTemplateMark> Marks => marks;

    public IReadOnlyList<ReportTemplateScaleRow> ScaleRows => scaleRows;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The trimmed name upper-cased invariant — the uniqueness key (db-design §2.1).</summary>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    public static ReportTemplate Create(string name, ReportTemplateSettings settings, long authorId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authorId);

        var template = new ReportTemplate { AuthorId = authorId };
        template.Apply(name, settings);
        return template;
    }

    /// <summary>Replaces the name and every setting (spec FR-009); the author does not change.</summary>
    public void Change(string name, ReportTemplateSettings settings) => Apply(name, settings);

    /// <summary>The settings the report builder and the forms take, scale rows ordered by <c>from</c>.</summary>
    public ReportTemplateSettings ToSettings() => new(
        View,
        HideMaterials,
        HoursPerLesson,
        ScaleMode,
        scaleRows.OrderBy(r => r.FromPercent).Select(r => new ReportScaleRow(r.FromPercent, r.ToPercent, r.Label)).ToList(),
        marks.ToDictionary(m => m.State, m => new ReportMark(m.Kind, m.Text)),
        new ReportLateMark(LateMarkKind, LateMarkText));

    private void Apply(string name, ReportTemplateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(settings);

        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException($"A template name has 1 to {MaxNameLength} characters.", nameof(name));
        }

        if (settings.HoursPerLesson is < MinHoursPerLesson or > MaxHoursPerLesson)
        {
            throw new ArgumentException("Hours per lesson lie between 1 and 10.", nameof(settings));
        }

        CheckMarks(settings);
        CheckLateMark(settings.LateMark);
        var rows = CheckScale(settings);

        Name = trimmed;
        NormalizedName = Normalize(trimmed);
        View = settings.View;
        HideMaterials = settings.HideMaterials;
        HoursPerLesson = settings.HoursPerLesson;
        ScaleMode = settings.ScaleMode;
        LateMarkKind = settings.LateMark.Kind;
        LateMarkText = settings.LateMark.Text;

        // db-design §2.4: marks are updated in place by state; scale rows are replaced wholesale.
        foreach (var (state, mark) in settings.Marks)
        {
            var existing = marks.SingleOrDefault(m => m.State == state);
            if (existing is null)
            {
                marks.Add(ReportTemplateMark.Create(state, mark.Kind, mark.Text));
            }
            else
            {
                existing.Set(mark.Kind, mark.Text);
            }
        }

        scaleRows.Clear();
        scaleRows.AddRange(rows.Select(r => ReportTemplateScaleRow.Create(r.FromPercent, r.ToPercent, r.Label)));
    }

    private static void CheckMarks(ReportTemplateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.Marks);
        var states = Enum.GetValues<ReportCellState>();
        if (settings.Marks.Count != states.Length || states.Any(s => !settings.Marks.ContainsKey(s)))
        {
            throw new ArgumentException("A template maps exactly the nine cell states.", nameof(settings));
        }

        foreach (var mark in settings.Marks.Values)
        {
            ArgumentNullException.ThrowIfNull(mark);
            CheckText(mark.Kind == ReportMarkKind.Own, mark.Text, MaxMarkTextLength);
        }
    }

    private static void CheckLateMark(ReportLateMark lateMark)
    {
        ArgumentNullException.ThrowIfNull(lateMark);
        CheckText(lateMark.Kind == ReportLateMarkKind.Own, lateMark.Text, MaxMarkTextLength);
    }

    /// <summary>VR-003: own text is set, non-empty and bounded exactly when the kind is own.</summary>
    private static void CheckText(bool own, string? text, int maxLength)
    {
        if (own ? string.IsNullOrEmpty(text) || text.Length > maxLength : text is not null)
        {
            throw new ArgumentException("A mark carries its own text exactly when its kind is own.", nameof(text));
        }
    }

    /// <summary>VR-004: no rows without conversion; otherwise 1 … 101 rows covering 0–100 without gap or overlap.</summary>
    private static List<ReportScaleRow> CheckScale(ReportTemplateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.ScaleRows);
        var rows = settings.ScaleRows.OrderBy(r => r.FromPercent).ToList();
        if (settings.ScaleMode == ReportScaleMode.None)
        {
            return rows.Count == 0
                ? rows
                : throw new ArgumentException("A scale without conversion has no rows.", nameof(settings));
        }

        if (rows.Count is 0 or > MaxScaleRows)
        {
            throw new ArgumentException("A range scale has 1 to 101 rows.", nameof(settings));
        }

        var next = 0;
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.FromPercent != next
                || row.ToPercent < row.FromPercent
                || row.ToPercent > 100
                || string.IsNullOrEmpty(row.Label)
                || row.Label.Length > MaxScaleLabelLength)
            {
                throw new ArgumentException("Scale rows cover 0–100 without gap or overlap.", nameof(settings));
            }

            next = row.ToPercent + 1;
        }

        return next == 101
            ? rows
            : throw new ArgumentException("Scale rows cover 0–100 without gap or overlap.", nameof(settings));
    }
}
