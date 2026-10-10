using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The translation keys of the Meet meetings page (US-032 spec FR-017; api-design §5). The openapi enums map to one key
/// family each. Every key exists in both the Ukrainian and the English file.
/// </summary>
public static class MeetCodesTextKeys
{
    public const string NavigationEntry = "MeetCodes.NavigationEntry";

    public const string Title = "MeetCodes.Title";

    public const string NoCandidates = "MeetCodes.NoCandidates";

    public const string Automatically = "MeetCodes.Automatically";

    public const string DeletedAccount = "MeetCodes.DeletedAccount";

    public const string NotConfirmed = "MeetCodes.NotConfirmed";

    public const string ActionPickCourse = "MeetCodes.Action.PickCourse";

    public const string ActionConfirm = "MeetCodes.Action.Confirm";

    public const string ActionRelink = "MeetCodes.Action.Relink";

    public const string ActionMarkNotACourse = "MeetCodes.Action.MarkNotACourse";

    public const string ChoiceTitle = "MeetCodes.Choice.Title";

    public const string ChoiceCandidates = "MeetCodes.Choice.Candidates";

    public const string ChoiceOtherCourses = "MeetCodes.Choice.OtherCourses";

    public const string ChoiceSubmit = "MeetCodes.Choice.Submit";

    public const string ChoiceCurrent = "MeetCodes.Choice.Current";

    public const string ChoiceCancel = "MeetCodes.Choice.Cancel";

    public const string ColumnCode = "MeetCodes.Column.Code";

    public const string ColumnOrganizers = "MeetCodes.Column.Organizers";

    public const string ColumnFirstMeeting = "MeetCodes.Column.FirstMeeting";

    public const string ColumnLastMeeting = "MeetCodes.Column.LastMeeting";

    public const string ColumnMeetings = "MeetCodes.Column.Meetings";

    public const string ColumnParticipants = "MeetCodes.Column.Participants";

    public const string ColumnCandidates = "MeetCodes.Column.Candidates";

    public const string ColumnCourse = "MeetCodes.Column.Course";

    public const string ColumnMadeBy = "MeetCodes.Column.MadeBy";

    public const string ColumnMadeAt = "MeetCodes.Column.MadeAt";

    public const string ColumnConfirmedBy = "MeetCodes.Column.ConfirmedBy";

    public const string ColumnMarkedBy = "MeetCodes.Column.MarkedBy";

    public const string ColumnMarkedAt = "MeetCodes.Column.MarkedAt";

    public const string ColumnActions = "MeetCodes.Column.Actions";

    public const string PaginationPrevious = "MeetCodes.Pagination.Previous";

    public const string PaginationNext = "MeetCodes.Pagination.Next";

    public static string ListName(MeetCodeList list) => "MeetCodes.List." + list;

    public static string Empty(MeetCodeList list) => "MeetCodes.Empty." + list;

    public static string Message(MeetCodesMessageKey key) => "MeetCodes.Message." + key;

    public static string FieldError(MeetCodeFieldError key) => "MeetCodes.FieldError." + key;
}
