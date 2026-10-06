using ClassroomAgent.Application.Localization;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Models.Dtos;
using Microsoft.Extensions.Localization;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// US-028 entity model §3.2: the program text of an export, through the installation's translations, in the current UI culture.
/// </summary>
public sealed class LocalizedReportTexts(IStringLocalizer<SharedResource> localizer) : IReportTexts
{
    public string Get(ReportText text)
    {
        var key = text switch
        {
            ReportText.SheetGrading => JournalExportTextKeys.SheetGrading,
            ReportText.SheetLessonTopics => JournalExportTextKeys.SheetLessonTopics,
            ReportText.HeaderTemplate => JournalExportTextKeys.HeaderTemplate,
            ReportText.HeaderCourse => JournalExportTextKeys.HeaderCourse,
            ReportText.HeaderPeriod => ReportTemplateTextKeys.ReportPeriod,
            ReportText.HeaderTeachers => ReportTemplateTextKeys.ReportTeachers,
            ReportText.BuiltInTemplateName => ReportTemplateTextKeys.BuiltInName,
            ReportText.StudentHeading => JournalTextKeys.StudentHeader,
            ReportText.Material => JournalTextKeys.Material,
            ReportText.Draft => JournalTextKeys.Draft,
            ReportText.TurnedInOn => JournalTextKeys.TurnedInOn,
            ReportText.StudentUnnamed => JournalTextKeys.Unnamed,
            ReportText.TeacherUnnamed => ReportTemplateTextKeys.TeacherUnnamed,
            ReportText.TopicDate => ReportTemplateTextKeys.ReportDate,
            ReportText.TopicTitle => ReportTemplateTextKeys.ReportTopic,
            ReportText.TopicHours => ReportTemplateTextKeys.ReportHours,
            ReportText.TopicTeacher => ReportTemplateTextKeys.ReportTeacher,
            ReportText.TopicIndependentWork => ReportTemplateTextKeys.ReportIndependentWork,
            ReportText.TopicSignature => ReportTemplateTextKeys.ReportSignature,
            ReportText.NothingPublished => ReportTemplateTextKeys.Empty(ReportEmptyStateKey.NothingPublished),
            ReportText.NoStudents => ReportTemplateTextKeys.NoStudents,
            ReportText.CourseFallback => JournalExportTextKeys.CourseFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(text), text, null),
        };

        return localizer[key];
    }

    public string ProgramMark(string programKey) => localizer[ReportTemplateTextKeys.ProgramMark(programKey)];
}
