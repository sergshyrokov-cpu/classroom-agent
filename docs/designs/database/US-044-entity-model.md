---
artifact_type: entity_model
story: US-044
version: 1
status: DRAFT
created_at: 2026-10-11T07:10:42Z
updated_at: 2026-10-11T07:10:42Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/designs/api/US-044-api-design.md
    version: 1
  - path: docs/designs/api/US-044-openapi.yaml
    version: 1
  - path: docs/designs/database/US-044-db-design.md
    version: 1
supersedes: null
---

# US-044 Entity Model — Export the built-in Meet reports to Excel

The C# shapes below are indicative signatures. They are verified once the
compile-only skeleton built at TEST_WRITING compiles; a mismatch found there is
recorded as an Open Decision, as in US-028 and US-033.

## 1. Domain — `AuditEvent` (changed)

### 1.1 `AuditAction` (enum, `Domain/Enums/AuditAction.cs`)

Two members appended (the list grows in the Story that performs the action):

```csharp
/// <summary>US-044 spec FR-013: reports 1–3 of one course and period were exported to a file.</summary>
MeetReportsExported,
/// <summary>US-044 spec FR-013: report 4 of one meeting was exported to a file.</summary>
MeetMeetingExported,
```

### 1.2 Properties (`Domain/Entities/AuditEvent.cs`)

| Property | Type | Column | Business concept |
|---|---|---|---|
| `ExportMeetingId` | `long?` | `export_meeting_id` | the meeting whose report 4 was exported |
| `ExportMeetings` | `int?` | `export_meetings` | how many meetings the course workbook listed |

Private setters, as the other export properties.

### 1.3 Factories

```csharp
public static AuditEvent MeetReportsExported(
    long actorId, AppRole actorRole, long courseId, DateOnly from, DateOnly to,
    int studentRows, int meetingRows, ExportFormat format,
    DateTimeOffset occurredAt, string? requestId);

public static AuditEvent MeetMeetingExported(
    long actorId, AppRole actorRole, long courseId, long meetingId,
    int personRows, ExportFormat format,
    DateTimeOffset occurredAt, string? requestId);
```

Both set `ActorType = AppUser`, `TargetType = Course`, `TargetId = courseId`,
`Outcome = Succeeded`, and the columns of db-design §3.2. Guards as
`JournalExported`: positive ids, non-negative counts, `from <= to` — a
violation is a programming error (`ArgumentOutOfRangeException`), never an
expected outcome (AD-9).

## 2. Application — export use cases

### 2.1 Requests (`Application/Models/Requests`)

```csharp
public sealed record MeetReportsExportRequest(string? CourseId, string? From, string? To, string? Orientation);
public sealed record MeetMeetingExportRequest(string? CourseId, string? MeetingId, string? Orientation);
```

Strings, as `JournalExportRequest`; parsed by the US-033 parsers (spec VR-001,
VR-002) and the orientation parser of US-028 with default `Landscape`
(spec VR-003). The parser takes the default as an argument, so US-028's
default (`Portrait`) is unchanged.

### 2.2 Commands (`Application/UseCases`)

| Command | Reads | Writes |
|---|---|---|
| `ExportMeetReportsCommand` | `GetMeetReportsQuery` | `AuditEvent.MeetReportsExported` |
| `ExportMeetMeetingCommand` | `GetMeetReportMeetingQuery`, `GetLastSynchronizationQuery` (delay line) | `AuditEvent.MeetMeetingExported` |

Each returns an outcome with the cases *file* (bytes, file name), *invalid*
(field errors in the order of the API design), *not found* (field error). Each
declares `PermittedServiceWrite.AuditEvent` in `PermittedServiceWrites`, as
`ExportJournalCommand` (spec FR-012). Order: validate → build → map → render
(`IReportRenderer`) → add audit row → commit → return.

### 2.3 Mapping (`Application`)

Two mappers (indicative `MeetReportsWorkbookMapper`,
`MeetMeetingWorkbookMapper`) turn the page results into `Workbook`, every
program text translated through `IReportTexts` / the localizer (spec FR-010).
They hold the layout of spec FR-004 … FR-007 and OD-013.

