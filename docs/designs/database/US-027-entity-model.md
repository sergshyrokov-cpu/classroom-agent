---
artifact_type: entity_model
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T19:40:00Z
updated_at: 2026-10-04T19:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
supersedes: null
---

# US-027 Entity Model — Report templates

Design rationale and every constraint: `docs/designs/database/US-027-db-design.md`.
Signatures are indicative where marked; names of entities, properties and enum
members are fixed by this stage.

## 1. New Domain types

### 1.1 Enums (`ClassroomAgent.Domain.Enums`)

| Enum | Members (stored code) | Source |
|---|---|---|
| `ReportView` | `Full` (`full`), `Short` (`short`) | FR-003, BR-057 |
| `ReportScaleMode` | `None` (`none`), `Ranges` (`ranges`) | FR-015 |
| `ReportMarkKind` | `Program` (`program`), `Own` (`own`), `Empty` (`empty`) | FR-003, VR-003 |
| `ReportLateMarkKind` | `Program` (`program`), `Own` (`own`), `Hidden` (`hidden`) | FR-003, VR-003 |
| `ReportCellState` | `TurnedInNotGraded`, `ReturnedWithoutGrade`, `TurnedIn`, `Returned`, `NotTurnedIn`, `NotDueYet`, `NotTurnedInNoDueDate`, `NotAssigned`, `Unrecognised` (snake-case codes, db-design §2.2) | FR-003; openapi `ReportCellState` |

Stored as lower-case codes through explicit value converters in the entity
configurations (the `CourseWorkConfiguration` / `AuditEventConfiguration`
pattern), never as integers.

### 1.2 Values (`ClassroomAgent.Domain.Rules`)

```csharp
public sealed record ReportMark(ReportMarkKind Kind, string? Text);          // Text set iff Kind == Own
public sealed record ReportLateMark(ReportLateMarkKind Kind, string? Text);  // Text set iff Kind == Own
public sealed record ReportScaleRow(int FromPercent, int ToPercent, string Label);

public sealed record ReportTemplateSettings(
    ReportView View,
    bool HideMaterials,
    int HoursPerLesson,
    ReportScaleMode ScaleMode,
    IReadOnlyList<ReportScaleRow> ScaleRows,                          // empty iff ScaleMode == None
    IReadOnlyDictionary<ReportCellState, ReportMark> Marks,           // exactly the nine states
    ReportLateMark LateMark);
```

`ReportTemplateSettings` is what both a created template and the built-in one
are: the report builder in `Application` takes settings, never an entity, so it
treats both alike (spec FR-005, FR-006).

- `BuiltInReportTemplates.AcademicJournal` — a static `ReportTemplateSettings`
  with the values of spec FR-006 (short, materials hidden, 12-point preset,
  2 hours, `NotAssigned` → `Own "—"`, every other state `Empty`, late `Hidden`).
  Its translated name is `Application`'s business; its key `academic-journal`
  is api-design §2.2.
- `TwelvePointScale.Rows` — the twelve `ReportScaleRow`s of spec FR-015 /
  OD-002, labels `"1"` … `"12"`. Used by the built-in template and offered to the
  form as the preset (api-design §2.8).

Both are code, never rows (spec I-1).

## 2. New entities (`ClassroomAgent.Domain.Entities`)

### 2.1 `ReportTemplate` — aggregate root, table `report_template`

```csharp
public sealed class ReportTemplate
{
    public const int MaxNameLength = 100;

    public long Id { get; private set; }
    public string Name { get; private set; }               // trimmed, as written
    public string NormalizedName { get; private set; }     // Name.ToUpperInvariant()
    public long AuthorId { get; private set; }             // AppUser.Id; no navigation, no FK
    public ReportView View { get; private set; }
    public bool HideMaterials { get; private set; }
    public int HoursPerLesson { get; private set; }
    public ReportScaleMode ScaleMode { get; private set; }
    public ReportLateMarkKind LateMarkKind { get; private set; }
    public string? LateMarkText { get; private set; }
    public IReadOnlyList<ReportTemplateMark> Marks { get; }          // backing field `marks`
    public IReadOnlyList<ReportTemplateScaleRow> ScaleRows { get; }  // backing field `scaleRows`
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ReportTemplate Create(string name, ReportTemplateSettings settings, long authorId);
    public void Change(string name, ReportTemplateSettings settings);
    public ReportTemplateSettings ToSettings();
}
```

