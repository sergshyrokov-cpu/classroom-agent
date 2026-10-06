---
artifact_type: entity_model
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T05:48:00Z
updated_at: 2026-10-06T05:48:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
  - path: docs/designs/api/US-028-api-design.md
    version: 1
  - path: docs/designs/database/US-028-db-design.md
    version: 1
  - path: docs/designs/database/US-042-entity-model.md
    version: 1
supersedes: null
---

# US-028 Entity Model — Export audit, workbook model, renderer and text ports

Design rationale and every constraint: `docs/designs/database/US-028-db-design.md`.
Names of entities, properties, enum members, records and ports are fixed by this
stage; method signatures are indicative where marked. API_DESIGN §8 handed over
four items: the audit details and built-in marker (§1), the workbook model (§2),
the `IReportRenderer` shape (§3.1) and, found here, how `Application` obtains
translated text (§3.2).

## 1. Domain

### 1.1 `AuditEvent` (changed)

```csharp
/// US-028 db-design §3.2; null for every other action.
public DateOnly? ExportPeriodFrom { get; private set; }
public DateOnly? ExportPeriodTo { get; private set; }
public long? ExportTemplateId { get; private set; }
public bool? ExportTemplateBuiltIn { get; private set; }
public int? ExportRows { get; private set; }
public ExportFormat? ExportFormat { get; private set; }

/// US-028 spec FR-010: one row per successful export. templateId null = the built-in template.
public static AuditEvent JournalExported(
    long actorId,
    AppRole actorRole,
    long courseId,
    DateOnly from,
    DateOnly to,
    long? templateId,
    int rows,
    ExportFormat format,
    DateTimeOffset occurredAt,
    string? requestId);
```

The factory rejects (`ArgumentOutOfRangeException`): an undeclared role or
format, `courseId <= 0`, `templateId <= 0`, `from > to`, `rows < 0`. It sets
`ActorType = AppUser`, `Action = JournalExported`, `TargetType = Course`,
`TargetId = courseId`, `Outcome = Succeeded`, `RefusalCategory = null`,
`ExportTemplateBuiltIn = templateId is null`. There is no "refused" factory: a
failed export writes no row (spec FR-005, FR-010).

### 1.2 Enums

| Enum | Change | Code |
|---|---|---|
| `AuditAction` | `+ JournalExported` | `journal_exported` |
| `AuditTargetType` | `+ Course` | `course` |
| `ExportFormat` (new, `Domain.Enums`) | `Xlsx` | `xlsx` |

### 1.3 `AuditEventConfiguration` (Infrastructure, changed)

Six properties mapped explicitly (`HasColumnType("date")` for the two dates,
`HasMaxLength(8)` + converter for the format); converters gain the new codes;
constraints per db-design §3.4–3.5.

## 2. Application — the workbook model (`ClassroomAgent.Application.Models.Export`)

The renderer's whole input (spec FR-004, FR-011). Every string is final —
translated, composed, cut to the Excel limit. The renderer adds no text and
applies no rule beyond the layout of §3.1.

```csharp
public sealed record Workbook(
    IReadOnlyList<Worksheet> Sheets,
    PageOrientation Orientation,
    string DateFormat);              // Excel number format of the UI culture's short date (§2.2)

public sealed record Worksheet(
    string Name,                     // translated, <= 31 chars
    IReadOnlyList<WorksheetRow> Rows,
    int? TableHeaderRowIndex,        // 0-based; repeated on every printed page and frozen; null when no table
    bool RepeatAndFreezeFirstColumn);// "Grading" with a table only (spec FR-004.8)

public sealed record WorksheetRow(IReadOnlyList<WorkbookCell> Cells);

public sealed record WorkbookCell(
    WorkbookCellKind Kind,
    string? Text,                    // Kind Text only
    decimal? Number,                 // Kind Number only
    DateOnly? Date,                  // Kind Date only
    bool IsHeading,                  // bold, wrapped
    bool QuotePrefix);               // spec FR-004.7

public enum WorkbookCellKind { Empty, Text, Number, Date }

public enum PageOrientation { Portrait, Landscape }   // request value portrait / landscape; never stored
```

`WorkbookCell` has static factories `Empty()`, `OfText(string, bool isHeading = false)`,
`OfNumber(decimal)`, `OfDate(DateOnly)`; `OfText` cuts at 32 767 characters
(FR-004.9) and sets `QuotePrefix` when the text starts with `=`, `+`, `-`, `@`,
`\t` or `\r` (FR-004.7). Both rules live there, once.

### 2.1 Mapping `Report` → `Workbook` (`ReportWorkbookMapper`, `Application/UseCases`)

```csharp
// indicative
public static Workbook Map(Report report, IReportTexts texts, CultureInfo uiCulture, PageOrientation orientation);
```

Sheet layout, both sheets (spec FR-004.2):

