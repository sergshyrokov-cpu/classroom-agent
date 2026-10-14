using System.Globalization;
using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-028 spec FR-001 … FR-010 (entity model §3.4): exports the report of the page to an Excel file. The request is
/// validated by the report page's own rules before anything is read; the report is built by the page's own builder,
/// mapped to the workbook model, rendered, audited and committed — in that order — and only then returned. US-043 adds
/// the second layout: the same report filled into copies of the shipped form (spec FR-004 … FR-007), with the same
/// audit row (FR-010, I-11).
/// </summary>
/// <remarks>
/// No <see cref="IReadOnlyModeGuard"/>: exporting already-synced data works in read-only mode (BR-026), and the one
/// write — the audit row — is declared as <see cref="PermittedServiceWrite.AuditEvent"/> (spec FR-009). No Google port
/// is in the graph. The actor, the role and the UI language come from the session, never from the request (VR-002).
/// </remarks>
public sealed class ExportJournalCommand(
    IJournalFieldSource fields,
    IReportTemplateRepository templates,
    SchoolTimeZone schoolTimeZone,
    TimeProvider timeProvider,
    IReportTexts texts,
    IReportRenderer renderer,
    IAuditEventRepository auditEvents,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    IBlankFormRenderer blankFormRenderer)
{
    /// <summary>The <c>orientation</c> request values (OD-004 a), case-sensitive and not trimmed.</summary>
    public const string Portrait = "portrait";

    public const string Landscape = "landscape";

    /// <summary>US-043 spec VR-001: the <c>layout</c> request values, case-sensitive and not trimmed.</summary>
    public const string ProgramLayout = "program";

    public const string GroupLessonsBlankLayout = "group-lessons-blank";

    private readonly ReportBuilder _builder = new(fields, templates, schoolTimeZone, timeProvider);

    public async Task<JournalExportResult> ExecuteAsync(
        JournalExportRequest request,
        long actorId,
        AppRole actorRole,
        CultureInfo uiCulture,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(uiCulture);

        // VR-001: the report page's rules with course and period required, then the orientation — no read yet.
        var selection = ReportRequestReader.Read(
            new ReportRequest(Values(request.Template), Values(request.CourseId), Values(request.From), Values(request.To), Values(request.Names)),
            default,
            default,
            required: true);
        var errors = selection.Messages.Select(m => new ExportFieldError(FieldOf(m), KeyOf(m))).ToList();
        if (!TryReadOrientation(request.Orientation, out var orientation))
        {
            errors.Add(new ExportFieldError(ExportField.Orientation, ExportMessageKey.OrientationInvalid));
        }

        // US-043 VR-001, api-design §2.4: the layout, then — only with the form — the two per-copy members.
        var layoutValid = TryReadLayout(request.Layout, out var layout);
        if (!layoutValid)
        {
            errors.Add(new ExportFieldError(ExportField.Layout, ExportMessageKey.LayoutInvalid));
        }

        var studentsPerCopy = GroupLessonsBlankMapper.StudentsPerCopyMax;
        var lessonsPerCopy = GroupLessonsBlankMapper.LessonsPerCopyMax;
        if (layoutValid && layout == ExportLayout.GroupLessonsBlank)
        {
            if (!TryReadPerCopy(request.StudentsPerCopy, GroupLessonsBlankMapper.StudentsPerCopyMax, out studentsPerCopy))
            {
                errors.Add(new ExportFieldError(ExportField.StudentsPerCopy, ExportMessageKey.StudentsPerCopyInvalid));
            }

            if (!TryReadPerCopy(request.LessonsPerCopy, GroupLessonsBlankMapper.LessonsPerCopyMax, out lessonsPerCopy))
            {
                errors.Add(new ExportFieldError(ExportField.LessonsPerCopy, ExportMessageKey.LessonsPerCopyInvalid));
            }
        }

        if (errors.Count > 0)
        {
            return new JournalExportResult(JournalExportOutcome.Invalid, null, errors);
        }

        // FR-003, FR-005: existence and the build of the page's own report.
        var courses = await fields.GetCoursesAsync(cancellationToken);
        var built = await _builder.BuildAsync(selection, courses, uiCulture, cancellationToken);
        if (built.Report is not { } report)
        {
            return new JournalExportResult(
                JournalExportOutcome.NotFound,
                null,
                built.NotFound.Select(m => new ExportFieldError(FieldOf(m), KeyOf(m))).ToList());
        }

        // US-043 VR-002 (OD-011 a): the sheet limit needs the report's counts; checked before anything is rendered.
        BlankFormExportFacts? blankForm = null;
        if (layout == ExportLayout.GroupLessonsBlank)
        {
            var sheets = GroupLessonsBlankMapper.SheetCount(
                GroupLessonsBlankMapper.StudentCount(report), GroupLessonsBlankMapper.LessonCount(report), studentsPerCopy, lessonsPerCopy);
            if (sheets > GroupLessonsBlankMapper.SheetLimit)
            {
                return new JournalExportResult(JournalExportOutcome.TooManySheets, null, []);
            }

            blankForm = new BlankFormExportFacts(FillMethod.Basic, studentsPerCopy, lessonsPerCopy, sheets);
        }

        // FR-004, FR-006 (US-043 FR-005, FR-007): the file is rendered in memory; nothing is written to disk. With the
        // form the orientation is validated but not applied (US-043 I-8).
        var content = layout == ExportLayout.GroupLessonsBlank
            ? await blankFormRenderer.RenderAsync(
                GroupLessonsBlankMapper.Map(report, texts, uiCulture, studentsPerCopy, lessonsPerCopy), cancellationToken)
            : await renderer.RenderAsync(ReportWorkbookMapper.Map(report, texts, uiCulture, orientation), cancellationToken);
        var fileName = JournalExportFileName.Build(
            report.Header.CourseName, report.Header.From, report.Header.To, texts.Get(ReportText.CourseFallback));

        // FR-010, api-design §2.8: the audit row is committed before the file is returned; an export is never
        // delivered unaudited.
        auditEvents.Add(AuditEvent.JournalExported(
            actorId,
            actorRole,
            selection.CourseId!.Value,
            report.Header.From,
            report.Header.To,
            selection.CreatedTemplateId,
            report.Grading?.Rows.Count ?? 0,
            ExportFormat.Xlsx,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var summary = new JournalExportSummary(
            selection.Template,
            selection.CourseId.Value,
            report.Header.From,
            report.Header.To,
            report.NameSource,
            report.NameSourceOrigin,
            report.Grading?.Rows.Count ?? 0,
            report.LessonTopics?.Rows.Count ?? 0)
        {
            Layout = layout,
            BlankForm = blankForm,
        };
        return new JournalExportResult(JournalExportOutcome.Exported, new JournalExportFile(content, fileName), [], summary);
    }

    /// <summary>An absent JSON member is an absent parameter; a present one is one occurrence (api-design §2.3).</summary>
    private static IReadOnlyList<string?> Values(string? value) => value is null ? [] : [value];

    /// <summary>OD-004 a, VR-001: absent or empty is portrait; otherwise exactly one of the two values.</summary>
    private static bool TryReadOrientation(string? value, out PageOrientation orientation)
    {
        orientation = PageOrientation.Portrait;
        switch (value)
        {
            case null or "" or Portrait:
                return true;
            case Landscape:
                orientation = PageOrientation.Landscape;
                return true;
            default:
                return false;
        }
    }

    /// <summary>US-043 VR-001: absent or empty is the program layout; otherwise exactly one of the two values.</summary>
    private static bool TryReadLayout(string? value, out ExportLayout layout)
    {
        layout = ExportLayout.Program;
        switch (value)
        {
            case null or "" or ProgramLayout:
                return true;
            case GroupLessonsBlankLayout:
                layout = ExportLayout.GroupLessonsBlank;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// US-043 VR-001, I-12, api-design §2.3: absent or JSON <c>null</c> is the maximum (the default); otherwise a JSON
    /// number with a whole value from 1 to <paramref name="max"/>. A string, a boolean, an array or an object is invalid.
    /// </summary>
    private static bool TryReadPerCopy(JsonElement? value, int max, out int perCopy)
    {
        perCopy = max;
        if (value is not { } element || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Number
            || !element.TryGetDecimal(out var number)
            || number != decimal.Truncate(number)
            || number < 1
            || number > max)
        {
            return false;
        }

        perCopy = (int)number;
        return true;
    }

    private static ExportField FieldOf(ReportMessageKey key) => key switch
    {
        ReportMessageKey.TemplateMalformed or ReportMessageKey.TemplateNotFound => ExportField.Template,
        ReportMessageKey.CourseMalformed or ReportMessageKey.CourseUnknown => ExportField.CourseId,
        ReportMessageKey.FromMalformed => ExportField.From,
        ReportMessageKey.ToMalformed or ReportMessageKey.PeriodInverted => ExportField.To,
        ReportMessageKey.NameSourceMalformed => ExportField.Names,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    private static ExportMessageKey KeyOf(ReportMessageKey key) => key switch
    {
        ReportMessageKey.TemplateMalformed => ExportMessageKey.TemplateMalformed,
        ReportMessageKey.TemplateNotFound => ExportMessageKey.TemplateNotFound,
        ReportMessageKey.CourseMalformed => ExportMessageKey.CourseMalformed,
        ReportMessageKey.CourseUnknown => ExportMessageKey.CourseUnknown,
        ReportMessageKey.FromMalformed => ExportMessageKey.FromMalformed,
        ReportMessageKey.ToMalformed => ExportMessageKey.ToMalformed,
        ReportMessageKey.PeriodInverted => ExportMessageKey.PeriodInverted,
        ReportMessageKey.NameSourceMalformed => ExportMessageKey.NameSourceMalformed,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };
}
