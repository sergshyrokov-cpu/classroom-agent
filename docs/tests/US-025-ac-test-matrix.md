---
artifact_type: ac_test_matrix
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T11:30:00Z
updated_at: 2026-10-04T11:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-025-journal-view-for-period.md
    version: null
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/designs/database/US-025-db-design.md
    version: 1
  - path: docs/designs/database/US-025-entity-model.md
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 3
supersedes: null
---

# US-025 Acceptance Criteria → Test Matrix

Conventions for every row: B = `InstallationTestHost.DefaultStart` (2026-09-17
08:00 UTC); the school zone is `Europe/Kyiv` (TC-8); the period is September
2026 (`JournalTestData.Period`, UTC interval `[2026-08-31 21:00Z,
2026-09-30 21:00Z)`). Unit tests build `GetJournalQuery(FakeJournalSource, new
SchoolTimeZone(JournalTestData.Kyiv), new ManualTimeProvider(B))` and call
`ExecuteAsync(JournalRequest, CultureInfo, ct)`. Status before implementation:
**R** = red (fails for missing behaviour), **G** = green by design (explained in
the test-generation report).

Classes (namespace `ClassroomAgent.Tests.<folder>`):

| Short | File |
|---|---|
| CELL | `Application/UseCases/JournalCellTests.cs` |
| ROWS | `Application/UseCases/JournalRowsTests.cs` |
| COLS | `Application/UseCases/JournalColumnsTests.cs` |
| VAL | `Application/UseCases/JournalRequestValidationTests.cs` |
| PER | `Application/UseCases/JournalPeriodTests.cs` |
| SRC | `Infrastructure/Persistence/JournalSourceTests.cs` |
| RO | `Web/UseCases/JournalReadOnlyTests.cs` |
| TZ | `Web/Configuration/TimeZoneConfigurationTests.cs` |
| PAGE | `Web/Pages/JournalPageTests.cs` |
| AUTH | `Web/Security/JournalAuthorizationTests.cs` |
| L10N | `Web/Localization/JournalTranslationTests.cs` |
| LOG | `Web/Logging/JournalLoggingTests.cs` |

## AC-001 — columns, rows, one state per cell

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| A course with 3 items × 2 students gives 3 columns, 2 rows, each row exactly 3 cells in column order | unit | COLS | `Journal.Columns.Count == 3`; every `Row.Cells.Count == 3`; cell *i* belongs to column *i* | R |
| Header: title as given, `Date` = Kyiv date of `ItemDate` (2026-09-05 22:30Z → 2026-09-06), `Kind`, `MaxPoints` only for graded | unit | COLS | as stated; ungraded and material `MaxPoints == null` | R |
| Column order: `ItemDate` asc, then title in the UI culture, then id | unit | COLS | same instant: titles ordered culture-wise; same instant and title: lower id first | R |
| No `courseId` → form only: `Shown`, `Journal == null`, courses listed, no item/member/submission read | unit | VAL | `ItemCalls == MemberCalls == SubmissionCalls == 0` | R |
| Drop-down lists every course incl. archived ones, sorted by name in the UI culture, tie by id; `Section` carried | unit | VAL | `Page.Courses` order and content | R |
| Rendered journal over HTTP shows the in-period titles and the students, not the October item nor the teacher | integration | PAGE | `200`; seeded titles/names present; `OctoberTitle`, `TeacherName` absent | R |

## AC-002 — who is in the journal

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| On the roster for the whole period | unit | ROWS | row | R |
| Left during the period (`OnRoster` false, `LastSeenAt` inside) | unit | ROWS | row | R |
| Left before the period, no submission | unit | ROWS | no row | R |
| Came during the period (`FirstSeenAt` inside) | unit | ROWS | row | R |
| Came after (`FirstSeenAt ≥ end`), and `FirstSeenAt == end` exactly | unit | ROWS | no row | R |
| `LastSeenAt == start` exactly, off roster | unit | ROWS | row (`≥ start`) | R |
| I-1: still on roster, `LastSeenAt` before start (sync paused) | unit | ROWS | row | R |
| Off-roster submitter with a submission to a column | unit | ROWS | row | R |
| Off-roster, submission only to an item outside the period | unit | ROWS | no row | R |
| Teacher membership with a submission | unit + integration | ROWS, PAGE | no row; name absent from the page | R |

