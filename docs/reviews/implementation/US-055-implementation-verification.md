---
artifact_type: implementation_verification
story: US-055
version: 1
status: APPROVED
created_at: 2026-10-25T14:30:00Z
updated_at: 2026-10-25T14:30:00Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-055-grade-columns-fit-any-value.md
    version: null
  - path: docs/decisions/US-055-open-decisions.md
    version: 1
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/designs/api/US-055-api-design.md
    version: 1
  - path: docs/designs/database/US-055-db-design.md
    version: 1
  - path: docs/designs/database/US-055-entity-model.md
    version: 1
  - path: docs/tests/US-055-test-strategy.md
    version: 1
  - path: docs/tests/US-055-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-055-test-generation-report.md
    version: 1
  - path: docs/evidence/US-055-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-055 Implementation Verification

## 1. Summary

**PASS.** The code changes exactly what the database design asks for: the three
columns are mapped as unconstrained `numeric`, and migration
`20261025104345_WidenGradeColumns` holds exactly three `AlterColumn<decimal>`
calls with nullability kept and no `USING`. No other production code changed.
The 16 story cases prove the Acceptance Criteria against real PostgreSQL and
the real host; all 16 pass, plus the 6 `AppUserMigrationTests`. No Critical,
Major or Minor finding; three notes for the Owner (section 9).

## 2. Reviewed Artifacts

As in `inputs`. The API design is NOT_APPLICABLE (no endpoint change); no
`openapi` exists by design. The implementation report names the current
version of every input — none is stale.

## 3. AC Verification

| AC | Test(s) | Proves the AC? | Verdict |
|---|---|---|---|
| AC-001 | `LargeGradesSyncTests.LargeMaximumPoints_AreStoredAsReturned_AndTheRunCompletes`; persistence theory row 1 | Run through the real host ends `completed`, `LastError` null, column reads 12345678.5. Fails on `numeric(10,4)` (22003). | met |
| AC-002 | `LargeGradesSyncTests.LargeGrades_AreStoredAsReturned_AndTheRunCompletes`; theory row 1 | Both grades read back exactly; run `completed`. | met |
| AC-003 | `LargeGradesSyncTests.SixDecimalPlaces_AreNotRounded`; theory rows 2–3 | Decimal equality **and** PostgreSQL text `10.123456`, `7.654321` — the text check fails on the old scale 4 (would print `10.1235`). | met |
| AC-004 | `LargeGradesSyncTests.TheCoursesAfterTheUnusualOne_StillLoad_AndTheMeetStepRuns` | Second course's max 1 000 000 (> 999 999.9999); asserts 3 courses + 3 items, `ProcessedCount` 3, Meet port called, `meet_loaded_up_to` set, `completed`. | met |
| AC-005 | `GradeColumnsPersistenceTests.TheMigration_KeepsEveryStoredValueAndNull` (+ `…ExistsUnderTheDesignedName…`, `TheThreeColumns_…`) | Migrates to `AddNotAnAdminAccountRefusal`, seeds 4-decimal values, the old maximum, zero and nulls, applies the rest, compares text row by row. Guards against a vacuous run (asserts the migration exists, row count, old maximum and a null present). | met |
| AC-006 | `LargeGradesPageTests.TheJournal_…`, `ARawPointsReport_…`, `TheBuiltInScaleReport_…` | Dean over HTTP, 200, `2500000 / 12345678,5` on journal and raw-points report; large draft shown for an ungraded student; scale report does not fail. | met |
| AC-006a | `LargeGradesPageTests.TheReportExport_CarriesTheLargeGradeAndMaximum_AsStored` | xlsx produced, a cell carries `2500000 / 12345678,5`. | met |

No vacuous test: every positive test fails on the old schema (the page tests
already at seed, 22003). `NegativeValues_AreStillRefusedByTheExistingChecks` is
green by design (regression guard for the kept CHECK constraints, db-design §3.1).
`ACompletedRunWithLargeValues_LogsNoneOfThem` (S-04) asserts completion first,
so its "not in the log" is not free.

