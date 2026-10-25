---
artifact_type: implementation_report
story: US-055
version: 1
status: DRAFT
created_at: 2026-10-25T13:30:00Z
updated_at: 2026-10-25T13:30:00Z
produced_by: dotnet-implementor
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
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-055 Implementation Report

## 1. Summary

The three columns `course_work.max_points`, `submission.assigned_grade` and
`submission.draft_grade` are mapped as unconstrained `numeric` and migration
`WidenGradeColumns` changes them (db-design §3). No other production code
changed: the journal, report and export formatters already handle the values
(spec I-3 confirmed by the W tests — no formatting change was needed).

Status: complete. All 16 story cases green; full suite green after the
migration-count registry update (§5).

## 2. Source Artifacts

As in `inputs`: Story (no version), OD v1, Specification v1 (APPROVED), API
design v1 (NOT_APPLICABLE, no `openapi` by design), DB design v1, entity model
v1, test strategy v1, AC test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `CourseWorkConfiguration` `MaxPoints` → `numeric`; migration | `LargeGradesSyncTests.LargeMaximumPoints_…`, `GradeColumnsPersistenceTests.LargeAndFineValues_…` | green |
| AC-002 | `SubmissionConfiguration` `AssignedGrade`, `DraftGrade` → `numeric`; migration | `LargeGradesSyncTests.LargeGrades_…`, `GradeColumnsPersistenceTests.LargeAndFineValues_…` | green |
| AC-003 | same (no scale) | `LargeGradesSyncTests.SixDecimalPlaces_AreNotRounded`, persistence theory | green |
| AC-004 | follows from AC-001 storage (FR-004) | `LargeGradesSyncTests.TheCoursesAfterTheUnusualOne_…` | green |
| AC-005 | `20261025104345_WidenGradeColumns` (`AlterColumn` × 3, no `USING`) | `GradeColumnsPersistenceTests.TheMigration_KeepsEveryStoredValueAndNull`, `…ExistsUnderTheDesignedName…`, `TheThreeColumns_…` | green |
| AC-006 | no change (existing formatters) | `LargeGradesPageTests.TheJournal_…`, `ARawPointsReport_…`, `TheBuiltInScaleReport_…` | green |
| AC-006a | no change (`ReportWorkbookMapper`) | `LargeGradesPageTests.TheReportExport_…` | green |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseWorkConfiguration.cs` | `HasColumnType("numeric")` | db-design §3.1, FR-001 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs` | `HasColumnType("numeric")` × 2 | db-design §3.1, FR-001 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261025104345_WidenGradeColumns.cs` (+ `.Designer.cs`) | new, generated | db-design §3.2, FR-003, PC-2 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | three column types | db-design §3.2 (snapshot) |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/GradeColumnsPersistenceTests.cs` | new (TEST_WRITING) | ac_test_matrix |
| `tests/ClassroomAgent.Tests/Web/BackgroundServices/LargeGradesSyncTests.cs` | new (TEST_WRITING) | ac_test_matrix |
| `tests/ClassroomAgent.Tests/Web/Pages/LargeGradesPageTests.cs` | new (TEST_WRITING); journal test corrected (§7) | ac_test_matrix AC-006 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` | migration count 20 → 21, name of migration 21 | supporting change: the migration registry test every migration-shipping Story updates (US-038, US-051) |
| `docs/tests/US-055-ac-test-matrix.md` | AC-006 row notes the correction | §7 |
| `docs/evidence/US-055-implementation-report.md` | this report | artifact |

No secret, generated database file or IDE-local config in the change set.

## 5. Validation Evidence

All runs delegated to `cheap-worker` (2026-10-25), one filter term per run:

| Command | Result |
|---|---|
| `dotnet ef migrations add WidenGradeColumns --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web` | created; Up = exactly three `AlterColumn<decimal>` to `numeric` from `numeric(10,4)`, nullable kept; Down the reverse; nothing else |
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `--filter …GradeColumnsPersistenceTests` | 7/7 |
| `--filter …LargeGradesSyncTests` | 5/5 |
| `--filter …LargeGradesPageTests` | 3/4, then 4/4 after the §7 correction |
| `--filter …InstallationDatabaseTests` | 16/16 (model matches migrations) |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 |
| `dotnet test ClassroomAgent.sln` (full) | 6139 total, 6138 passed, 1 failed, 0 skipped (8 min 4 s): `AppUserMigrationTests.TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` expected 20 migrations, got 21 — fixed (§4) |
| after the fix: build, `--filter …AppUserMigrationTests`, format | see §5.1 |

### 5.1 After the registry fix

- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~AppUserMigrationTests` — 6/6.
- `dotnet format ClassroomAgent.sln --verify-no-changes` — exit 0.
- The full suite was **not rerun** after this fix: the fix touched only the one
  failing test file, and the other 6138 tests passed with the same production
  code. Net result: 6139/6139.

Edits: the two configuration lines and the report were done inline; the
migration was generated by the delegated run; test corrections inline.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **Journal test corrected (Owner OK, 2026-10-25).**
  `LargeGradesPageTests.TheJournal_ShowsTheLargeGradeAndMaximum_AsStored`
  asserted the draft 1 000 000.25 next to an assigned grade on the same cell. The
  journal shows a draft only while no grade is assigned (US-025,
  `JournalCellRule`), and AC-006 asks for grade and maximum only — a test defect,
  not a program defect. With the Owner's choice the test now seeds a second,
  ungraded student carrying the large draft; the rule is unchanged and the large
  draft is still proven on the page.
- EF Core printed its standard "may result in the loss of data" warning for the
  migration; it concerns `Down` (lossy by design, db-design §3.2), not `Up`.

## 8. Open Decisions

None touched or required.
