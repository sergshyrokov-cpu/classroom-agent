---
artifact_type: entity_model
story: US-071
version: 1
status: DRAFT
created_at: 2026-11-03T16:55:20Z
updated_at: 2026-11-03T16:56:18Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/designs/api/US-071-api-design.md
    version: 1
  - path: docs/designs/api/US-071-openapi.yaml
    version: 1
  - path: docs/designs/database/US-071-db-design.md
    version: 1
supersedes: null
---

# US-071 Entity Model — ports and read records

No entity changes (`docs/designs/database/US-071-db-design.md` §1). What is new is the read side. These signatures are verified only once TEST_WRITING's skeleton compiles (standing rule in `test-writer`).

## 1. Ports (`Application/Ports`)

`string search` is the validated text (VR-001), at least 2 characters (VR-003), whole and not split: the words are the Infrastructure helper's.

```csharp
/// <summary>New. The students of the header search; implemented by Infrastructure's StudentSearchSource.</summary>
public interface IStudentSearchSource
{
    Task<int> CountStudentsAsync(string search, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentSearchRecord>> GetStudentsAsync(string search, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentSearchCourseRecord>> GetStudentCoursesAsync(
        IReadOnlyCollection<long> participantIds, CancellationToken cancellationToken);
}

public interface IMeetCodesReadSource
{
    // Existing members are unchanged. New:
    Task<IReadOnlyList<MeetCodeMatch>> GetFirstMatchesAsync(string search, int limit, CancellationToken cancellationToken);
}
```

`ITeacherListSource` and `ICourseListSource` keep their signatures. Test doubles of `IMeetCodesReadSource` gain the new member (`RecordingMeetCodesReadSource`).

## 2. Read records (`Application/Models`)

```csharp
public sealed record StudentSearchRecord(long ParticipantId, string? Surname, string? GivenName, string? Email);

public sealed record StudentSearchCourseRecord(
    long ParticipantId,
    long CourseId,
    string Name,
    string? Section,
    bool OnRoster,
    bool SeenOnce,
    DateTimeOffset LastSeenAt);

public sealed record MeetCodeMatch(string Code, MeetCodeList List);
```

`MeetCodeList` is the existing `Application.Models` enum. One public type per file.

## 3. Page model (`Application/Models/Dtos`)

The openapi's `SearchResultsPageModel` and rows, in C#. A row's label is `CoursePersonNameRule.NameOf(surname, givenName, email).Text`, the rule of the teacher list; a matched person has a name or an email, so it is never null, and the mapping uses `?? string.Empty` only to satisfy the nullable check.

```csharp
public enum SearchResultsState { Prompt, Refused, Results }

public sealed record SearchResultsPageModel(
    SearchResultsState State,
    string? SearchText,
    SearchRefusalKey? Refusal,
    string ReturnPath,
    SearchResultsGroup<StudentSearchRow> Students,
    SearchResultsGroup<TeacherSearchRow> Teachers,
    SearchResultsGroup<CourseSearchRow> Courses,
    MeetCodeSearchGroup MeetCodes);

public sealed record SearchResultsGroup<TRow>(IReadOnlyList<TRow> Rows, int Total);

public sealed record MeetCodeSearchGroup(
    IReadOnlyList<MeetCodeMatch> Rows, int Total, MeetCodeList? FirstListWithMatch);

public sealed record StudentSearchRow(
    long ParticipantId, string Label, string? Email, IReadOnlyList<StudentSearchCourse> Courses);

public sealed record StudentSearchCourse(long CourseId, string Name, string? Section, LeftMark? Left);

public sealed record TeacherSearchRow(long ParticipantId, string Label, string? Email);

public sealed record CourseSearchRow(long CourseId, string Name, string? Section, CourseState State);
```

`CourseSearchRow.State` is the `CourseState` of `CourseListRow`; the view words it as the course list's row does. `OpenAPI MeetCodeSearchRow` is `MeetCodeMatch`: it holds exactly the code and the list.

`SearchRefusalKey` and `LeftMark` exist.

## 4. Application and host

```csharp
public sealed record SearchResultsRequest(IReadOnlyList<string?> Q);

public sealed class GetSearchResultsQuery(
    IStudentSearchSource students,
    ITeacherListSource teachers,
    ICourseListSource courses,
    IMeetCodesReadSource meetCodes,
    SchoolTimeZone schoolTimeZone)
{
    public const int GroupSize = 20;

    public const int MinimumLength = 2;

    public Task<SearchResultsPageModel> ExecuteAsync(SearchResultsRequest request, CancellationToken cancellationToken);
}

public static class SearchResultsUrl
{
    public const string Path = "/workspace/search";

    public static string Build(string? searchText);
}
```

`DataMarkRules` is `internal` to Application, which is where the mapping runs. `FirstListWithMatch` comes from `MeetCodeListCounts`: the first of Unassigned, Linked, NotACourse with a count above 0.

Registered in `InstallationServices` as the other sources and queries are (`AddScoped`).

## 5. Mapping to business concepts and DTOs

| Concept | Stored as | Read record | DTO |
|---|---|---|---|
| A student found (BR-088) | `classroom_participant` + a student `course_membership` | `StudentSearchRecord` | `StudentSearchRow` |
| A course of that student, left or not (BR-068) | `course_membership` + `course` | `StudentSearchCourseRecord` | `StudentSearchCourse` with `LeftMark` |
| A teacher found | as the teacher list | `TeacherListRecord` | `TeacherSearchRow` |
| A course found | as the course list | `CourseListCourseRecord` | `CourseSearchRow` |
| A Meet code found | as the Meet code lists | `MeetCodeMatch` | `MeetCodeSearchGroup.Rows` |
