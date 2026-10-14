---
artifact_type: test_strategy
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-14T06:29:14Z
updated_at: 2026-10-14T06:29:14Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/decisions/US-043-open-decisions.md
    version: 2
  - path: docs/designs/api/US-043-api-design.md
    version: 1
  - path: docs/designs/api/US-043-openapi.yaml
    version: 1
  - path: docs/designs/database/US-043-db-design.md
    version: 1
supersedes: null
---

# US-043 Test Strategy — Fill the group-lessons blank form

## 1. Scope

The export `POST /api/v1/exports/journal-xlsx` with the new `layout`,
`studentsPerCopy`, `lessonsPerCopy` members; the shipped form
`docs/product/report-templates/group-lessons-blank.xlsx` and its embedding; the
layout choice on `GET /reports`; logging and translations. DB_DESIGN is
`NOT_APPLICABLE` (no schema change, audit format stays `xlsx`): no persistence
test beyond the existing audit row read through raw SQL.

## 2. Test levels

| Level | Where | What |
|---|---|---|
| Integration over HTTP + PostgreSQL (TC-2) | `Web/Pages/GroupLessonsBlankExportTests` | the filled file, validation, sheet limit, audit row, read-only mode, program-layout parity |
| Security (TC-5) | `Web/Security/GroupLessonsBlankAuthorizationTests` | Admin / Dean allowed; anonymous `401`; forced password change `403`; no antiforgery `400` |
| Page (HTTP) | `Web/Pages/GroupLessonsBlankPageTests` | layout choice, number inputs with min / max / default from `ExportAction.blankForm`, nothing remembered, uk / en |
| Logging | `Web/Logging/GroupLessonsBlankLoggingTests` | FR-011 line fields; refusals as Warnings; no value, no personal data |
| Localization | `Web/Localization/GroupLessonsBlankTranslationTests` | the four new message keys in uk and en |
| File (no host) | `Infrastructure/Export/GroupLessonsBlankFormTests` | AC-013 integrity of the shipped form against `school-journal-full.xlsx`; the embedded resource is that file |

No Application-level unit test is added: the shape of the fill model and of the
form renderer port is IMPLEMENTATION's (api-design §8), so a unit test would
fix a signature no artifact defines. Every behaviour is asserted end-to-end
through the contract instead. Read-only mode: the HTTP theory covers all three
BR-025 causes and asserts no Google call; the Application-level proof that the
export never consults `IReadOnlyModeGuard` is the existing US-028 test of the
same command (it has no guard dependency).

## 3. The oracle

- **The screen** (spec FR-005: "the same report … nothing computed again") is
  taken from the US-028 program-layout export of the same request
  (`GroupLessonsBlankHostExtensions.ScreenAsync`): course and teachers from the
  header block, students and grade cells from "Grading", lessons (date, title,
  hours) from "Lesson topics", materials recognised by the heading's material
  marker. Each grade cell of the form must equal the screen's cell in type,
  value and quote prefix.
- **The plan of copies** (FR-006) is computed in the test (`GroupLessonsBlank.Plan`)
  from the counts: groups, blocks, order, names, per-kind numbering, footer
  number.
- **The form**: the cell positions are the facts of spec FR-002 (recorded in
  `GroupLessonsBlank`, never in production code); "untouched" is asserted
  against the shipped file itself (values outside the written areas, lines,
  fonts, rotation, widths, heights, page setup, no merges).

## 4. Scenarios

**Positive.** Small report with defaults (one copy each); 25 × 30 by 10 / 23
(the Story's eight sheets); partial copies 7 × 5 by 5 / 3; 1 / 1; 30 / 23,
`null`, `30.0` / `23.0`; template showing materials; English user (sheet names
and printed text untranslated); exactly 100 sheets.

**Negative.** Per-copy values `0`, `31`, `-1`, `10.5`, `"10"`, `true`, `[]`,
`{}` / `0`, `24`, `2.5`, `"5"`, `false`; unknown layout (case, leading space,
`PROGRAM`); non-string layout (`RequestMalformed`); orientation `sideways` with
the form; more than 100 sheets; unknown course before the sheet limit.

**Boundary.** 1 and 30 / 1 and 23; 100 vs 110 sheets; empty report; students
without lessons (I-6); lessons without students.

**Validation.** Field order `from`, `studentsPerCopy`, `lessonsPerCopy`; message
of the first; per-copy members ignored with `program` and with an invalid
layout (I-10, api-design §2.4); value never echoed.

**Security.** TC-5 allowed / forbidden; antiforgery; read-only causes; no Google
call; formula-like text quote-prefixed (AC-010); audit row without personal data;
logs without values or personal data; the form holds no personal data (equal to
the blank reference apart from markers).

**Persistence.** One `journal_exported` row, format `xlsx`, student count, no
row on any refusal. No schema test (no migration, DB_DESIGN N/A).

## 5. Fixtures

`PostgreSqlFixture` (Testcontainers); `JournalHostExtensions.StartAsync` (actor,
read-only cause); `SeedJournalAsync` (US-028 seed) and the new
`SeedBlankCourseAsync` (any number of named students, lessons one per September
day, materials, returned grades on a fixed pattern, a named teacher);
`InsertScaleTemplateAsync` (whole-number labels `2`, `5` and text `добре`, so
number and text grade cells both occur). All data synthetic (TC-4).

## 6. Excluded

| Scenario | Why |
|---|---|
| `500` for a missing or broken embedded form | the form is compiled in; substituting it needs a seam no artifact defines. AC-013 tests and the embedding test keep a broken form from shipping (spec I-2). |
| Phone-width layout of the inputs (NFR-070) | CSS behaviour, not observable through the HTTP host; reviewed by hand. |
| The script's sending of raw strings (api-design §2.7) | browser JavaScript; the server side of it (a string is a field error) is tested. |

## 7. Known limitations

- `Plan` and the screen oracle restate FR-006 / US-028 FR-004 in test code; a
  defect shared by both would go unseen. The Story table case (AC-002) pins the
  plan to literal names.
- Grade-cell parity relies on the US-028 program layout being correct (its own
  tests cover it).

## 8. Open Decisions affecting testing

None. OD-006 … OD-012 are resolved; no new Open Decision was raised.
