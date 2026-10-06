using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using static ClassroomAgent.Application.UseCases.JournalQueryRules;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// The shape rules of the report query (US-027 spec FR-011, VR-006, VR-007; US-025 VR-001 … VR-003; US-042 VR-002),
/// shared by the report page and the journal export so the same input gives the same message (US-028 spec FR-003,
/// VR-001; entity model §3.3). Pure: it reads nothing.
/// </summary>
internal static class ReportRequestReader
{
    /// <summary>
    /// Reads the query in parameter order template, courseId, from, to, the pair, then the name source (US-042
    /// api-design §2.5). With <paramref name="required"/> an absent course or date is malformed (US-028 spec VR-001:
    /// an export has no form-only state); otherwise an absent course selects nothing and an absent date takes its default.
    /// </summary>
    public static ReportSelection Read(ReportRequest request, DateOnly defaultFrom, DateOnly defaultTo, bool required)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<ReportMessageKey>();
        var selected = ReportTemplateReference.AcademicJournal;
        long? createdId = null;
        var templateValid = true;
        switch (Single(request.Template, out var templateText))
        {
            case Presence.Absent:
                break;
            case Presence.One:
                switch (ReportTemplateReference.Parse(templateText, out var parsedId))
                {
                    case ReportTemplateReference.Kind.BuiltIn:
                        break;
                    case ReportTemplateReference.Kind.Created:
                        createdId = parsedId;
                        selected = ReportTemplateReference.Of(parsedId);
                        break;
                    default:
                        templateValid = false;
                        messages.Add(ReportMessageKey.TemplateMalformed);
                        break;
                }

                break;
            default:
                templateValid = false;
                messages.Add(ReportMessageKey.TemplateMalformed);
                break;
        }

        long? course = null;
        switch (Single(request.CourseId, out var courseText))
        {
            case Presence.Absent when !required:
                break;
            case Presence.One when ParseCourseId(courseText) is { } id:
                course = id;
                break;
            default:
                messages.Add(ReportMessageKey.CourseMalformed);
                break;
        }

        var from = ReadRequiredDate(request.From, defaultFrom, required, out var fromMalformed);
        if (fromMalformed)
        {
            messages.Add(ReportMessageKey.FromMalformed);
        }

        var to = ReadRequiredDate(request.To, defaultTo, required, out var toMalformed);
        if (toMalformed)
        {
            messages.Add(ReportMessageKey.ToMalformed);
        }

        if (from is { } f && to is { } t && f > t)
        {
            messages.Add(ReportMessageKey.PeriodInverted);
        }

        // US-042 VR-002: absent means the template's setting; empty, repeated or unknown is malformed.
        ReportNameSource? pageSource = null;
        if (request.Names is { Count: > 0 } names)
        {
            if (NameSourceCode.TryParseSingle(names, out var parsedSource))
            {
                pageSource = parsedSource;
            }
            else
            {
                messages.Add(ReportMessageKey.NameSourceMalformed);
            }
        }

        return new ReportSelection(selected, templateValid, createdId, course, from, to, pageSource, messages);
    }

    private static DateOnly? ReadRequiredDate(IReadOnlyList<string?> values, DateOnly fallback, bool required, out bool malformed)
    {
        if (required && Single(values, out _) == Presence.Absent)
        {
            malformed = true;
            return null;
        }

        return ReadDate(values, fallback, out malformed);
    }
}
