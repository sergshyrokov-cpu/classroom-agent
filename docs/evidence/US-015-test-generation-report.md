---
artifact_type: test_generation_report
story: US-015
version: 1
status: DRAFT
created_at: 2026-09-28T15:03:58Z
updated_at: 2026-09-28T15:03:58Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/designs/api/US-015-api-design.md
    version: 2
  - path: docs/designs/database/US-015-db-design.md
    version: 1
  - path: docs/designs/database/US-015-entity-model.md
    version: 1
  - path: docs/decisions/US-015-open-decisions.md
    version: 2
  - path: docs/tests/US-015-test-strategy.md
    version: 1
  - path: docs/tests/US-015-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-015 Test Generation Report — Sync coursework and submissions

## 1. Result

**PASS.** 85 new tests in 12 new classes, plus 2 new fixtures; one existing test
file modified as the database design foresaw. Build 0 errors / 0 warnings. Whole
suite **2422 total, 2338 passed, 84 failed, 0 skipped** — every failure expected
and classified in §5.

## 2. Files created

Test sources, under `tests/ClassroomAgent.Tests/`:

| File | Tests |
|---|---|
| `Application/UseCases/CourseWorkImportTests.cs` | 10 |
| `Application/UseCases/SubmissionImportTests.cs` | 11 |
| `Application/UseCases/CourseAgeRuleTests.cs` | 7 |
| `Application/UseCases/SubmitterWithoutRosterTests.cs` | 4 |
| `Application/UseCases/CourseWorkRefusalTests.cs` | 5 |
| `Application/UseCases/CourseWorkFailureTests.cs` | 3 |
| `Application/UseCases/CourseWorkInvariantTests.cs` | 8 |
| `Application/UseCases/SubmissionInvariantTests.cs` | 4 |
| `Web/Persistence/CourseWorkSchemaTests.cs` | 11 |
| `Web/Persistence/SubmissionSchemaTests.cs` | 12 |
| `Web/Configuration/RetentionConfigurationTests.cs` | 5 |
| `Web/Logging/SubmissionImportLoggingTests.cs` | 5 |
| **Total** | **85** |

Fixtures: `TestInfrastructure/CourseWorkTestData.cs` (the migration suffix, the
two table names, every constraint and index name, the two resource codes, the
seven state codes, the unrecognised raw state, synthetic generators) and
`TestInfrastructure/CourseWorkRows.cs` (direct `INSERT` helpers that bypass the
entities, so a **database** constraint is tested apart from a domain guard).

## 3. Files modified

| File | Change |
|---|---|
| `TestInfrastructure/SyncWorld.cs` | the in-memory `ClassroomReader` implements the two new port members with seeding, failure injection and the `CourseWorkRead` / `SubmissionsRead` counters; two new in-memory repositories; `Retention` and the three new use-case arguments. `retentionYears` defaults, so every existing construction compiles unchanged |
| `TestInfrastructure/FakeClassroomReader.cs` | the same two members for the host double — empty unless seeded, which is what keeps host tests offline (US-014 D-4) |
| `Infrastructure/Persistence/AppUserMigrationTests.cs` | **expected change**, db-design §10 item 14: seven migrations to **eight** with `_AddCourseWorkAndSubmissions` last, table set nine to **eleven**. Its other five tests keep their own assertions untouched, `TheControlPlaneSchema_IsUnchangedByThisStory` included |

## 4. Production code: the OD-012 skeleton

OD-012 authorised a **compile-only skeleton**, created before the tests because a
test that does not compile is not a red test. 16 production files created or
modified, every body throwing `NotImplementedException`: the two entities and
their detail records, the three enums, the two port members, three Application
models, `RetentionSettings`, the two repository ports with their Infrastructure
implementations, their DI registration, and three new parameters on
`RunSynchronizationUseCase`.