- `Create` and `Change` trim the name, set `NormalizedName`, and apply the
  settings. They **throw `ArgumentException`** on a broken invariant — a
  programming error, because `Application` validates the form first (spec §6):
  empty or over-long name; not exactly the nine states; `Own` without text or
  text with another kind; hours outside 1–10; `None` with rows or `Ranges`
  without; scale rows that do not cover 0–100 without gap or overlap; a label
  empty or over 10 characters; a mark text over 30.
- `Change` keeps `AuthorId` (FR-009), updates each `ReportTemplateMark` in place
  by state, and replaces `ScaleRows` wholesale (db-design §2.4).
- `ToSettings` gives the settings the report builder and the copy/change forms
  need, scale rows ordered by `FromPercent`.
- `authorId` must be positive; it comes from the session (VR-008).

### 2.2 `ReportTemplateMark` — table `report_template_mark`

```csharp
public sealed class ReportTemplateMark
{
    public long Id { get; private set; }
    public long ReportTemplateId { get; private set; }
    public ReportCellState State { get; private set; }
    public ReportMarkKind Kind { get; private set; }
    public string? Text { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
```

Created and changed only through `ReportTemplate` (internal factory / setter).

### 2.3 `ReportTemplateScaleRow` — table `report_template_scale_row`

```csharp
public sealed class ReportTemplateScaleRow
{
    public long Id { get; private set; }
    public long ReportTemplateId { get; private set; }
    public int FromPercent { get; private set; }   // smallint
    public int ToPercent { get; private set; }     // smallint
    public string Label { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
```

Created only through `ReportTemplate`.

### 2.4 Mapping (`Infrastructure/Persistence/Configurations`)

One configuration class per entity (PC-4): `ReportTemplateConfiguration`,
`ReportTemplateMarkConfiguration`, `ReportTemplateScaleRowConfiguration`.
`HasMany(...).WithOne().HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)`
for both collections, navigations accessed through their backing fields; every
length, nullability, index name and check constraint as db-design §2. The model
was built in a scratch project with EF Core 10.0.4 / Npgsql 10.0.3 and the
snake-case convention; table, column, index and FK names came out as designed.

## 3. Changed entities

### 3.1 `CourseWork`

```csharp
/// <summary>Google's scheduledTime, stored as given (US-027 FR-020, OD-004 a). The lesson date's first source.</summary>
public DateTimeOffset? ScheduledTime { get; private set; }
```

`CourseWorkDetails` gains a `ScheduledTime` parameter (positional record —
every caller is updated: `GoogleClassroomReader` passes the value it already
reads; test fixtures pass theirs). `Apply` assigns it. Mapped as
`builder.Property(c => c.ScheduledTime);` → `scheduled_time timestamptz NULL`.
`Kind`, `ItemDate` and the guards are unchanged. No `LessonDate` property: the
rule lives in the port's query (db-design §5.1).

### 3.2 `AuditAction`, `AuditTargetType`

Members of db-design §4, each with its XML summary citing US-027 FR-016, and the
matching cases in `AuditEventConfiguration`'s code maps and checks.

### 3.3 `AppUser`, `AuditEvent`, the purge

Unchanged. `RetentionPurgeStore` gains no step (db-design §2.1).

## 4. Ports and records (`ClassroomAgent.Application`)

```csharp
public interface IJournalFieldSource                       // Ports; F1 … F4 of db-design §5.1
{
    Task<IReadOnlyList<JournalCourseRecord>> GetCoursesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<JournalLessonRecord>> GetLessonsAsync(long courseId, DateTimeOffset start, DateTimeOffset endExclusive, CancellationToken cancellationToken);
    Task<IReadOnlyList<JournalCourseMemberRecord>> GetMembersAsync(long courseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<JournalSubmissionRecord>> GetLessonSubmissionsAsync(long courseId, DateTimeOffset start, DateTimeOffset endExclusive, CancellationToken cancellationToken);
}

public interface IReportTemplateRepository                 // Ports; T1 … T3
{
    Task<IReadOnlyList<ReportTemplateListRecord>> ListAsync(CancellationToken cancellationToken);
    Task<ReportTemplate?> GetAsync(long id, bool forUpdate, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string normalizedName, long? exceptId, CancellationToken cancellationToken);
    void Add(ReportTemplate template);
    void MarkChanged(ReportTemplate template);             // root entry → Modified (db-design §2.4)
    void Remove(ReportTemplate template);
}
```