## AC-003 — graded work cell states, late mark

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| `AssignedGrade` 8.5 of 10, state `TurnedIn` | unit | CELL | `Grade`, `Points 8.5`, `MaxPoints 10` | R |
| Assigned grade wins in every recognised state (`New`, `Created`, `Returned`, `ReclaimedByStudent`, `StudentEditedAfterTurnIn`) | unit | CELL | `Grade` (theory) | R |
| Grade `0` | unit | CELL | `Grade`, `Points 0` (never empty) | R |
| `TurnedIn` / `StudentEditedAfterTurnIn`, no grade | unit | CELL | `TurnedInNotGraded` | R |
| `Returned`, no grade | unit | CELL | `ReturnedWithoutGrade` | R |
| `New` / `Created` / `ReclaimedByStudent`, no `DueAt` | unit | CELL | `NotTurnedInNoDueDate` | R |
| same, `DueAt` before B | unit | CELL | `NotTurnedIn` | R |
| same, `DueAt` after B; and `DueAt == B` exactly | unit | CELL | `NotDueYet` (strict `DueAt < B`) | R |
| `Late` true on a grade, on `NotTurnedIn`, on `TurnedInNotGraded` | unit | CELL | `Late == true` with the state unchanged | R |
| `Late` false | unit | CELL | `Late == false` | R |
| OD-008 (a): two submissions — newer `UpdateTime` wins; absent `UpdateTime` is older than any; equal (or both absent) → larger `Id` | unit | CELL | the chosen submission's state/grade | R |
| OD-008: an off-roster student with two submissions is one row | unit | ROWS | one row | R |

## AC-004 — not assigned

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Graded and ungraded work with no submission for the student | unit | CELL | `NotAssigned`, `Late false`, no points, no draft | R |
| Over HTTP: the on-roster student without submissions shows the translated "not assigned" | integration | PAGE | `Text("Journal.Cell.NotAssigned", "uk")` present | R |

## AC-005 — draft grades

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Full view, graded, state shown (`TurnedIn`, draft 6.25, no assigned grade) | unit | CELL | `TurnedInNotGraded`, `DraftPoints 6.25` | R |
| Short view, same submission | unit | CELL | `DraftPoints == null` | R |
| Full view, assigned grade present (I-2) | unit | CELL | `Grade`, `DraftPoints == null` | R |
| Ungraded work with a draft | unit | CELL | `DraftPoints == null` | R |
| `TurnedInOn`: full view → Kyiv date (2026-09-05 22:30Z → 2026-09-06); short view → null | unit | CELL | as stated | R |
| `view=short` / `view=SHORT` / absent → `Short` / `Short` / `Full` in the model | unit | VAL | as stated | R |
| Over HTTP: full shows `6,25`, short does not; both views link to the other | integration | PAGE | body checks; `view=short` / `view=full` links | R |

## AC-006 — unrecognised state

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| `Unrecognised` with raw `SUBMISSION_STATE_UNSPECIFIED`, assigned 7, draft 6, full view | unit | CELL | `Unrecognised`, `RawState` as stored, `Points/DraftPoints == null` | R |
| `Unrecognised` + `Late` | unit | CELL | `Late == true` (I-3) | R |
| Due passed, unrecognised | unit | CELL | `Unrecognised`, never `NotTurnedIn` | R |
| Over HTTP: a raw state with markup is rendered HTML-encoded | integration | PAGE | encoded text present; raw markup absent | R |

