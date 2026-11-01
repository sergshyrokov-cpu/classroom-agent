---
artifact_type: test_generation_report
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T14:48:15Z
updated_at: 2026-10-31T14:48:15Z
produced_by: test-writer
inputs:
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
supersedes: null
---

# US-061 Test Generation Report

## 1. Files

**Created** (under `tests/ClassroomAgent.Tests/`):

| File | Cases |
|---|---|
| `TestInfrastructure/MarkedCourseMeetTexts.cs` | — (FR-006 Meet-line texts, uk/en) |
| `Web/Pages/MarkedCourseMeetReportsPageTests.cs` | 11 |
| `Web/Pages/MarkedCourseMeetExportTests.cs` | 9 |
| `Web/Pages/MarkedCourseMeetCodesPageTests.cs` | 7 |
| `Web/BackgroundServices/MarkedCourseMeetLinkingHostTests.cs` | 5 |

**Modified:** none. No existing test or fixture changed.

**Compile-only skeleton:** none — every test is observable over HTTP or through the host's synchronization run and
references no new production declaration (test strategy §2).

**Production code:** not touched.

## 2. Delegation

Test design, the four classes and the texts helper were written in this context. Docker start-up check, the build and
the four story-class runs were delegated to a `cheap-worker` subagent (no edits, report only).

## 3. Commands and results

Docker Desktop had to be started (server 29.8.0).

- `dotnet build ClassroomAgent.sln` — **0 warnings, 0 errors.**
- `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~<Class>`, one class per run:

| Class | Total | Passed | Failed |
|---|---|---|---|
| `MarkedCourseMeetReportsPageTests` | 11 | 2 | 9 |
| `MarkedCourseMeetExportTests` | 9 | 2 | 7 |
| `MarkedCourseMeetCodesPageTests` | 7 | 1 | 6 |
| `MarkedCourseMeetLinkingHostTests` | 5 | 2 | 3 |
| **Total** | **32** | **7** | **25** |

## 4. Red-phase verdict

**Expected failures (25), each for missing behaviour:**

- Pages (9): the line is not on the page — `Assert.Contains` "Sub-string not found" for «Курсу немає в Classroom з
  15.09.2026; склад курсу …», «Курс не видно технічному акаунту з 16.09.2026; …», the English lines (all three read-only
  causes), the undated roster; `OnTheMeetingPage_TheLineComesAfterTheCourseName` fails because the line is absent.
- Exports (7): "C1 is Blank, not text" — no line in C1 (all marks, English, every read-only cause).
- Meet codes (6): the marked course «Test Course Marked Old» is still listed as a candidate (100 %) on "unlinked
  meetings" — both roles and the only-candidate case; the choice page shows it with its share «(100%)» and no mark (uk,
  en); the current-course block has no mark.
- Linking (3): the re-created course gets no link (both marks — `Assert.Single` on an empty collection: 100 % / 100 % is
  ambiguous while the old course is a candidate); the marked 85 % course is still linked (`Expected 0, Actual 1`).

Every failure message matches the scenario; none comes from compilation, fixtures, host configuration or a wrong
assertion.

**Green by design (7):** `AnOrdinaryCourse_ShowsNoLine`, `ASkippedButUnmarkedCourse_ShowsNoLine`,
`AnOrdinaryCourse_LeavesC1Empty`, `ASkippedButUnmarkedCourse_LeavesC1Empty` (no line exists yet — they guard AC-003 and
AC-011 against a line that is too broad); `ChoosingAMarkedCourse_LinksTheCode_AsAPersonsLink_WithTheUsualAudit` (AC-006:
the manual choice already accepts any course; guards against the filter reaching it);
`RemovingAMarkedCandidate_LeavesTheOthersSharesAsBefore` (FR-001.2: 85 % with or without B);
`AnAutomaticLink_StaysWithItsCourse_AfterTheCourseIsMarked` (AC-007: only unlinked codes are scored). Each would fail on
a plausible wrong implementation — a line for any skipped course, a filter applied to the manual choice or to linked
codes, a filter that also changes the denominator — so none is vacuous.

**Not yet exercised:** the "nothing else changes" comparisons in the AC-001 page and export tests (FR-005.4) run after
the line assertion, which fails first. They rely on the host's fixed test clock giving the same "Exported" value in two
exports; IMPLEMENTATION confirms they pass.

## 5. Regression

No production code, existing test or shared fixture changed, so the existing suite is unaffected; the full suite was
not run at this stage. IMPLEMENTATION runs it.

## 6. Coverage

Every Acceptance Criterion AC-001 … AC-011 maps to at least one test (`docs/tests/US-061-ac-test-matrix.md`). Untested:
none. Excluded scenarios with reasons: test strategy §5.

## 7. Open Decisions

None new; OD-001 and OD-002 resolved (a).

## 8. Overall result

**PASS** — tests compile, red for the missing behaviour only, guards green by design.
