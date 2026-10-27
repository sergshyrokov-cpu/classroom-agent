---
artifact_type: implementation_verification
story: US-060
version: 1
status: APPROVED
created_at: 2026-10-27T14:45:47Z
updated_at: 2026-10-27T14:45:47Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-060-marks-in-course-list-journal-report-and-export.md
    version: null
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
  - path: docs/designs/api/US-060-api-design.md
    version: 1
  - path: docs/designs/database/US-060-db-design.md
    version: 1
  - path: docs/tests/US-060-test-strategy.md
    version: 1
  - path: docs/tests/US-060-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-060-test-generation-report.md
    version: 1
  - path: docs/evidence/US-060-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-060 Implementation Verification

## 1. Summary

**PASS** — 0 Critical, 0 Major, 1 Minor, 2 Info.

The code shows every mark the Specification asks for, where it asks, computed in Application from stored data only:

- the course mark;
- "no teacher in Classroom";
- the course header line;
- above-maximum grades;
- the item mark;
- "left".

Each Acceptance Criterion has a test that would fail without the behaviour, and the Story's 73 cases pass. The
implementation report matches the working tree. Its one deviation, D-1 (a corrected test fixture), is disclosed
and sound.

Judged from files and the working tree only, in a fresh context.

**Delegation:**
- `cheap-worker` (Sonnet) ran the build and the six Story test classes (§8).
- Everything else was done inline: reading code against the spec, reading tests against the ACs, classifying findings.

## 2. Reviewed Artifacts

| Artifact | Version | Status |
|---|---|---|
| Story `docs/stories/US-060-marks-in-course-list-journal-report-and-export.md` | — | — |
| `docs/specifications/US-060-spec.md` | 1 | APPROVED |
| `docs/decisions/US-060-open-decisions.md` | 1 | OD-001…003 resolved a) |
| `docs/designs/api/US-060-api-design.md` | 1 | NOT_APPLICABLE (no openapi) |
| `docs/designs/database/US-060-db-design.md` | 1 | NOT_APPLICABLE (no entity model) |
| `docs/tests/US-060-test-strategy.md`, `US-060-ac-test-matrix.md` | 1 | DRAFT |
| `docs/evidence/US-060-test-generation-report.md` | 1 | — |
| `docs/evidence/US-060-implementation-report.md` | 1 | DRAFT; inputs name the current versions — not stale |

## 3. AC Verification

