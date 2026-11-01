---
artifact_type: test_strategy
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T14:45:20Z
updated_at: 2026-10-31T14:45:20Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md
    version: null
  - path: docs/specifications/US-061-spec.md
    version: 1
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
  - path: docs/designs/api/US-061-api-design.md
    version: 1
  - path: docs/designs/database/US-061-db-design.md
    version: 1
supersedes: null
---

# US-061 Test Strategy — Meet reports of a marked course, and a marked course is not a Meet link candidate

## 1. Scope

Two behaviours over stored data: (1) the Meet line of a marked course on the reports page, the meeting page and both
Meet exports (FR-005, FR-006); (2) a marked course left out of the automatic link and of the candidate list, still
choosable by hand, with its mark on the course-choice page (FR-001 … FR-004). No endpoint, schema, role or audit change
(API_DESIGN and DB_DESIGN NOT_APPLICABLE), so no contract, migration or authorization test is added; the existing
authorization, antiforgery and endpoint-enumeration tests cover the unchanged pages, forms and exports (TC-5).

## 2. Test levels

Every test is an **integration test** against real PostgreSQL via Testcontainers (TC-2), Google ports substituted (TC-4):

- pages and exports **over HTTP** — rendered visible text, and `.xlsx` read back with ClosedXML from the response bytes;
- the automatic link **through the real host's synchronization run** (`SyncHostExtensions`), the Meet port fed with
  synthetic `call_ended` events, the Classroom port empty so the seeded courses, rosters and marks stay as seeded.

No unit test on `MeetCodeScorer` or the DTOs: no artifact names the new members (where the filter sits, what the
DTO carries), and observable tests stay valid whatever names IMPLEMENTATION chooses. **No compile-only skeleton is
needed.**

## 3. Fixtures

- `MeetReportsHostExtensions.SeedWorldAsync` (US-033): the course «Test Course Reports» with meetings A1, B1 and
  excluded meetings; scenarios set `course.last_complete_read_at` and the US-059 mark (`DataMarksHostExtensions.MarkCourseAsync`).
- `MeetLinkingTestData` (US-032): courses A and B taught by one organizer; rosters `SeedClearRostersAsync` (85 % / 20 %)
  or identical rosters (100 % / 100 %) for the re-created course.
- `MeetCodesHostExtensions.SeedRosterCourseAsync` / `SeedMeetingAsync`: the choice-page world — Marked (100 %, "not in
  Classroom" since D1), First and Second (66 % each), Hidden ("not visible" since D2, not the teacher's), Other.
- New `MarkedCourseMeetTexts`: the FR-006 Meet-line texts verbatim, uk and en, composed as mark + «; » + roster; dates
  through `DataMarksTexts.Date` (Kyiv calendar date, the language's short date pattern). The choice-page texts are the
  US-060 course-list texts in `DataMarksTexts`. Instants D1, D2, R1, R2 (`DataMarksWorld`) fall at night in UTC, so the
  Kyiv date is the next day (TC-8).

## 4. Scenarios

**Positive:** the line for each mark on both pages and in C1 of each of the four sheets (three reports sheets, one
meeting sheet); the re-created course gets the code automatically (both marks); the two unmarked candidates are listed
with their shares; the choice page shows the mark next to a marked other course and next to a marked current course;
a marked course chosen by hand is linked as a person's link with `meet_code_course_picked`.

**Negative:** an ordinary course and a skipped-only course have no line and an empty C1; a marked course is not listed
as a candidate, not chosen automatically even with the best share; unmarked courses on the choice page carry no mark;
an existing automatic link to a later-marked course stays (snapshot equal, one audit row).

**Boundary:** last complete read unknown → undated roster part (OD-001 a); only candidate marked → code listed with no
candidate; marked candidate removed leaves the other shares unchanged (85 % still linked); Kyiv date ≠ UTC date.

**Unchanged-content checks (FR-005.4):** each page and each workbook is captured before the mark and after it; with the
line removed (pages) or C1 excluded (workbooks) the content is identical — no figure, row, role or delay notice moves.

**Validation:** no new input (spec §6); nothing to add.

**Security:** read-only mode — all read-only causes — pages and exports work with the English line, exports audited
(two rows), no Google call (AC-008, BR-026). No new endpoint, write or audit event.

**Persistence:** no schema change; the scenarios read the US-059 columns. The linking tests assert the mark is still
stored after the run, so a pass cannot come from the run clearing the mark.

## 5. Excluded scenarios

- FR-001.5 / I-3 (mark cleared by synchronization → candidate again): clearing is US-059 behaviour; the filter reads
  the stored mark, which every test here fixes. A test would re-test US-059.
- Translation-key presence tests: no artifact names the new keys; the uk and en texts are pinned verbatim on the pages
  and in the files, which fails if either translation is missing.
- I-2 places with no mark (reports' course selector, linked and "not a course" lists): out of scope (§10); not asserted.

## 6. Known limitations

- The line is matched as one contiguous visible string (spec FR-005.1 "one line … joined by «; »"); markup that splits
  it with extra text between the two parts would fail the tests.
- The choice-page mark is located as the text between a course's radio control and the next course control.

## 7. Open Decisions affecting testing

None. OD-001 and OD-002 are resolved (a); no new Open Decision.