| Row | Cell A | Cell B |
|---|---|---|
| 0 | label `HeaderTemplate` | built-in → `BuiltInTemplateName`; created → `Header.TemplateName` as written |
| 1 | label `HeaderCourse` | `CourseName`, or `CourseName — CourseSection` (the screen's `CourseTitle`) |
| 2 | label `HeaderPeriod` | `From.ToString("d") – To.ToString("d")` |
| 3 | label `HeaderTeachers` | teachers joined `", "`, unnamed → `TeacherUnnamed` |
| 4 | empty row | |
| 5 | table header, or the empty-state text | |

Labels and heading cells are `IsHeading`. Values are text cells.

"Grading" (FR-004.3, FR-004.5):

- Report `EmptyStateKey = NothingPublished` → row 5 = `NothingPublished` text, no
  table, `TableHeaderRowIndex = null`. Grading with no rows → row 5 = `NoStudents`.
- Header row: `StudentHeading`; then per column one heading cell
  `Title + "\n" + LessonDate.ToString("d")` plus `"\n" + Material` for a material —
  the screen's order.
- Student row: name cell (`DisplayName`, or `StudentUnnamed` when `Unnamed`), then
  per cell:

| Condition | Cell |
|---|---|
| `Content = Empty` and no draft, late or turn-in date | `Empty()` |
| `Content = Grade`, `Grade.Kind = ScaleLabel`, `Label` matches `^(0\|[1-9][0-9]{0,14})$`, `DraftGrade`, `Late`, `TurnedInOn` all null | `OfNumber(decimal.Parse(Label, InvariantCulture))` (OD-003 a) |
| otherwise | `OfText(parts joined "\n")` |

  Parts, in the screen's order: main (grade text / mark text / raw state);
  `Draft + " " + grade text`; late mark text; `TurnedInOn + " " + date("d")`.
  Grade text = scale label, or `points + " / " + max` with the format
  `0.############################` in `uiCulture`. Mark text = own text as
  written, or `ProgramMark(programKey)`. The leading-zero exclusion keeps a
  number cell displaying exactly the screen's text.

"Lesson topics" (FR-004.4): `NothingPublished` → as Grading; otherwise header row
of the six headings, then per row `OfDate(LessonDate)`, `OfText(Title)`,
`OfNumber(Hours)`, `OfText(teachers joined ", ")`, `Empty()`, `Empty()`.
`TableHeaderRowIndex = 5`, `RepeatAndFreezeFirstColumn = false`.

### 2.2 Date format

`DateFormat` = `uiCulture.DateTimeFormat.ShortDatePattern` with `M` → `m`, other
characters kept (e.g. `dd.MM.yyyy` → `dd.mm.yyyy`, `M/d/yyyy` → `m/d/yyyy`;
tests derive the expected value from the runtime culture, not a literal). Used by the renderer for every `Date` cell (spec I-4).

### 2.3 File name (`JournalExportFileName`, `Application/UseCases`)

```csharp
// indicative
public static string Build(string courseName, DateOnly from, DateOnly to, string fallback);
```

Spec FR-006.1 exactly: replace `\ / : * ? " < > |` and control characters with
`_`; collapse white space; trim spaces and dots; cut to 100; empty →
`fallback` (`CourseFallback`); result `"{course} {from:yyyy-MM-dd}–{to:yyyy-MM-dd}.xlsx"`.

## 3. Application — ports and use case

### 3.1 `IReportRenderer` (`Application/Ports`, implemented in `Infrastructure/Export`)

```csharp
public interface IReportRenderer
{
    Task<byte[]> RenderAsync(Workbook workbook, CancellationToken cancellationToken);
}
```

`ClosedXmlReportRenderer` (`Infrastructure/Export`, ClosedXML 0.105.1) writes
the model into a `MemoryStream` and returns its bytes — no file, no temp path.
Fixed layout, no business rule: text via `SetValue(string)` (never `FormulaA1`);
`QuotePrefix` style when set; `Date` cells with `DateFormat`; headings bold and
wrapped, text containing `\n` wrapped; `Orientation`; fit to one page wide;
`TableHeaderRowIndex` repeated on print and frozen; with
`RepeatAndFreezeFirstColumn` column A repeated and frozen too; columns adjusted
to content, capped at 60 characters. No ClosedXML type leaves the class (AD-4).

### 3.2 `IReportTexts` (`Application/Ports`, implemented in `Web`)

`Application` has no localization package and may gain none without an Open
Decision; the text-key constants live in `Web` (`ReportTemplateTextKeys`,
`JournalTextKeys`). So the mapper asks a port, and `Web` answers through
`IStringLocalizer<SharedResource>` in the request's UI culture — the precedent
is `ISynchronizationRequests`, implemented in `Web`.

```csharp
public interface IReportTexts
{
    string Get(ReportText text);
    string ProgramMark(string programKey);   // a ReportCellState member name or "Late", as ReportTemplateTextKeys.ProgramMark
}

public enum ReportText
{
    SheetGrading, SheetLessonTopics,
    HeaderTemplate, HeaderCourse, HeaderPeriod, HeaderTeachers,
    BuiltInTemplateName,
    StudentHeading, Material, Draft, TurnedInOn,
    StudentUnnamed, TeacherUnnamed,
    TopicDate, TopicTitle, TopicHours, TopicTeacher, TopicIndependentWork, TopicSignature,
    NothingPublished, NoStudents,
    CourseFallback,
}
```

`LocalizedReportTexts` (`Web`) maps each member to an **existing** key where one
exists (`ReportPeriod`, `ReportTeachers`, `BuiltInName`, `JournalTextKeys.StudentHeader`,
`Material`, `Draft`, `TurnedInOn`, `Unnamed`, `TeacherUnnamed`, the six topic
headings, `Empty(NothingPublished)`, `NoStudents`, `ProgramMark`), and to new
keys for `SheetGrading`, `SheetLessonTopics`, `HeaderTemplate`, `HeaderCourse`,
`CourseFallback` (spec FR-013). A test asserts every `ReportText` member resolves
in `uk` and `en`.

### 3.3 Reuse of the report query (spec FR-003)

`GetReportQuery` is split, behaviour unchanged (its existing tests guard it):

| Part | Type | Does |
|---|---|---|
| `ReportRequestReader` (`Application/Validation`) | pure | parses `ReportRequest` → `ReportSelection` (template ref, created id, course id, from, to, page name source) + the ordered `ReportMessageKey` list — today's lines before the first read |
| `ReportBuilder` (`Application/UseCases`) | reads | given a valid selection with a course: existence (`TemplateNotFound`, `CourseUnknown`) and the build → `ReportBuildResult(Report? Report, IReadOnlyList<ReportMessageKey> NotFound)` |
| `GetReportQuery` | composes | reader → drop-down reads → builder, as today |

Signatures indicative; the split is the supporting change the Story cannot work
without (one source of content).

### 3.4 `ExportJournalCommand` (`Application/UseCases`)

```csharp
// indicative
public Task<JournalExportResult> ExecuteAsync(
    JournalExportRequest request, long actorId, AppRole actorRole,
    CultureInfo uiCulture, string? requestId, CancellationToken cancellationToken);
```

1. Required `courseId`, `from`, `to` (absent → the field's malformed key);
   `orientation` (`OrientationInvalid`); then `ReportRequestReader` for the rest —
   all keys in the API order (api-design §2.4). Any key → `Invalid`. No read yet.
2. `ReportBuilder` → `NotFound` with its keys.
3. `ReportWorkbookMapper.Map` → `IReportRenderer.RenderAsync`.
4. `AuditEvent.JournalExported(…, rows: report.Grading?.Rows.Count ?? 0, ExportFormat.Xlsx, …)`
   → `IAuditEventRepository.Add` → `IUnitOfWork.SaveChangesAsync`.
5. `Exported` with the file. No `IReadOnlyModeGuard` call; no Google port in the
   graph (spec FR-009).

### 3.5 Request and result types

| Type | Location | Shape |
|---|---|---|
| `JournalExportRequest` | `Models/Requests` | `string? Template, CourseId, From, To, Names, Orientation` — openapi `JournalExportRequest` (AD-8) |
| `JournalExportResult` | `Models` | `JournalExportOutcome Outcome, JournalExportFile? File, IReadOnlyList<ExportFieldError> Errors` |
| `JournalExportOutcome` | `Models` | `Exported, Invalid, NotFound` |
| `JournalExportFile` | `Models/Dtos` | `byte[] Content, string FileName` |
| `ExportFieldError` | `Models/Dtos` | `ExportField Field, ExportMessageKey Key` |
| `ExportField` | `Models/Dtos` | `Template, CourseId, From, To, Names, Orientation` → openapi `fieldErrors[].field` |
| `ExportMessageKey` | `Models/Dtos` | openapi `ExportMessageKey` members |
| `ExportAction` | `Models/Dtos` | openapi `ExportAction`; `ReportPageModel` gains `ExportAction? Export` |

The controller turns `Errors` into the API-6 body, translating each key through
the existing keys (openapi `ExportMessageKey`) — presentation, no rule.

## 4. Mapping to business concepts and the API

| Concept (spec) | Model |
|---|---|
| Export audit (FR-010) | `AuditEvent.JournalExported` + six columns |
| The file's content (FR-004) | `Workbook` from `ReportWorkbookMapper` |
| Program text in the file (FR-007) | `IReportTexts` |
| One source of content (FR-003) | `ReportRequestReader` + `ReportBuilder` |
| Orientation (OD-004) | `PageOrientation`, request only |
| `POST /api/v1/exports/journal-xlsx` body | `JournalExportRequest` |
| `200` body | `JournalExportFile` |
| `fieldErrors` | `ExportFieldError` list |