## AC-007 — ungraded work and materials

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Ungraded: `TurnedIn` / `StudentEditedAfterTurnIn` → `TurnedIn`; `Returned` → `Returned` | unit | CELL | as stated | R |
| Ungraded with an assigned grade Google sent | unit | CELL | still `TurnedIn`, `Points == null` | R |
| Ungraded `New` no due / due passed / due later | unit | CELL | `NotTurnedInNoDueDate` / `NotTurnedIn` / `NotDueYet` | R |
| Material, every row, even with a submission record | unit | CELL | `Empty` | R |
| Column kinds and markers | unit | COLS | `Kind` per item | R |

## AC-008 — participant without a name

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Name → `FullName`; no name, email → `Email` with the email as `DisplayName`; neither → `Unnamed`, `DisplayName == null` | unit | ROWS | as stated | R |
| Order: named and email rows by display value in the UI culture (uk: `Андрій`, `Євген`, `Іван`, `Ярослав` — ordinal order differs), tie by participant id; then unnamed rows by participant id | unit | ROWS | sequence equality | R |
| English culture order (`alpha`, `Bravo`, `charlie`) | unit | ROWS | sequence equality | R |

## AC-009 — read-only mode

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| `GetJournalQuery` resolved from the host in each BR-025 cause (theory) and outside it: returns the seeded journal; teaching tables unchanged; `host.Classroom.ImpersonatedAs` empty | integration (Application) | RO | 2 rows, 3 columns; fingerprint equal; no Google call | R |
| Same over HTTP | integration | RO | `200`, student name present, fingerprint equal, no Google call | R |
| Home page shows the journal link to Admin and Dean, also when suspended | integration | PAGE | `href="/workspace/journal"` | R |

## AC-010 — only Admin and Dean

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Anonymous | integration | AUTH | `302`, `Location: /sign-in`; fingerprint unchanged | R* |
| Dean with a temporary password | integration | AUTH | `302`, `Location: /sign-in/change-password` | R* |
| Admin; Dean | integration | AUTH | `200` with the seeded student's name | R |
| Endpoint enumeration classifies the page as protected | integration | existing `InstallationEndpointTests` | unchanged test, page included once routed | G |

\* The two refusal tests also need the journal shown to the allowed role in the same
class to be meaningful; they may pass before implementation only because the
path does not exist (`302` from the fallback policy). Explained in the report.

## AC-011 — invalid input

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| `courseId` malformed (theory: `abc`, `-1`, `+1`, ` 1`, `1 `, `0`, `1.0`, `9223372036854775808`, `١`) | unit | VAL | `Invalid`, `[CourseMalformed]`, no item/member/submission read, `SelectedCourseId == null` | R |
| `courseId` repeated | unit | VAL | `[CourseMalformed]` | R |
| `courseId` empty string | unit | VAL | treated as absent: `Shown`, form only | R |
| Unknown course; and unknown when no course is stored (`NoCoursesStored` true) | unit | VAL | `CourseUnknown`, `[CourseUnknown]`, no item read | R |
| `from` malformed (theory: `2026-9-1`, `2026-09-31`, `2027-02-29`, `01.09.2026`, `1999-12-31`, `2101-01-01`, ` 2026-09-01`, `2026-09-01T00:00`) | unit | VAL | `[FromMalformed]`, `From == null` | R |
| `from` bounds `2000-01-01` and `2100-12-31` with a matching `to`; leap day `2028-02-29` | unit | VAL | valid | R |
| `to` malformed; `to` repeated | unit | VAL | `[ToMalformed]` | R |
| Inverted pair | unit | VAL | `[PeriodInverted]`, both dates kept | R |
| One-day period | unit | VAL | valid | R |
| `view` unknown / repeated | unit | VAL | `[ViewUnknown]`, `View == Full` | R |
| All four malformed at once | unit | VAL | exactly `[CourseMalformed, FromMalformed, ToMalformed, ViewUnknown]` | R |
| Malformed date + well-formed unknown course | unit | VAL | `Invalid` (not `CourseUnknown`) | R |
| Valid course kept when a date is malformed | unit | VAL | `SelectedCourseId` kept | R |
| Over HTTP: `400` body is the journal page with the translated message; malformed value not echoed; `404` likewise | integration | PAGE | status, form inputs present, message text, value absent | R |
| Unknown parameter ignored and not echoed | integration | PAGE | `200`, value absent | R |
| Warning log names the parameter, never the value | integration | LOG | warning event mentions `from`, not the value | R |

