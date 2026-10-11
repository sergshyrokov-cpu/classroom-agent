namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>View DTO of report 4 (US-033 openapi MeetReportMeetingPageModel).</summary>
public sealed record MeetReportMeetingPageModel(
    MeetReportBackLink BackLink,
    MeetReportMeetingMessageKey? MessageKey,
    MeetingDetail? Meeting,
    MeetMeetingExportAction? Export = null);