## 4. Specification Coverage

| Req | Where | Status |
|---|---|---|
| FR-001 | `CourseWorkConfiguration.cs:47`, `SubmissionConfiguration.cs:47–48` → `numeric` | implemented |
| FR-002 | no conversion code changed; nullability kept (snapshot, migration) | implemented |
| FR-003 | `20261025104345_WidenGradeColumns.cs` + `.Designer.cs`, snapshot | implemented |
| FR-004 | follows from FR-001; run rules untouched (no change in sync code) | implemented |
| FR-005 | no formatter change; I-3 confirmed by the W tests | implemented |
| VR-001/VR-002 | no validation added or removed | conforms |
| S-01…S-06 | no endpoint/policy/Google call/log/NuGet change; schema via migration only; synthetic values | conforms (security-reviewer checks in depth) |

No behaviour beyond the Specification.

## 5. Design Conformance

- **DB design §3.1** — the three `HasColumnType("numeric")` lines match verbatim.
- **DB design §3.2** — `Up`: three `AlterColumn<decimal>`, `type: "numeric"`,
  `oldType: "numeric(10,4)"`, `nullable: true`, `oldNullable: true`; `Down` the
  lossy reverse, as designed. Snapshot and Designer show `numeric` for the three
  columns; CHECK constraints unchanged by name and text.
- **Entity model** — CLR types stay `decimal?`; no entity, value object or DTO
  changed.
- **AD-3/AD-4/AD-6/AD-8** — only Infrastructure configuration and migration
  touched; invariants unaffected.

## 6. Deviations

| Deviation | Departs from | Reason | Verdict |
|---|---|---|---|
| Journal test changed at IMPLEMENTATION: the large draft is carried by a second, ungraded student | `ac_test_matrix` AC-006 row as written at TEST_WRITING | The journal shows a draft only while no grade is assigned (US-025 `JournalCellRule`); the original test asserted a draft next to an assigned grade — a test defect. The AC (grade and maximum) is unchanged and the large draft is still proven. Disclosed in report §7, matrix row updated, Owner OK 2026-10-25. | accepted |
| `AppUserMigrationTests` count 20 → 21 and name of migration 21 | — | Supporting change every migration-shipping Story makes (US-038, US-051). | accepted |

No undisclosed deviation.

## 7. Report Accuracy

The report's change set (section 4) equals `git status`: 4 modified code/test
files, 2 new migration files, 3 new test classes, plus Story artifacts and
workflow state. Every code and test file is traced. AC-to-test names in the
report and the matrix exist. No `TODO`/`TBD`/`FIXME`, skip or
`NotImplementedException` in the changed files (ripgrep, untracked files
included).

## 8. Story Test Run

Delegated to `cheap-worker` (no files touched), 2026-10-25, one filter term per run:

| Filter | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| `GradeColumnsPersistenceTests` | 7 | 7 | 0 | 0 |
| `LargeGradesSyncTests` | 5 | 5 | 0 | 0 |
| `LargeGradesPageTests` | 4 | 4 | 0 | 0 |
| `AppUserMigrationTests` | 6 | 6 | 0 | 0 |

No build warnings, no Docker/Npgsql timeouts. The full suite was not rerun
here (security-reviewer runs it); the implementation report records 6138/6139
before the registry fix and did not rerun after it.

## 9. Findings

None Critical, Major or Minor. Notes:

- **N-1** The synchronization tests feed `decimal` values through the
  substituted Classroom port, so the reader's `double → decimal` cast
  (`GoogleClassroomReader.cs`) is not exercised — out of scope by spec I-1, and
  the values in the ACs fit `double`'s 15 digits.
- **N-2** The full suite has not run green after the last test change
  (`AppUserMigrationTests`); SECURITY_REVIEW's full run closes it.
- **N-3** `Down` fails with 22003 once any value above 999 999.9999 is stored —
  by design (db-design §3.2), no deployment step runs it.

## 10. Verdict

**PASS** → SECURITY_REVIEW.
