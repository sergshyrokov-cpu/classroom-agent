using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// The field rules of US-027 spec VR-001, VR-003, VR-004, VR-005 on a form that already has its shape. Every problem is
/// listed, not only the first (api-design §2.5). Name uniqueness needs the repository and is checked by the use case.
/// </summary>
internal static class ReportTemplateFormValidator
{
    public static List<ReportTemplateFieldError> Validate(ParsedReportTemplateForm form)
    {
        var errors = new List<ReportTemplateFieldError>();

        CheckText(
            "name",
            null,
            form.Name,
            ReportTemplate.MaxNameLength,
            ReportTemplateFieldErrorKey.NameRequired,
            ReportTemplateFieldErrorKey.NameTooLong,
            errors);

        foreach (var (state, mark) in form.Marks)
        {
            if (mark.Kind == ReportMarkKind.Own)
            {
                CheckText(
                    $"marks[{state}].text",
                    null,
                    mark.Text,
                    ReportTemplate.MaxMarkTextLength,
                    ReportTemplateFieldErrorKey.MarkTextRequired,
                    ReportTemplateFieldErrorKey.MarkTextTooLong,
                    errors);
            }
        }

        if (form.LateMark.Kind == ReportLateMarkKind.Own)
        {
            CheckText(
                "lateMark.text",
                null,
                form.LateMark.Text,
                ReportTemplate.MaxMarkTextLength,
                ReportTemplateFieldErrorKey.MarkTextRequired,
                ReportTemplateFieldErrorKey.MarkTextTooLong,
                errors);
        }

        CheckHours(form.Hours, errors);

        if (form.ScaleMode == ReportScaleMode.Ranges)
        {
            CheckScale(form.ScaleRows, errors);
        }

        // US-042 VR-001: missing, empty, repeated or unknown "names" — the form again with a message.
        if (form.NameSource is null)
        {
            errors.Add(new ReportTemplateFieldError(
                NameSourceCode.FieldName, ReportTemplateFieldErrorKey.NameSourceInvalid, null));
        }

        return errors;
    }

    /// <summary>The domain settings of a form that has no field error.</summary>
    public static ReportTemplateSettings ToSettings(ParsedReportTemplateForm form) => new(
        form.View,
        form.HideMaterials,
        int.Parse(form.Hours, CultureInfo.InvariantCulture),
        form.ScaleMode,
        form.ScaleMode == ReportScaleMode.Ranges
            ? form.ScaleRows
                .Select(r => new ReportScaleRow(
                    int.Parse(r.From, CultureInfo.InvariantCulture),
                    int.Parse(r.To, CultureInfo.InvariantCulture),
                    r.Label))
                .ToList()
            : [],
        form.Marks.ToDictionary(
            m => m.Key,
            m => new ReportMark(m.Value.Kind, m.Value.Kind == ReportMarkKind.Own ? m.Value.Text : null)),
        new ReportLateMark(
            form.LateMark.Kind,
            form.LateMark.Kind == ReportLateMarkKind.Own ? form.LateMark.Text : null),
        form.NameSource ?? throw new InvalidOperationException("The form has a field error."));

    private static void CheckText(
        string field,
        int? rowNumber,
        string text,
        int maxLength,
        ReportTemplateFieldErrorKey required,
        ReportTemplateFieldErrorKey tooLong,
        List<ReportTemplateFieldError> errors)
    {
        if (text.Length == 0)
        {
            errors.Add(new ReportTemplateFieldError(field, required, rowNumber));
        }
        else if (text.Any(char.IsControl))
        {
            errors.Add(new ReportTemplateFieldError(field, ReportTemplateFieldErrorKey.TextInvalidCharacters, rowNumber));
        }
        else if (text.Length > maxLength)
        {
            errors.Add(new ReportTemplateFieldError(field, tooLong, rowNumber));
        }
    }

