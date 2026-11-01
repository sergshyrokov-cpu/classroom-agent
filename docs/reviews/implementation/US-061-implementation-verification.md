---
artifact_type: implementation_verification
story: US-061
version: 1
status: APPROVED
created_at: 2026-11-01T07:08:57Z
updated_at: 2026-11-01T07:08:57Z
produced_by: implementation-verifier
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
  - path: docs/tests/US-061-test-strategy.md
    version: 1
  - path: docs/tests/US-061-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-061-test-generation-report.md
    version: 1
  - path: docs/evidence/US-061-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-061 Implementation Verification

## 1. Summary

**PASS.** The code does what Specification v1 asks: marked courses are removed from the Meet candidates before the
decision (one filter in `MeetCodeScorer.Score` serves the automatic link, "unlinked meetings" and the choice page's
candidates); the choice page shows the course-list mark next to a marked course; reports 1–4 and both exports carry the
Meet line built in Application. Every AC maps to a test that would fail without the behaviour (or, for the
green-by-design guards, protects behaviour that already held). The three deviations are disclosed and sound. No
Critical or Major finding; one Minor and two Info.

Delegated: one `cheap-worker` run of the build and the seven affected test classes (report-only, no files touched).
Reading the code and tests against the ACs, the deviations and the classification were done here. Docker Desktop had to
be started before the run.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md` | — |
| Specification | `docs/specifications/US-061-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-061-open-decisions.md` | 1 (OD-001 a, OD-002 a) |
| API design | `docs/designs/api/US-061-api-design.md` | 1 (NOT_APPLICABLE; no openapi) |
| DB design | `docs/designs/database/US-061-db-design.md` | 1 (NOT_APPLICABLE; no entity model) |
| Test strategy / AC matrix | `docs/tests/US-061-test-strategy.md`, `docs/tests/US-061-ac-test-matrix.md` | 1 / 1 |
| Test generation report | `docs/evidence/US-061-test-generation-report.md` | 1 |
| Implementation report | `docs/evidence/US-061-implementation-report.md` | 1 — inputs name the current versions |
| Working tree | `git status`, `git diff`, untracked files | 2026-11-01 |

## 3. AC Verification

| AC | Tests | Verdict |
|---|---|---|
| AC-001 | MP `ANotInClassroomCourse_…_AndNothingElseChanges` (Dean, Admin) — exact uk line on both pages, page text from the delay notice / course name equal to the pre-mark page minus the line; MP `OnTheMeetingPage_TheLineComesAfterTheCourseName`; MX `ANotInClassroomCourse_PutsTheLineInC1_…_AndChangesNothingElse` — C1 = line on 3 + 1 sheets, every other used cell equal to the pre-mark export | proven |
| AC-002 | MP `ANotVisibleCourse_…`, MX `ANotVisibleCourse_…` — exact «Курс не видно технічному акаунту з D2; склад курсу — на R2» | proven |
| AC-003 | MP `AnOrdinaryCourse_ShowsNoLine` (uk and en fragments absent, page content present), MX `AnOrdinaryCourse_LeavesC1Empty` | proven (guard) |
| AC-004 | ML `ARecreatedCourse_GetsTheCodeAutomatically_WhenTheOldOneIsMarked` ×2 marks — 100 %/100 % tie without the filter, link to B with `meet_code_auto_linked`; ML `AMarkedCourse_IsNeverChosen_EvenWithTheBestShare` | proven — both fail without the filter |
| AC-004 / FR-001.2 | ML `RemovingAMarkedCandidate_LeavesTheOthersSharesAsBefore` | proven (guard); the code keeps the denominator outside the candidate loop |
| AC-005 | MC `UnlinkedMeetings_OfferTheUnmarkedCandidatesOnly` (Dean, Admin) — two «66 %», no marked name, no «100 %»; MC `ACodeWhoseOnlyCandidateIsMarked_IsListedWithNoCandidate` | proven |
| AC-006 | MC `ChoosingAMarkedCourse_LinksTheCode_AsAPersonsLink_WithTheUsualAudit` — person's link, `meet_code_course_picked` succeeded | proven (guard; manual path untouched) |
| AC-007 | ML `AnAutomaticLink_StaysWithItsCourse_AfterTheCourseIsMarked` — link snapshot equal after a second run, one audit row | proven (guard) |
| AC-008 | MP `InReadOnlyMode_AnEnglishUser_SeesTheEnglishLine`, MX `InReadOnlyMode_AnEnglishUsersExports_CarryTheEnglishLine` — every read-only cause, English line, 2 export audit rows, 0 Google calls; MP/MX English "not visible" | proven |
| AC-009 | MP `AMarkedCourseNeverReadCompletely_…`, MX `…_PutsTheUndatedRosterPartInC1` | proven |
| AC-010 | MC `TheChoicePage_ShowsTheMarkNextToEachMarkedCourse_AndOffersEveryCourse` (uk, en) — mark+date per marked course, no share on it, no date on the three unmarked, all five offered; MC `…_ShowsTheMarkNextToTheCurrentCourse` | proven |
| AC-011 | MP `ASkippedButUnmarkedCourse_ShowsNoLine`, MX `ASkippedButUnmarkedCourse_LeavesC1Empty` | proven (guard) — see INFO-1 |

## 4. Specification Coverage

- FR-001.1–.5: `MeetCodeScorer.Score` drops `MarkedCourseIds` when collecting candidates; the denominator
  (`datesByAccount.Count`) and per-candidate numerators are unchanged; `Decide` and thresholds untouched; only codes
  without a `meeting_code_link` row are scored (unchanged `GetUnassignedCodesAsync`); the mark is read at each scoring.
- FR-002: `GetMeetCodesQuery` and `GetMeetCodeCourseChoiceQuery` both score through `Score`; a marked course falls into
  `OtherCourses` on the choice page.
- FR-003: `MeetCodesReadSource.GetCourseAbsencesAsync` → `MeetCodeCourseChoicePageModel.Absences` → `CourseChoice.cshtml`
  `Mark(...)` beside other courses and the current course; `SetMeetCodeCourseUseCase` unchanged.
- FR-004: no code path re-examines linked codes.
- FR-005: `DataMarkRules.MeetNotice` (null without a mark — the skip record is not consulted, FR-005.5); carried in
  `MeetReports.CourseNotice` / `MeetingDetail.CourseNotice` (AD-8); views render `DataMarksTexts.MeetNotice`; workbook
  mappers use `MeetExportLayout.CourseRow` (third cell, no row added).
- FR-006: four new keys in uk/en resx, the "not in Classroom" header key and course-list keys reused (I-4); export via
  `IReportTexts` / `LocalizedReportTexts`.
- §9 cost: the marked set is computed from the same membership query, once per batch; reports 1–3 take the mark from the
  drop-down record already loaded; report 4 adds one single-row read. No behaviour outside the Specification found.

## 5. Design Conformance

API and DB designs are NOT_APPLICABLE: no endpoint, route or status code changed; no entity, migration or schema change
in the diff. AD-3: no `DbContext` in Web, the new reads are in Infrastructure behind `IMeetReportSource` /
`IMeetCodesReadSource`. AD-4: no Google call. AD-6: no write added; read-only behaviour unchanged. AD-8: views receive
`MeetCourseNotice` / `CourseAbsence` DTOs, never `Course`.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | test matrix wording of AC-001 page test ("page before the mark") | the US-062 banner above the reports counts marked courses and rightly changes; the comparison starts at the delay notice / course name and still proves FR-005.4 | accepted |
| D-2 | US-028 / US-044 translation tests ("every export text differs between languages") | FR-006 makes the separator «; » the same in both; the journal test pins the exact value, the Meet test lists it with the two "Email" exemptions and still checks it resolves | accepted (see m-1) |
| D-3 | US-053 `FailedRunRecordedTests.ARejectedLinkingWrite_…` world | the old world let US-059 mark courses A and B, which under FR-001 are no longer candidates, so the rejected write never happened; listing A and B keeps the test's subject; assertions unchanged | accepted — the Story's intended behaviour |
| — | FR-003.2 does not say how the mark sits next to the name | the mark is shown in parentheses after the name | accepted — disclosed in report §7, restrained tone kept |

## 7. Report Accuracy

The change set in report §4 matches `git status`: 26 modified source files, 3 new source files, 4 modified tests, 5 new
test files and the test artifacts. Every code and test file is traced. `docs/workflow/findings.jsonl` is modified but not
listed (INFO-2; it is on the Git Policy closed list). No `TODO`/`TBD`/`FIXME`, skipped test or `NotImplementedException`
in the changed files. No secret, database file, `.xlsx` or IDE config.

## 8. Story Test Run

Delegated to `cheap-worker`, 2026-11-01, no files touched:

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `--filter FullyQualifiedName~MarkedCourseMeetLinkingHostTests` | 5/5 |
| `--filter FullyQualifiedName~MarkedCourseMeetCodesPageTests` | 7/7 |
| `--filter FullyQualifiedName~MarkedCourseMeetExportTests` | 9/9 |
| `--filter FullyQualifiedName~MarkedCourseMeetReportsPageTests` | 11/11 |
| `--filter FullyQualifiedName~FailedRunRecordedTests` | 8/8 |
| `--filter FullyQualifiedName~JournalExportTranslationTests` | 92/92 |
| `--filter FullyQualifiedName~MeetExportTranslationTests` | 105/105 |

0 failed, 0 skipped. The full suite is not rerun here (security-reviewer runs it), so the open
`US-061/IMPLEMENTATION/INFO-1` stays open for that stage.

## 9. Findings

- **m-1 (Minor)** — `tests/ClassroomAgent.Tests/Web/Localization/MeetExportTranslationTests.cs`, field
  `SameInBothLanguages`: its summary still says "The two columns that may legitimately read 'Email' in both languages",
  but D-2 added `MeetNoticeSeparator`, which is neither a column nor "Email". Checked against AGENTS.md coding
  conventions (accurate comments). Advisory; loop-back key if fixed: `changes_required`.
- **INFO-1** — The AC-011 tests insert a `sync_skipped_course` row (Google id matches the seeded course) but do not
  assert that the system recognises it (e.g. the journal's "Data not updated" line), so they would also pass if the
  skip were not matched. Low risk: `DataMarkRules.MeetNotice` never reads the skip flag.
- **INFO-2** — Implementation report §4 omits `docs/workflow/findings.jsonl` from the change set (closed-list workflow
  file, in scope).

## 10. Verdict

**PASS** — next stage `SECURITY_REVIEW`.