**Deliberately outside the skeleton** (OD-012): the EF Core configurations, the
two `DbSet` properties and the `AddCourseWorkAndSubmissions` migration. The
consequence is stated plainly rather than hidden — **the two tables do not exist
yet**, so the 23 schema tests fail on a missing relation (`42P01`), not on a wrong
constraint. Until the migration exists they cannot tell a correct constraint from
an absent one.

**Debt IMPLEMENTATION must clear**, each recorded here so a reviewer can check it:

1. every `NotImplementedException` introduced by the skeleton;
2. three `#pragma warning disable CS9113` — `CourseWorkRepository`,
   `SubmissionRepository` and `RunSynchronizationUseCase`;
3. the placeholder `services.AddSingleton(new RetentionSettings(0))` in
   `InstallationServices`, which exists only so the host still starts;
4. the log event name `SyncSubmissionStateUnrecognised`, which the tests expect
   for OD-005's Warning line — no document fixes it, so **IMPLEMENTATION must
   match the tests** or change both together;
5. the configuration key `Retention:Years` (spec I-2) has no constant in
   `InstallationSettingsReader` yet; the tests use the literal.

## 5. Execution evidence

Commands: `dotnet build ClassroomAgent.sln`, then `dotnet test ClassroomAgent.sln`
and per-class `dotnet test --filter FullyQualifiedName~<Class>`. Docker running
for Testcontainers (TC-2). `--nologo` was never passed.

Build: **0 errors, 0 warnings**.

Whole suite: **2422 total, 2338 passed, 84 failed, 0 skipped.**

### 5.1 The 84 failures, classified

| Class | Tests | Failed | Reason |
|---|---|---|---|
| `CourseWorkImportTests` | 10 | 10 | no coursework step in the run |
| `SubmissionImportTests` | 11 | 11 | idem |
| `CourseAgeRuleTests` | 7 | 7 | the age rule does not exist |
| `SubmitterWithoutRosterTests` | 4 | 4 | FR-007 not implemented |
| `CourseWorkRefusalTests` | 5 | 4 | the **control** fails — see §6 |
| `CourseWorkFailureTests` | 3 | 3 | no step to fail inside |
| `CourseWorkInvariantTests` | 8 | 8 | `CourseWork.Import` throws `NotImplementedException` |
| `SubmissionInvariantTests` | 4 | 4 | `Submission.Import` / `SetState` throw |
| `CourseWorkSchemaTests` | 11 | 11 | `42P01: relation "course_work" does not exist` |
| `SubmissionSchemaTests` | 12 | 11 | idem for `submission` |
| `RetentionConfigurationTests` | 5 | 5 | the placeholder ignores the setting, so the host never refuses to start |
| `SubmissionImportLoggingTests` | 5 | 5 | the control finds no rows |
| `AppUserMigrationTests` | 6 | **1** | the expected count change; the migration does not exist yet |

83 of the 84 are new tests; the 84th is the one foreseen change to
`AppUserMigrationTests`.

### 5.2 No other test regressed

`dotnet test` over everything outside this Story's classes: **2318 total, 2318
passed, 0 failed.** Named checks, all green: `CourseImportTests` (12),
`RosterImportTests` (12), `CourseInvariantTests` (12),
`CourseImportRefusalTests` (6), `SynchronizationRunTests` (7),
`SynchronizationRefusalTests` (8).

## 6. Tests that pass before implementation, and why

Two, both investigated rather than left unexplained.

1. **`CourseWorkRefusalTests.PermittedServiceWrites_DoesNotGrow`** — asserts that
   `RunSynchronizationUseCase` is still absent from `PermittedServiceWrites`
   (FR-001, BR-026). True today and it must **stay** true: it guards against
   IMPLEMENTATION adding the new writes to the read-only exemption list instead of
   leaving them inside the guarded use case. A structural invariant, not a
   behaviour awaiting code.
2. **`SubmissionSchemaTests.AScheduledRun_WritesNoAuditRow`** — `audit_event`
   stays empty whether or not the coursework pipeline runs (FR-020). It passes for
   the same reason it will keep passing, and it fails the moment a Story gives a
   scheduled run an audit row.

