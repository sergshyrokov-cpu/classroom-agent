---
artifact_type: test_strategy
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T17:10:24Z
updated_at: 2026-10-28T17:10:24Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md
    version: null
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
  - path: docs/designs/api/US-063-api-design.md
    version: 1
  - path: docs/designs/database/US-063-db-design.md
    version: 1
supersedes: null
---

# US-063 Test Strategy

## 1. Scope

The two lines "generated" (FR-001) and "data from Google as of" (FR-002): on the journal page and in the report header
(FR-003), in a note on the course-name cell of the program-layout export on both sheets (FR-004) and of every
`{{course}}` cell of the «Бланк для групових занять» (FR-005), in the format and texts of FR-006, in read-only mode
(§7). API_DESIGN and DB_DESIGN are NOT_APPLICABLE: no endpoint, schema or contract change, so there are no contract or
migration tests; the existing export contract tests stay as regression evidence.

## 2. Test levels

All new tests are **integration tests over HTTP** against PostgreSQL via Testcontainers (TC-2), with the host's manual
clock (`ManualTimeProvider`) so B is known exactly, and the substituted Google ports (TC-4). Files are read back with
ClosedXML. Rationale: the behaviour is visible only on rendered pages and in the produced files; the DTO fields that
will carry B and the last read are not designed by name (API N/A), so a unit test on them would invent declarations —
hence **no compile-only skeleton** (tests reference only existing test infrastructure and DOM ids / cells the
Specification names).

## 3. Fixtures

- The US-060 marks course (`DataMarksHostExtensions.SeedMarksCourseAsync`, `CourseHeaderVariant`) — its stored last
  complete read is R2 = 2026-09-07 22:00 UTC (08.09.2026 01:00 in Kyiv: a date boundary, TC-8); the clock starts at
  2026-09-17 08:00 UTC (11:00 Kyiv).
- New `TestInfrastructure/GeneratedLinesTexts`: the FR-006.3 texts verbatim (uk, en), the FR-006.1 format (Kyiv,
  `ShortDatePattern + " HH:mm"`), the label stems.
- Null last read: `UPDATE course SET last_complete_read_at = NULL`. Archived unchanged course: variant
  `ArchivedUnchanged` plus `last_listed_at` five days after R2.

## 4. Scenarios

### Positive
- Journal (full, short; uk, en) and report (uk, en) show both lines, each the whole text of its element
  (`journal-generated`, `journal-data-as-of`, `report-generated`, `report-data-as-of`), once; journal lines above the
  table, report lines inside `#report-header`.
- Program export: the Course value cell (B2) of both sheets keeps the course name and has a note of exactly the two
  lines (G2 = moment of export after a 7-minute clock advance, R2).
- Blank export: the `{{course}}` cell keeps the name and has the note; every grading copy of a three-copy fill too.

### Negative
- No course selected → neither line nor label on the journal page.
- No printed cell of either file holds a label or either formatted instant.
- The topics copy of the blank gets no "generated" note; the program label cell gets no note; C2 stays empty when
  US-060 has no line.

### Boundary
- Literal Ukrainian lines «Сформовано: 17.09.2026 11:00», «Дані з Google на: 08.09.2026 01:00» — zone conversion,
  date boundary, no seconds (VR-003).
- AC-002: an item due 5 minutes after B; before → cell «термін ще не минув» and the line shows B; after a 10-minute
  advance → «не здано» and the line shows the new B (VR-001).
- Empty period (July 2026) still shows both lines (FR-003.3).
- Null last read → «невідомо» / "unknown" on screen and in the notes (FR-002.3).
- Archived course: last complete read shown, the later listing not (VR-002, OD-002 a).

### Note composition (VR-004)
- An existing note (US-060 header line on the blank's course cell) is kept first, then the two lines, no empty line;
  the program layout keeps the US-060 line printed in C2 and its B2 note has only the two lines.

### Validation
- None: the Story adds no input (spec §6).

### Security
- No endpoint, policy or role changes (spec §7): existing allowed/forbidden-role, anonymous-access and antiforgery
  suites cover the unchanged endpoints and must stay green.
- Read-only mode, three causes: journal and export show the lines; teaching tables unchanged (fingerprint); exactly
  one export audit row (the BR-026 permitted write); zero Google calls.
- No Google call in any test (`GoogleCallCount() == 0`).

### Persistence
- None new; the only persistence assertion is "nothing written" in read-only mode.

## 5. Excluded scenarios

- Unknown course id on the journal/report: existing behaviour (US-025/US-027 tests), lines follow FR-003.1's "found"
  condition; not duplicated.
- Meet exports, course list, course card: out of scope (spec §10).
- Log lines not carrying the values (spec §7 SC-10): nothing logs them today and the Story adds no log; not tested.
- Admin vs Dean: same view (BR-002); tests sign in as Dean like US-060.

## 6. Known limitations

- "The cells and header rows are otherwise the same as before" (AC-003) is proved by the existing layout suites
  (`JournalExportTests`, `GroupLessonsBlankExportTests`, `ReportMarksExportTests`, `BlankFormMarksExportTests`)
  staying green, plus the course-cell value assertions here.
- Spec findings m-1, m-2 (SPEC_REVIEW) are text issues accepted by the Owner; they change no test.

## 7. Open Decisions affecting testing

None. OD-001…OD-003 resolved a); each has a test (AC-006, AC-009, AC-008).
