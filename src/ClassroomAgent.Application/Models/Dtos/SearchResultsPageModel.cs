namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 entity model §3: the results page of the header field.</summary>
public sealed record SearchResultsPageModel(
    SearchResultsState State,
    string? SearchText,
    SearchRefusalKey? Refusal,
    string ReturnPath,
    SearchResultsGroup<StudentSearchRow> Students,
    SearchResultsGroup<TeacherSearchRow> Teachers,
    SearchResultsGroup<CourseSearchRow> Courses,
    MeetCodeSearchGroup MeetCodes);
