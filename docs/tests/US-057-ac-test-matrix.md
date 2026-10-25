---
artifact_type: ac_test_matrix
story: US-057
version: 1
status: DRAFT
created_at: 2026-10-25T20:35:00Z
updated_at: 2026-10-25T20:35:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-057-spec.md
    version: 2
  - path: docs/decisions/US-057-open-decisions.md
    version: 2
  - path: docs/tests/US-057-test-strategy.md
    version: 1
supersedes: null
---

# US-057 Acceptance Criteria → Test Matrix

Status before implementation: **red** = fails for the missing behaviour; **green (design)** =
passes today because the current code already gives the expected result (strategy §6).

Class prefixes: `IG` = `Infrastructure/Google/GoogleClassroomReaderValueRangeTests`,
`UCS` = `Application/UseCases/ValueOutOfRangeSkipTests`,
`WBS` = `Web/BackgroundServices/ValueOutOfRangeSyncTests`,
`UCR` = `Application/UseCases/ReportScaleBoundsTests`,
`WP` = `Web/Pages/ScaleBoundsReportPageTests`.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | `maxPoints` ±1·10³⁰, ±8·10²⁸ → `ValueOutOfRange` | unit (adapter) | IG | `AValueBeyondTheRange_IsAValueOutOfRangeFailure` (maxPoints rows) | `GoogleReadFailedException`, kind `ValueOutOfRange` | red (4) |
| AC-001 | three courses, second out of range (coursework / submissions read) | unit (use case) | UCS | `ACourseWithAValueOutOfRange_IsSkipped_AndTheRunCompletes` | 1, 3 stored; 2 and its person not; `completed`; 2 counted | red (2) |
| AC-001 | three courses, second `maxPoints` 1·10³⁰, real adapter | integration (host) | WBS | `ACourseWithMaxPointsBeyondTheRange_IsSkipped_AndTheRunCompletes` | 1, 3 + items stored; 2 nothing; Meet called; `completed`; 2 counted | red |
| AC-002 | assigned / draft ±1·10³⁰, ±8·10²⁸ → `ValueOutOfRange` | unit (adapter) | IG | `AValueBeyondTheRange_IsAValueOutOfRangeFailure` (grade rows) | kind `ValueOutOfRange` | red (8) |
| AC-002 | assigned 1·10³⁰ and draft −1·10³⁰ in two courses | integration (host) | WBS | `CoursesWithGradesBeyondTheRange_AreSkipped_InBothDirections` | both skipped, third stored, `completed`, 1 counted | red |
| AC-003 | stored course, later out-of-range read | unit (use case) | UCS | `AStoredCourse_WithAValueOutOfRange_IsLeftAsItWas` | name, complete-read time, item, grade, memberships unchanged; other course updated | red (2) |
| AC-003 | stored rows (seeded), Google now `maxPoints` 1·10³⁰ + rename | integration (host) | WBS | `AStoredCourse_WithMaxPointsBeyondTheRange_IsLeftExactlyAsStored` | text fingerprint of course, items, memberships, submissions equal | red |
| AC-004 | one Warning 5135; no value or identity in the log | integration (host) | WBS | `TheSkip_IsLoggedOnce_WithoutTheValueOrAnyIdentityOfTheCourse` | single `SyncCourseValueOutOfRange`, Warning, id 5135, run id, course id; none of the listed strings | red |
| AC-004 | the failure carries no value or id (SC-10) | unit (adapter) | IG | `TheFailure_CarriesNoValueAndNoIdentifier` | exception text free of value, item, submission, user id | red (3) |
| AC-005 | archived course skipped, next run same `updateTime` | unit (use case) | UCS | `AnArchivedCourseSkippedForAValue_IsReadAgainByTheNextRun_AtTheSameUpdateTime` | read in full and stored; control course not read (1 skipped unchanged) | red |
| AC-006 | 7·10²⁸ of 0.0001, assigned / draft, page | unit (report) | UCR | `AnExtremeGrade_ShowsTheLabelFor100Percent_OnThePage` | scale label `TSFULL` | red (2) |
| AC-006 | same, export | unit (export) | UCR | `AnExtremeGrade_ShowsTheLabelFor100Percent_InTheExport` | exported; workbook has `TSFULL` only | red (2) |
| AC-006 | stored 7·10²⁸ of 0.0001, page + export over HTTP | integration (HTTP) | WP | `AnExtremeGrade_ShowsTheLabelFor100Percent_OnThePageAndInTheExport` | 200 / 200; `TSFULL` only | red (2) |
| AC-007 a | stored 0 of 0.0001, page + export over HTTP | integration (HTTP) | WP | `AZeroGrade_ShowsTheLabelFor0Percent_OnThePageAndInTheExport` | 200 / 200; `TSZERO` only | green (design) (2) |
| AC-007 b | −7·10²⁸ of 0.0001 via substituted port, page | unit (report) | UCR | `ANegativeExtremeGrade_ShowsTheLabelFor0Percent_OnThePage` | scale label `TSZERO` | red (2) |
| AC-007 b | same, export | unit (export) | UCR | `ANegativeExtremeGrade_ShowsTheLabelFor0Percent_InTheExport` | exported; `TSZERO` only | red (2) |
| AC-007 / AC-006 | bounds 2/2, 0/2, 1/2 (FR-005.1–3) | unit (report) | UCR | `TheBounds_AndAPointBetween_ShowTheirLabels` | `TSFULL`, `TSZERO`, `TSMID` | green (design) (5) |
| AC-008 | 12-point preset 6, 12, 0 of 12 | unit (report + export) | UCR | `TheTwelvePointPreset_ShowsTheSameLabelsAsBefore` | `6`, `12`, `1`; export produced | green (design) (3) |
| AC-009 | skip then configuration failure | unit (use case) | UCS | `ASkipFollowedByAnotherFailure_FailsWithThatFailure_NotAsUnexpected` | `failed`, `TechnicalAccountCannotRead` | red |
| AC-009 | skip then roster 403, real adapter | integration (host) | WBS | `ASkipFollowedByAnotherFailure_IsStillReported_AndTheRunFailsWithThatFailure` | `failed`, `TechnicalAccountCannotRead`; 5135 line for course 1 | red |
| AC-010 | ±7.9·10²⁸ converted as today | unit (adapter) | IG | `AValueJustInsideTheRange_IsConvertedAsToday` | equals `(decimal)value` | green (design) (6) |
| AC-010 | +7.9·10²⁸ for all three fields stored | integration (host) | WBS | `ValuesJustInsideTheRange_AreStored_AndTheCourseIsNotSkipped` | `completed`, 1 counted, stored values equal | green (design) |

Totals: 53 cases in 5 classes — 36 red, 17 green (design). Every AC has at least one test.
