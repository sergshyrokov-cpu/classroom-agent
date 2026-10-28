---
artifact_type: implementation_verification
story: US-066
version: 1
status: APPROVED
created_at: 2026-10-28T19:34:17Z
updated_at: 2026-10-28T19:34:17Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: null
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
  - path: docs/designs/api/US-066-api-design.md
    version: 1
  - path: docs/designs/api/US-066-openapi.yaml
    version: 1
  - path: docs/designs/database/US-066-db-design.md
    version: 1
  - path: docs/tests/US-066-test-strategy.md
    version: 1
  - path: docs/tests/US-066-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-066-test-generation-report.md
    version: 1
  - path: docs/evidence/US-066-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-066 Implementation Verification

## 1. Summary

**PASS.** The code does what Specification v1 and the API design say. No Critical or Major finding;
one Minor (m-1) and three INFO notes. Every AC-001 … AC-009 has at least one test that would fail
without the behaviour. Story test classes rerun green (31/31) plus the US-060 class of INFO-1 (15/15).

Delegated: one `cheap-worker` ran the build and the five test classes (§8), touching no files. The
reading of code against the artifacts, of tests against the ACs, and the classification were done here.

## 2. Reviewed Artifacts

As in `inputs` (all v1; the Specification APPROVED; DB_DESIGN NOT_APPLICABLE, no entity model). The
implementation report's `inputs` name the same versions — nothing stale. Working tree: 9 modified or
new files under `src/`, 7 new test files, 4 new Story artifacts (`git status`, ripgrep for untracked).

## 3. AC Verification

| AC | Tests | Verdict |
|---|---|---|
| AC-001 | `NamesakeJournalPageTests.TwoStudentsWithTheSameName_…` (uk, en) — whole headings asserted in order | Proved |
| AC-002 | `NamesakeReportPageTests.TwoStudentsWithTheSameProfileName_…` (uk, en) | Proved |
| AC-003 | `NamesakeReportExportTests.TheNamesakesNameCells_…` (uk, en); `OnlyTheNameCellsText_Changes_TheLayoutIsTheSame` compares sheets, used/merged ranges, row heights, values, notes and name-cell styles of a profile vs email export, and ends asserting the email is present, so it is not vacuous | Proved |
| AC-004 | `NamesakeBlankExportTests.TheBlanksNameCells_…`, `NamesakesInDifferentCopies_BothShowTheirEmail` (I-4: second Petrenko alone in copy 2 still has the email) | Proved |
| AC-005 | `NamesDifferingOnlyInCase_AreNamesakes` (journal, report; uk, en) | Proved |
| AC-006 | four `NoNamesakes_…` tests — names only; files hold no `@` anywhere | Proved for names; see m-1 |
| AC-007 | `TheNameVariantDecides_WhoIsANamesake` (email variant: local parts, no email; profile: emails), `TheSamePartBeforeTheAt_AtTwoDomains_…` (I-3) | Proved |
| AC-008 | `ANamesakeWithoutAnEmail_…` (journal, report, program export) | Proved; blank not separately tested (INFO-1) |
| AC-009 | `ANamesakeWhoLeft_…` (journal, report; uk, en), `ALeavingNamesakesCell_…` (export uk/en, blank) — cell text and note asserted, other namesake has no note | Proved |

Guards: `RowsLabelledByTheirEmail_GetNoSecondEmail` (FR-002.5) is meaningful after implementation — the two
addresses differ only in case (`CourseTestData.Email` keeps case), so `NamesakeRule.Find` marks them and only
the `JournalNameKind.FullName` check in `GetJournalQuery` keeps the second email off.
`TwoTeachersWithTheSameName_AreShownWithoutAnEmail` (FR-001, OD-002 a) — teachers cannot carry the field by
type (`GradingRow`, not `PersonName`).

## 4. Specification Coverage

- FR-001: field on `JournalRow` and `GradingRow` only; `StudentCell` is the one name cell of the program
  layout and of `GroupLessonsBlankMapper` (line 111); Meet reports and the course card untouched.