| AC | Tests (class → method) | Code checked | Verdict |
|---|---|---|---|
| AC-001 | CL `TheCourseList_ShowsEachCoursesMark_NextToItsStatus`, `TheCourseCard_ShowsTheCoursesMark_InItsStatusField` (uk, en) | `DataMarkRules.Absence`; `GetCourseListQuery`, `GetCourseCardQuery`; `CourseListSource`, `CourseCardSource` project `AbsenceMark`/`AbsentSince`; `Courses/Index.cshtml`, `Card.cshtml` | Proven. The order status → mark → teacher is asserted, the Kyiv date is pinned, and the plain course is checked by stems |
| AC-002 | CL `ACourseWithoutATeacherOnTheRoster_SaysSo_InPlaceOfTheTeachers` | `NoTeacherInClassroom = current.Count == 0` over `GetCurrentTeachersAsync` (on-roster teachers) | Proven. After D-1 the "former teacher absent" assertion is meaningful (§6) |
| AC-003 | JM `TheFullJournal_…`, `TheShortJournal_…`; RM `TheReport_LabelsGradeAndDraftAboveMaximum_AndKeepsTheScaleValue` | `DataMarkRules.IsAboveMaximum` (strict, max > 0); `JournalCellRule` flags (the draft only when `full`); `ReportBuilder.ToReportCell` → `ReportAboveMaximum`; class + label in both views | Proven. Positive cases, 12/12 ordinary, the short view drops the draft label, the scale value `TSFULL` is kept |
| AC-004 | RX `AnAboveMaximumCell_KeepsItsValue_IsColoured_AndCarriesTheRawPoints`; BX `AnAboveMaximumGradeCell_IsColouredLikeTheProgramLayout_AndCarriesTheNote` | `ReportWorkbookMapper.GradingCell` (`Highlighted`, `AboveMaximumNote`); `ExportMarks.Apply` in both renderers | Proven. The file is read back; value, fill, the same colour in both layouts, note text, and no fill or note on 12/12 |
| AC-005 | JM `TheDeletedItem_KeepsItsColumnAndGrades_WithTheMarkInItsHeading`; RM `TheDeletedItem_IsMarkedInItsColumnAndOnTheLessonTopics`; RX `TheDeletedItem_IsMarkedInItsHeadingAndTopic`; BX `TheDeletedItemsTopic_CarriesTheMarkAsANote_AndThePrintedTextIsUnchanged` | `NotInClassroomSince != null` in `JournalSource`/`JournalFieldSource`; `JournalColumn`, `GradingColumn`, `LessonTopicRow`; mapper heading/topic; blank topic note | Proven. Grades in the marked column are asserted; no date (I-5); the ordinary item is unmarked |
| AC-006, AC-007, AC-011, AC-012 | JM `TheCourseHeaderLine_StandsAboveTheTable` (9 variants), `…_IsInEnglish_…`; RM `TheCourseHeaderLine_IsInTheReportHeader`; RX `TheCourseHeaderLine_IsTheThirdCellOfTheCourseRow`; BX `TheCourseCell_CarriesTheHeaderLineAsANote` | `DataMarkRules.Notice` (precedence VR-002); skip record by `CourseGoogleId == c.GoogleId` in the course query (VR-003, no extra round trip); `#journal-notice` above the table, `#report-notice` in `#report-header`, C2 cell, `{{course}}` note | Proven. Each variant's exact line occurs once, other stems are absent, the table stays at row 6, grades are unchanged. The None/Archived variants are green by design and are disclosed in the matrix |
| AC-008 | JM, RM `AStudentOffTheRoster_IsMarkedLeft_NextToTheName`; RX, BX `ALeaversNameCell_KeepsTheName_AndCarriesTheLeftNote` | `DataMarkRules.Left`; `SeenOnce = FirstSeenAt == LastSeenAt` in both sources (OD-003 a); `StudentCell` note | Proven. Dated, undated (a regex rules out a date) and on-roster cases; names unchanged |
| AC-009 | every `"en"` case above | resx: 12 keys in each file, verbatim to the FR-009 table (checked line by line); `LocalizedReportTexts` maps the 8 new `ReportText` values | Proven. The tests pin literals, not the resx, so the check is not circular |
| AC-010 | RO `InReadOnlyMode_TheListAndTheExportShowTheMarks_AndWriteNoTeachingData` (3 causes) | no guard added; queries read only | Proven. List mark, export C2 line and note; teaching fingerprint unchanged; 0 Google calls |

## 4. Specification Coverage

- **FR-001…FR-009:** each is implemented where the report says.
- **VR-001…VR-005:** each lives in one place, `DataMarkRules`, plus the two `SeenOnce` projections.
- **FR-003.1 edge case:** `NotInClassroom` with a null `AbsentSince` cannot happen (§1.1: the two are null together).
  The code would fall through to the skip check, which is harmless.
- **FR-006.3:** both queries build rows only from memberships, so no other row kind gets a mark.
- **FR-008.5** (multi-copy notes): covered by construction — each placement carries its own `WorkbookCell` — but no
  test exercises a multi-copy fill (Minor m-1).
- **Out of scope:** no behaviour beyond the Specification found. The course card's "Left the course" section is
  untouched (spec §10).

## 5. Design Conformance

- API and DB designs are NOT_APPLICABLE. No endpoint, route, entity, migration or package changed (confirmed by the
  diff).
- **AD-3:** no `DbContext` in Web. The views render only — `DataMarksTexts` words DTO values and decides nothing.
- **AD-4:** no Google type and no Google call.
- **AD-6:** no read-only branch added; viewing and export stay outside the guard, as BR-026 requires.
- **AD-8:** only DTOs and records reach the views. `CourseAbsenceMark` is a Domain enum value in a DTO, the same
  practice as `CourseState`.