## AC-012 — bilingual; Google data untranslated

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Every `Journal.Cell.*` (but `Empty`, `Grade`), `Journal.Validation.*`, `Journal.Empty.*` key exists in uk and en, non-empty, different | integration | L10N | as stated | R |
| An English user sees English cell text and `8.5`; a Ukrainian one Ukrainian text and `8,5`; the course name and titles identical in both | integration | PAGE | as stated | R |
| Empty states: no item in the period → `Journal.Empty.NoColumns`; items but no student → `Journal.Empty.NoRows` | unit + integration | COLS, PAGE | `EmptyStateKey`, `Journal == null`; translated text | R |
| Titles, names and raw states are HTML-encoded | integration | PAGE | encoded present, raw markup absent | R |

## AC-013 — tests never reach Google; PostgreSQL data

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Q1–Q4 over real PostgreSQL: scope, half-open period (item at `start` in, at `end` out), other course out, teacher out, leaver in, both duplicates returned with `Id`/`UpdateTime`, unrecognised `RawState`, `Kind` mapping, archived course listed | integration | SRC | as stated | R |
| Non-zero offset or `start ≥ end` → `ArgumentException` | integration | SRC | as stated | R |
| Each port method issues exactly one database command; nothing tracked afterwards | integration | SRC | counting interceptor `== 1`; `ChangeTracker.Entries()` empty | R |
| Bounded reads: 1×1 and 5×4 journals call each port method at most once; no item → members/submissions not read; no member → submissions not read | unit | COLS | call counts | R |
| No schema change: the EF model has no pending migration | integration | SRC | `HasPendingModelChanges() == false` | G |
| Every host test runs with the substituted Classroom port | integration | RO | `ImpersonatedAs` empty | R |

## AC-014 (derived) — period boundaries in the school's time zone

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| September → bounds `(2026-08-31 21:00Z, 2026-09-30 21:00Z)`, offsets zero, same bounds for items and submissions | unit | PER | `Bounds` | R |
| Item at 2026-09-30 21:30Z (00:30 on 1 Oct in Kyiv) is not a column; item at 2026-08-31 21:30Z (00:30 on 1 Sep) is | unit | PER | as stated | R |
| December → `(2026-11-30 22:00Z, 2026-12-31 22:00Z)` | unit | PER | winter offset | R |
| 25 Oct 2026 (DST ends) one-day period → `(2026-10-24 21:00Z, 2026-10-25 22:00Z)` | unit | PER | 25-hour day | R |
| Default period with B = DefaultStart → 1–30 Sep; with B = 2026-09-30 22:30Z (1 Oct in Kyiv) → 1–31 Oct | unit | PER | `Page.From/To` | R |
| Only `from` given (2026-10-05) with B in September → `to` defaults to 30 Sep → `PeriodInverted` | unit | PER | as stated | R |

## AC-015 (derived) — time zone setting

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| Missing / blank / `Not/AZone` / Windows id `FLE Standard Time` | integration | TZ | host refuses to start; exception text names `Installation:TimeZone` | R |
| `Europe/Kyiv`, and padded ` Europe/Kyiv ` | integration | TZ | host starts | G |

## Logging (spec FR-016)

| Scenario | Level | Class | Expected | St |
|---|---|---|---|---|
| A built journal logs one Information event with the course id and row/column counts | integration | LOG | event found | R |
| No log line after a journal request carries a student name, an email, a title or a grade text | integration | LOG | absent | R (paired with the first) |