- `JournalCourseRecord` and `JournalSubmissionRecord` are reused from US-025.
- `JournalLessonRecord(long Id, CourseWorkResource Resource, string Title, decimal? MaxPoints, DateTimeOffset? DueAt, DateTimeOffset LessonDate)` — new; `Kind` is derived in `Application` by the BR-052 rule `CourseWork.Kind` uses.
- `JournalCourseMemberRecord(long ParticipantId, ClassroomRole Role, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, bool OnRoster, string? FullName, string? Email)` — new.
- `ReportTemplateListRecord(long Id, string Name, DateTimeOffset UpdatedAt, string? AuthorEmail)` — new; null email = author account deleted.
- `UniqueReportTemplateNameViolationException` (`Application.Exceptions`) —
  thrown by `UnitOfWork` for `uq_report_template_normalized_name` only.

Record and method names are indicative; their content and the query count are
fixed.

## 5. Mapping to business concepts

| Element | Business concept | Source |
|---|---|---|
| `ReportTemplate` | a report template created in the school | `trebovaniya.md` §3 `ReportTemplate` (v84), v83 school-own data |
| `Name` | the template's name, school text | FR-003, VR-001 |
| `AuthorId` | who created it (a copy: who copied it) | FR-014 |
| `View` | full / short | BR-057 |
| `HideMaterials` | materials hidden in Grading | FR-003, FR-005.3 |
| `HoursPerLesson` | hours in Lesson topics | FR-005.6 |
| `ScaleMode`, `ScaleRows` | the grading scale | FR-015, OD-002 |
| `Marks` | what each cell state is shown as | FR-003, BR-056 |
| `LateMarkKind`, `LateMarkText` | the late mark | FR-003, FR-005.4 |
| `UpdatedAt` | the date of its last change | FR-002 |
| `BuiltInReportTemplates.AcademicJournal` | the built-in "Academic journal" | FR-006, OD-003 |
| `CourseWork.ScheduledTime` | Google's planned publication time | FR-020, OD-004 |
| `JournalLessonRecord.LessonDate` | the lesson date = publication date | OD-001 a, FR-004 |

## 6. Mapping to API DTOs (openapi v1)

| DTO / form field | Entity / record side | Mapped in |
|---|---|---|
| `ReportTemplateListItem.reference` | `ReportTemplate.Id` as decimal string, or `academic-journal` | `Application` |
| `.name` | `ReportTemplateListRecord.Name` | `Application` |
| `.authorState`, `.authorEmail` | `AuthorEmail` null → `AccountDeleted`, else `Known` + email | `Application` |
| `.changedAt` | `UpdatedAt` → school time zone | `Application` |
| `.isBuiltIn`, `.canChange` | true/false for the built-in; false/true otherwise | `Application` |
| `ReportTemplateForm.name` | `Create` / `Change` `name` | `Application` |
| `.view`, `.hideMaterials`, `.hoursPerLesson`, `.scaleMode` | `ReportTemplateSettings` members | `Application` (string → enum / bool / int after VR-002, VR-004, VR-005) |
| `.scale[i].from/to/label` | `ReportScaleRow`, sorted by `from` | `Application` |
| `.marks[<State>].kind/text` | `Marks[ReportCellState]` → `ReportMark` | `Application` |
| `.lateMark.kind/text` | `ReportLateMark` | `Application` |
| `Report` and its parts | built from `ReportTemplateSettings` + `IJournalFieldSource` records | `Application` |

No entity crosses into `Web` (AD-8); `Web` binds `ReportTemplateForm` (strings)
and renders DTOs only.
