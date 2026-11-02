---
artifact_type: entity_model
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-02T20:27:28Z
updated_at: 2026-11-02T20:27:28Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
supersedes: null
---

# US-070 Entity Model — ports and read records

No entity changes (`docs/designs/database/US-070-db-design.md` §1). What changes is the read side. These signatures are verified only when the skeleton built from them compiles.

## 1. Ports (`Application/Ports`)

`string? search` is the validated text (VR-001), null with no search. It is whole, not split: the words are the Infrastructure helper's business.

```csharp
public interface IMeetCodesReadSource
{
    Task<MeetCodeListCounts> GetCountsAsync(string? search, CancellationToken cancellationToken);

    Task<PagedRows<UnassignedCodeRow>> GetUnassignedPageAsync(
        int page, int size, TimeZoneInfo zone, string? search, CancellationToken cancellationToken);

    Task<PagedRows<LinkedCodeRow>> GetLinkedPageAsync(int page, int size, string? search, CancellationToken cancellationToken);

    Task<PagedRows<MarkedCodeRow>> GetMarkedPageAsync(int page, int size, string? search, CancellationToken cancellationToken);

    // GetAllCoursesAsync and GetCourseAbsencesAsync are unchanged.
}

public interface ITeacherListSource
{
    Task<int> CountTeachersAsync(CourseState? state, string? search, CancellationToken cancellationToken);

    Task<IReadOnlyList<TeacherListRecord>> GetTeacherPageAsync(
        CourseState? state, string? search, int offset, int limit, CancellationToken cancellationToken);
}
```

`ICourseListSource` keeps its signatures: `CourseListFilter.SearchText` already carries the text.

## 2. Read records (`Application/Models`)

`IReadOnlyList<string> OrganizerEmails` goes right after `MeetingCode`, as on `UnassignedCodeRow`:

```csharp
public sealed record LinkedCodeRow(
    string MeetingCode,
    IReadOnlyList<string> OrganizerEmails,
    long CourseId,
    string CourseName,
    string? CourseSection,
    bool LinkedAutomatically,
    AccountRef? LinkedBy,
    DateTimeOffset LinkedAt,
    AccountRef? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    int MeetingCount,
    DateTimeOffset? LastStartedAt);

public sealed record MarkedCodeRow(
    string MeetingCode,
    IReadOnlyList<string> OrganizerEmails,
    AccountRef MarkedBy,
    DateTimeOffset MarkedAt,
    int MeetingCount,
    DateTimeOffset? LastStartedAt);
```

## 3. The shared match (`Infrastructure/Persistence/SearchMatch.cs`)

Internal to Infrastructure; US-071 reuses it. The shape is a static class with one public type per file. The exact members are the implementer's; these responsibilities are fixed by db-design §2:

| Responsibility | Rule |
|---|---|
| Split a text into words | any white space, no empty word |
| Build "word found in this string field" as an expression | fold on both sides, not-null test, the word as a parameter, never a constant |
| Build the same for a Meet code | hyphens removed from both sides; no term when the word has none left |
| Combine | OR over a record's fields per word; AND across words |

## 4. Mapping

| Business concept | Stored in | Shown as (API DTO) |
|---|---|---|
| A course's name, section | `course.name`, `course.section` | course list row; `CourseOption` |
| A teacher's surname, given name, email | `classroom_participant` | teacher list row |
| A Meet code | `meet_session.meeting_code`, `meeting_code_link.meeting_code` | `meetingCode` |
| An organizer's email | `meet_session.organizer_email`, null once erased or excluded | `organizerEmails` on all three items |
| A code's linked course | `meeting_code_link.course_id` → `course` | `LinkedCodeItem.course` |

Application maps `OrganizerEmails` to the DTOs of `US-070-openapi.yaml`; domain entities stay out of every view model (AD-8).