- FR-002.1: `NamesakeRule.Find` runs once over the whole ordered student list of the journal
  (`GetJournalQuery`) or report (`ReportBuilder`), after the name source; blanks reuse the report's rows.
- FR-002.2: `Find` skips null names; both label rules return null for unnamed rows.
- FR-002.3: `StringComparer.Create(uiCulture, ignoreCase: true)`; no trimming.
- FR-002.5: `GetJournalQuery` passes the email only for `JournalNameKind.FullName`.
- FR-003.1 / VR-001: `NamesakeRule.Email` — null for null or empty email.
- FR-003.4: decision in `Application`; views and mapper only call `NamesakeLabel.Compose`.
- FR-004.1–4: `Compose` → `<name> (<email>)`; the "left" mark keeps its place after the cell content on
  screen; the export note is set exactly as before.
- FR-005: the existing case-sensitive ordering runs before the rule and is not changed.
- FR-006 / §7: no write, no endpoint, policy, log or audit change. VR-002: Razor `@(...)` encodes the
  composed string.
- No behaviour beyond the artifacts.

## 5. Design Conformance

- OpenAPI: `JournalRow.namesakeEmail`, `GradingRow.namesakeEmail`, optional, null when not a namesake —
  as implemented (trailing optional record parameters, api-design §6). No operation change.
- DB: none (NOT_APPLICABLE); no migration needed, none added.
- AD-3: business decision in `Application`; views render only. AD-4: no external call added. AD-6: no
  write path. AD-8: views see DTOs only. One public type per file holds.

## 6. Deviations

- **D-1** (report §7): local renamed `students` in `ReportBuilder` — compile-time only. Sound.
- **Placement of `NamesakeLabel`** (report §7): the composition helper api-design D-3 recommends lives in
  `Application.Models.Dtos` because the views import that namespace and `UseCases` types are internal.
  Disclosed, sound (INFO-2).
- No undisclosed deviation found.

## 7. Report Accuracy

The change set in report §4 equals the diff of `src/`; the test files are untracked and listed as
"from TEST_WRITING, unchanged". All AC-to-test locations in report §3 exist. No `TODO`, `TBD`, `FIXME`,
skipped test or `NotImplementedException` in the changed or new files.

## 8. Story Test Run

`cheap-worker`, 2026-10-28, one class per run:

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `NamesakeJournalPageTests` | 9/9 passed (22.9 s) |
| `NamesakeReportPageTests` | 11/11 passed (14.9 s) |
| `NamesakeReportExportTests` | 7/7 passed (12.3 s) |
| `NamesakeBlankExportTests` | 4/4 passed (14.7 s) |
| `ReportMarksExportTests` (INFO-1 of IMPLEMENTATION) | 15/15 passed (18.5 s) |

The full suite is left to SECURITY_REVIEW.

## 9. Findings

- **m-1 (Minor)** — AC-006 says "the file is the same as before this Story". The `NoNamesakes_…` export
  and blank tests prove names only and no `@` anywhere, not equality with the pre-Story file. The code makes
  it hold (`NamesakeLabel.Compose` returns the name unchanged when the email is null, and the note logic is
  untouched), so this is advisory. Checked against spec AC-006. Loop-back key if pursued: `test_gap`.
- **INFO-1** — AC-008 is not tested on the blank; it is covered through the shared `StudentCell`, which
  the program-layout AC-008 test pins.
- **INFO-2** — `NamesakeLabel` is a formatting helper in the `Models.Dtos` namespace (disclosed, see §6).
- **INFO-3** — the IMPLEMENTATION finding US-066/IMPLEMENTATION/INFO-1 (one stalled ReportMarksExportTests
  run) did not recur: 15/15 in 18.5 s. Consistent with an environment stall; left OPEN for the Owner.

## 10. Verdict

**PASS** → SECURITY_REVIEW.