## 7. Vacuous passes found and fixed

Five tests were written in a shape that passes while the pipeline has no step.
All five were corrected **before** this report, because the point of the red phase
is that green later means something:

- **`CourseAgeRuleTests.ProcessedCount_CountsCoursesNotItems`** asserted only
  "the counter is 1", which is equally true of a run that imported three items
  under one course and of a pipeline that imported nothing. It now also asserts
  the three items and two submissions really arrived — which is what makes the
  count mean *courses, not items*.
- **`CourseAgeRuleTests.CourseExactlyAtTheBoundary_IsImported`** asserted only
  "the course was imported", which US-014 already does unconditionally. It now
  asserts the coursework and submission arrived too, so it tests the boundary
  rather than US-014.
- **The three `SubmissionImportLoggingTests` absence tests** passed because a run
  that imports nothing writes no grade, no name and no title either. Each now
  calls `AssertTheImportHappenedAsync` first — the US-014
  `CourseImportLoggingTests` pattern — so the absence is asserted **inside a run
  that really imported**.

The shape behind all five is the one US-014 recorded: **an assertion that
something did not happen is free in a world where nothing happens.** The read-only
tests were written correctly from the start, each pairing its refusal with a
control in the same test, which is why they fail on the control rather than
passing vacuously.

## 8. Untested Acceptance Criteria

None. AC-001 … AC-010 all have mapped, executing tests
(`docs/tests/US-015-ac-test-matrix.md`).

## 9. Coverage gaps IMPLEMENTATION must close

1. **The schema tests cannot yet distinguish a correct constraint from a missing
   table** (§4). When the migration lands, IMPLEMENTATION must confirm each of the
   23 schema tests fails for the *right* reason if the constraint is wrong — in
   particular the three **positive** guards: the same Google id in two courses,
   the same submission id under another piece of work, and two submissions of one
   item by one student must all **succeed**.
2. **`SubmissionImportLoggingTests` asserts absence by string search** over the
   log files. A grade of `80` is a short string; IMPLEMENTATION should keep the
   fixture's grade value distinctive (`91.25`) so the assertion cannot pass by
   accident, and must not introduce a line that logs a count equal to a grade.
3. **The OD-005 Warning line's property names** (`RawState`,
   `SubmissionGoogleId`) and its event name are fixed **by these tests only**. If
   IMPLEMENTATION prefers other names it changes both sides together and records
   it as a deviation.

## 10. Limitations, not gaps

Inherited from the test strategy §7 and restated so they are not mistaken for
coverage: Google's real response shapes are only as accurate as the synthetic
fixtures (TC-4), and a renamed field would pass here and fail in production;
paging is proved with **two** pages; whether Classroom returns the submissions of
a student removed from a roster is `trebovaniya.md` §7 item 14, **still open**
(OD-010), so AC-004's tests prove the program's rule and never Google's
behaviour; the due-date fallback of db-design §3.4 is asserted as the program's
behaviour, not as a shape Classroom can produce; and
`STUDENT_EDITED_AFTER_TURN_IN` is asserted only as a permitted stored value,
because OD-011 records that it may not exist in the API.

## 11. Fixture note for SECURITY_REVIEW

Every value in `CourseWorkTestData` and `CourseWorkRows` is invented; the test
domain is `school-one.example.test`, inherited from US-014. A check confirmed no
skipped test, no `TODO` / `FIXME` marker and no real address, name or school
domain in any new test file. Grades are synthetic numbers.

## 12. Open Decisions

- **OD-012** was raised by this stage and resolved by the Owner the same day
  (`open_decisions` v2), scoped exactly as US-005 OD-002, US-007 OD-003, US-011
  OD-006, US-012 OD-005, US-013 OD-008 and US-014 OD-012 were scoped.
- OD-001 … OD-011 arrived resolved; nothing in this stage reopened one.
- No blocking Open Decision remains.