    private static void CheckHours(string hours, List<ReportTemplateFieldError> errors)
    {
        if (hours.Length == 0)
        {
            errors.Add(new ReportTemplateFieldError("hoursPerLesson", ReportTemplateFieldErrorKey.HoursRequired, null));
            return;
        }

        if (!TryWholeNumber(hours, out var value)
            || value is < ReportTemplate.MinHoursPerLesson or > ReportTemplate.MaxHoursPerLesson)
        {
            errors.Add(new ReportTemplateFieldError("hoursPerLesson", ReportTemplateFieldErrorKey.HoursOutOfRange, null));
        }
    }

    private static void CheckScale(
        IReadOnlyList<ParsedReportTemplateForm.ScaleRowEntry> rows,
        List<ReportTemplateFieldError> errors)
    {
        if (rows.Count == 0)
        {
            errors.Add(new ReportTemplateFieldError("scale", ReportTemplateFieldErrorKey.ScaleNoRows, null));
            return;
        }

        if (rows.Count > ReportTemplate.MaxScaleRows)
        {
            errors.Add(new ReportTemplateFieldError("scale", ReportTemplateFieldErrorKey.ScaleTooManyRows, null));
            return;
        }

        var before = errors.Count;
        var numbers = new List<(int Number, int From, int To)>();
        foreach (var row in rows)
        {
            var prefix = $"scale[{row.Number - 1}]";
            var fromValid = TryBound(row.From, out var from);
            var toValid = TryBound(row.To, out var to);
            if (!fromValid)
            {
                errors.Add(new ReportTemplateFieldError(prefix + ".from", ReportTemplateFieldErrorKey.ScaleBoundInvalid, row.Number));
            }

            if (!toValid)
            {
                errors.Add(new ReportTemplateFieldError(prefix + ".to", ReportTemplateFieldErrorKey.ScaleBoundInvalid, row.Number));
            }

            if (fromValid && toValid)
            {
                if (from > to)
                {
                    errors.Add(new ReportTemplateFieldError(prefix + ".from", ReportTemplateFieldErrorKey.ScaleRowInverted, row.Number));
                }
                else
                {
                    numbers.Add((row.Number, from, to));
                }
            }

            CheckText(
                prefix + ".label",
                row.Number,
                row.Label,
                ReportTemplate.MaxScaleLabelLength,
                ReportTemplateFieldErrorKey.ScaleLabelRequired,
                ReportTemplateFieldErrorKey.ScaleLabelTooLong,
                errors);
        }

        if (errors.Count == before)
        {
            CheckCoverage(numbers, errors);
        }
    }

    /// <summary>
    /// VR-004: sorted by <c>from</c>, the rows start at 0, end at 100 and each starts at the previous <c>to</c> + 1. Only
    /// the first offending row is named (test report F-2: the later of two rows, the lowest <c>from</c> when 0 is
    /// missing, the highest when 100 is).
    /// </summary>
    private static void CheckCoverage(List<(int Number, int From, int To)> rows, List<ReportTemplateFieldError> errors)
    {
        var sorted = rows.OrderBy(r => r.From).ThenBy(r => r.Number).ToList();
        if (sorted[0].From != 0)
        {
            errors.Add(new ReportTemplateFieldError("scale", ReportTemplateFieldErrorKey.ScaleGap, sorted[0].Number));
            return;
        }

        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].From <= sorted[i - 1].To)
            {
                errors.Add(new ReportTemplateFieldError("scale", ReportTemplateFieldErrorKey.ScaleOverlap, sorted[i].Number));
                return;
            }

            if (sorted[i].From > sorted[i - 1].To + 1)
            {
                errors.Add(new ReportTemplateFieldError("scale", ReportTemplateFieldErrorKey.ScaleGap, sorted[i].Number));
                return;
            }
        }

        if (sorted[^1].To != 100)
        {
            errors.Add(new ReportTemplateFieldError("scale", ReportTemplateFieldErrorKey.ScaleGap, sorted[^1].Number));
        }
    }

    private static bool TryBound(string text, out int value) =>
        TryWholeNumber(text, out value) && value is >= 0 and <= 100;

    /// <summary>ASCII decimal digits only: no sign, no fraction, no spaces, no other script's digits.</summary>
    private static bool TryWholeNumber(string text, out int value)
    {
        value = 0;
        return text.Length is > 0 and <= 9
            && text.All(char.IsAsciiDigit)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