### 2.4 File name

`JournalExportFileName.Build(courseName, from, to, fallback)` keeps its
signature and output. Its cleaning of the course part is reused by a sibling
(indicative `MeetExportFileName`) with two forms:

```csharp
public static string BuildForPeriod(string courseName, DateOnly from, DateOnly to, string fallback);   // "Meet <course> <from>–<to>.xlsx"
public static string BuildForMeeting(string courseName, DateTime localStart, string fallback);         // "Meet <course> <yyyy-MM-dd> <HH-mm>.xlsx"
```

The cleaning moves into one shared helper so the two cannot drift; the journal
name is byte-identical (US-028 tests).

### 2.5 Page view models (`Application/Models/Dtos`)

| Record | Change |
|---|---|
| `MeetReportsPageModel` | gains `MeetReportsExportAction? Export` (last positional member) |
| `MeetReportMeetingPageModel` | gains `MeetMeetingExportAction? Export` (last positional member) |

```csharp
public sealed record MeetReportsExportAction(long CourseId, DateOnly From, DateOnly To);
public sealed record MeetMeetingExportAction(long CourseId, long MeetingId);
```

Non-null exactly when the page shows reports / a meeting (API design §2.5).

## 3. Application — workbook model additions (not persisted)

All additions are **opt-in**: a workbook built by US-028 sets none of them and
renders exactly as before (spec FR-007, I-7).

### 3.1 `WorkbookCellKind`

Appended: `Time`, `DateTime`.

### 3.2 `WorkbookCell`

Non-positional init members, so every existing constructor call and factory is
unchanged:

```csharp
public TimeOnly? Time { get; init; }
public DateTime? LocalDateTime { get; init; }   // local wall-clock time of the school, Kind Unspecified
public string? NumberFormat { get; init; }      // e.g. "0.0"; null → Excel's General

public static WorkbookCell OfTime(TimeOnly time);
public static WorkbookCell OfDateTime(DateTime localDateTime);
public static WorkbookCell OfNumber(decimal number, string? numberFormat);   // overload; OfNumber(decimal) unchanged
```

### 3.3 `Worksheet`

```csharp
public bool AutoFilter { get; init; }           // false → no autofilter (journal)
public int? TableLastRowIndex { get; init; }    // inclusive; null → the sheet's last row
```

With `AutoFilter` and a `TableHeaderRowIndex`, the renderer sets an autofilter
over the header row's cells down to `TableLastRowIndex` (spec FR-007: the
"Other participants" line of report 4 stays outside). The existing
`TableHeaderRowIndex` already freezes and repeats the header row.

### 3.4 `Workbook`

```csharp
public string? TimeFormat { get; init; }        // UI-language short time, e.g. "HH:mm"
public string? DateTimeFormat { get; init; }    // UI-language short date + time
```

The renderer throws `InvalidOperationException` when a `Time` or `DateTime`
cell meets a null format — a mapper defect, not user input.

### 3.5 `ClosedXmlReportRenderer` (Infrastructure)

Writes the two new kinds as Excel date values with their formats, applies
`NumberFormat` to a number cell, and the autofilter of §3.3. Nothing else
changes; no ClosedXML type crosses into `Application` (AD-4).

## 4. Mapping to business concepts and DTOs

| Concept (spec) | Model |
|---|---|
| Export of reports 1–3 (FR-005) | `MeetReportsExportRequest` → `ExportMeetReportsCommand` → `Workbook` (3 sheets) → `AuditEvent.MeetReportsExported` |
| Export of report 4 (FR-006) | `MeetMeetingExportRequest` → `ExportMeetMeetingCommand` → `Workbook` (1 sheet) → `AuditEvent.MeetMeetingExported` |
| Export action on the pages (FR-002) | `MeetReportsExportAction`, `MeetMeetingExportAction` |
| Typed cells, filter, freeze, print (FR-007) | §3 |
| File name (FR-008.1) | `MeetExportFileName` |
| Audit (FR-013) | §1, db-design §3 |
| OpenAPI `MeetReportsExportRequest`, `MeetMeetingExportRequest`, `*ExportAction` | §2.1, §2.5 — same members |