- **CSP:** the highlight is a CSS class, with no inline style.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | The TEST_WRITING fixture (`DataMarksHostExtensions.PresentTeacherName`, `FormerTeacherName`) | It held full names while the list labels people surname-first (US-020 FR-007). Now labels and seeded names agree, and no assertion was weakened | **Sound.** Expectation follows an approved rule; it also made the AC-002 absence check meaningful |
| D-2 | — (style) | Trailing defaulted record members keep existing tests compiling | Sound; not a departure from an artifact |
| I-1 | — | Comment author set to empty, so no machine account name enters a file | Sound; in line with US-043 INFO-2 |

## 7. Report Accuracy

- **Change set:** the report's §4 equals the working tree.
  - 41 modified `src` files.
  - 9 new `src` files: 5 DTOs, `DataMarkRules`, `ExportMarks`, `DataMarksTextKeys`, `DataMarksTexts`.
  - The TEST_WRITING test files (untracked).
  - The workflow and closed-list files.
- **Traces:** every changed code and test file is traced.
- **Clean change:** ripgrep over the changed and new files found no `TODO`, `TBD`, `FIXME`, `NotImplementedException`,
  or skipped or commented-out test.
- **Not visually checked:** the on-screen colour is disclosed as unchecked. This is not a defect — it is asserted
  as a class, and the label is always present (FR-004.5).

## 8. Story Test Run

Run by `cheap-worker` (Sonnet).

**Environment:** free RAM 5.65 GB, Docker 29.8.0.

**Build:** `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.

**Tests:** `dotnet test ClassroomAgent.sln --no-build --filter FullyQualifiedName~<Class>`, one term per run:

| Class | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| BlankFormMarksExportTests | 14 | 14 | 0 | 0 |
| CourseListMarksPageTests | 6 | 6 | 0 | 0 |
| JournalMarksPageTests | 19 | 19 | 0 | 0 |
| ReportMarksExportTests | 15 | 15 | 0 | 0 |
| ReportMarksPageTests | 16 | 16 | 0 | 0 |
| DataMarksReadOnlyTests | 3 | 3 | 0 | 0 |
| **Total** | **73** | **73** | 0 | 0 |

**Notes:**
- Two classes ran in parallel by mistake; both were green.
- The worker reported an end time of 16:45:36Z. `date -u` at the time of writing gave 14:45:47Z, so the worker's
  value is not used.
- The full suite was not rerun here; `security-reviewer` runs it.

## 9. Findings

| Id | Severity | Where | Checked against | Finding | Loop-back |
|---|---|---|---|---|---|
| m-1 | Minor | `BlankFormMarksExportTests` | spec FR-008.5 | No test fills a multi-copy blank form, so "every copy carries the notes of its own cells" is shown by construction only (per-placement `WorkbookCell.Note`), not by a test | advisory (`test_gap` if the Owner wants it) |
| INFO-1 | Info | `JournalMarksPageTests`, `ReportMarksPageTests`, `ReportMarksExportTests`, `BlankFormMarksExportTests` (None / ArchivedUnchanged variants) | spec FR-003.3, AC-012 | These absence cases are green by design (disclosed in the matrix). They prove "no line" only because no rule reads `State`. That is the specified behaviour, but the tests cannot fail for an unrelated reason | — |
| INFO-2 | Info | implementation report §7 | strategy §5 | The on-screen highlight colour (`#fff2cc`) has not been seen by a person; worth a look at the first demo | — |

**Open findings from earlier stages:**
- `US-060/IMPLEMENTATION/INFO-1` (D-1): verified as fixed (§6) and closed `FIXED`.
- `US-060/SPEC_REVIEW/m-1` and `m-3` concern the Specification and digest wording, which is not this stage's
  responsibility. They stay OPEN for the Owner. The code follows VR-001 as approved (maximum > 0; equal is not
  above).

## 10. Verdict

**PASS** — no Critical or Major finding. Next stage: SECURITY_REVIEW.
