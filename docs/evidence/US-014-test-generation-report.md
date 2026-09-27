---
artifact_type: test_generation_report
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T19:14:37Z
updated_at: 2026-09-27T19:14:37Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: docs/designs/database/US-014-entity-model.md
    version: 2
  - path: docs/decisions/US-014-open-decisions.md
    version: 2
  - path: docs/tests/US-014-test-strategy.md
    version: 1
  - path: docs/tests/US-014-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-014 Test Generation Report — Sync courses and rosters

## 1. Test files created

| File | Level | Tests |
|---|---|---|
| `tests/.../Application/UseCases/CourseImportTests.cs` | Application | 11 |
| `tests/.../Application/UseCases/RosterImportTests.cs` | Application | 11 |
| `tests/.../Application/UseCases/CourseImportRefusalTests.cs` | Application, security | 6 |
| `tests/.../Infrastructure/Google/GoogleClassroomReaderTests.cs` | adapter, offline transport | 6 |
| `tests/.../Web/Persistence/CourseSchemaTests.cs` | integration (Testcontainers) | 11 |
| `tests/.../Web/Persistence/ClassroomParticipantSchemaTests.cs` | integration | 7 |
| `tests/.../Web/Persistence/CourseMembershipSchemaTests.cs` | integration | 13 |

Fixtures created: `TestInfrastructure/CourseTestData.cs` (names, codes, synthetic
data), `TestInfrastructure/CourseRows.cs` (direct SQL inserts with
constraint-satisfying defaults).

## 2. Test files modified

| File | Change | Why |
|---|---|---|
| `TestInfrastructure/SyncWorld.cs` | four ports added in memory — the Classroom reader and the three repositories — and passed to the run | the use case's constructor grew; empty by default so every US-013 test stays true |
| `Infrastructure/Persistence/AppUserMigrationTests.cs` | six → **seven** migrations, `_AddCoursesAndRosters` last, three tables added to the set | the expected change db-design §9 item 10 foresaw; US-013 made the previous one |
| `Infrastructure/Persistence/DeanAccountSchemaTests.cs` | sample table set grew by three | each keeps **its own** assertion — that *its* Story adds no table; only the sample grew |
| `Web/Persistence/AccessCheckAuditSchemaTests.cs` | idem | idem |
| `Web/UseCases/AdminLoginCheckEveryTimeTests.cs` | idem | idem; its two "no table names allowed/admin" assertions are untouched |

## 3. Production files touched, and why a test-writing stage touched them

Under **OD-012** (resolved by the Owner), a compile-only skeleton was created: every
body throws `NotImplementedException`. Created: `Course`, `ClassroomParticipant`,
`CourseMembership`, `CourseDetails`, `CourseState`, `ClassroomRole`,
`IClassroomReader`, three repository ports, `CourseSnapshot`, `CourseRoster`,
`RosterEntry`, `GoogleClassroomReader`, and three repository implementations.
Modified: `RunSynchronizationUseCase` (four constructor parameters) and
`InstallationServices` (registration only).

**The skeleton grew twice beyond OD-012's first description**, both times out of
necessity rather than convenience, and OD-012 now records both:

1. `GoogleClassroomReader` — paging is a property of the adapter and invisible at the
   port by design (VR-005), so the test that proves it must construct the adapter.
2. The three repositories **and their DI registration** — without them the host cannot
   construct `RunSynchronizationUseCase`, and **eight US-013 tests failed on a host
   that would not start**. A stage that leaves regression tests red has not finished.

Deliberately **not** in the skeleton, as US-013 decided for itself: the EF Core entity
configurations, the three `DbSet` properties and the `AddCoursesAndRosters` migration.
Mapping an entity without its migration would leave the model and the database
disagreeing, so `IMPLEMENTATION` adds the mapping, the check constraints, the indexes
and the migration as one piece.

**`IMPLEMENTATION` must remove every `NotImplementedException` and every
`#pragma warning disable CS9113`** the skeleton needed (in `RunSynchronizationUseCase`
and the three repositories). A remaining one is a finding — the rule US-013 set.

## 4. A correction this stage forced on DB_DESIGN

Building the skeleton from `entity-model` v1 exposed **two defects in that document**,
both found by compiling it rather than by reading it:

1. `CourseDetails` was placed in `Application/Models` while being a parameter of
   `Course.Import`. `Course` is in `Domain`, and `Domain` references nothing —
   Architecture Invariant 1 (AD-3). The combination does not compile (`CS0234`).
   **Corrected:** `CourseDetails` is a `Domain` type. A `ProjectReference` from
   `Domain` to `Application` was rejected as a plain breach of AD-3, and expanding the
   record into primitives would give `Import` eleven parameters.
2. The port signatures gave the adapter no way to know **whose** data it reads. Every
   Classroom call impersonates the technical account of `WorkspaceConnection` (BR-015),
   and `GoogleAccessProbe` takes that account per call. **Corrected:** both members
   take the impersonation address as their first parameter; the use case already holds
   it.

`entity-model` is now v2 and records both. The db-design (v1) is untouched: neither
correction changes a table, column, constraint or index.

## 5. Commands run

- `dotnet build ClassroomAgent.sln` — **0 errors, 0 warnings** (with
  `TreatWarningsAsErrors`, so this is also the nullable-annotation check).
- `dotnet test --filter "FullyQualifiedName~CourseImportRefusalTests"` — used to verify
  the red phase after the vacuity fix of §7.
- `dotnet test --filter "FullyQualifiedName~SyncReadinessTests"` and the three other
  US-013 background-service classes — used to confirm the regression was cleared:
  **9/9 and 28/28 green**.
- `dotnet test ClassroomAgent.sln` — the whole suite; results in §6.

`--nologo` was not used: it reaches the test application and fakes a "zero tests ran"
failure in this project.

## 6. Execution evidence

### 6.1 Whole suite

`dotnet test ClassroomAgent.sln`, Docker running throughout so Testcontainers met no
obstacle (TC-2):

| | Count |
|---|---|
| Total | **2321** |
| Passed | **2252** |
| Failed | **69** — every one expected, classified in §6.2 (70 after the §7.1 fix, which turned one vacuous pass red) |
| Skipped | **0** |
| Duration | 2 m 56 s |

Before this Story the suite was 2253 tests, all green (US-013 evidence). The stage
therefore added **68 tests**, and the 69th failure is an existing test whose expectation
this Story deliberately changed (`AppUserMigrationTests`, §2).

**Every test outside this Story's scope passes.** That matters more than the failure
count: the eight US-013 background-service tests that broke mid-stage — on a host that
could not start, §3 — are green again, and no other Story's test was disturbed.

### 6.2 Expected failures, by class

| Class | Failures | Cause |
|---|---|---|
| `CourseMembershipSchemaTests` | 13 | the three tables do not exist yet — the EF configurations and the migration are deliberately outside the skeleton (OD-012) |
| `CourseSchemaTests` | 12 | idem |
| `ClassroomParticipantSchemaTests` | 7 | idem (6 before the vacuity fix of §7.1, 7 after) |
| `RosterImportTests` | 11 | the skeleton's bodies throw `NotImplementedException` |
| `CourseImportTests` | 11 | idem |
| `CourseImportRefusalTests` | 6 | idem |
| `GoogleClassroomReaderTests` | 6 | idem |
| `AppUserMigrationTests` | 1 | asserts the post-implementation counts: seven migrations, three tables added |
| `DeanAccountSchemaTests` | 1 | its table-set sample now includes the three new tables |
| `AccessCheckAuditSchemaTests` | 1 | idem |
| `AdminLoginCheckEveryTimeTests` | 1 | idem |

**No other class fails.** Confirmed by three targeted runs during the stage: the US-013
Application and schema classes (`SynchronizationRunTests`, `SynchronizationRefusalTests`,
`SyncStateInvariantTests`, `SyncStateSchemaTests`, `SyncIntervalConfigurationTests`)
**49/49 green**, `SyncReadinessTests` **9/9 green**, and
`SyncScheduleTests` + `SyncLoggingTests` + `SyncRunCoordinationTests` **28/28 green**.

The schema tests fail **on missing tables, not on a wrong constraint**. That is the
expected red phase for them, and it is worth stating plainly: until the migration
exists they cannot distinguish a correct constraint from an absent one.

The schema tests fail **on missing tables, not on a wrong constraint**. That is the
expected red phase for them, and it is worth stating plainly: until the migration
exists they cannot distinguish a correct constraint from an absent one.

## 7. A vacuity defect found and fixed in this stage

The first run of `CourseImportRefusalTests` had **5 of 6 tests passing before any
implementation existed**. They were not weak assertions — they were vacuous ones: "the
Classroom port was not called" and "nothing was written" are equally true of a run that
correctly refused **and** of a pipeline that has no step at all. Green there would have
meant nothing, and would have stayed green if the read-only guard were later removed.

Each of the six now pairs the refusal with a **control**: the same seeded world,
allowed to run, must reach the port and write. All six fail before implementation and
will only pass when the refusal is real. This is the red-phase rule applied rather than
recited, and it is recorded here because the report would otherwise show five
"already-satisfied" tests with no explanation.

### 7.1 A second vacuity, in a schema test

The same check on the final run found one more:
`ClassroomParticipantSchemaTests.TheTable_HasNoRoleColumn` passed, because on a database
without the table `information_schema` returns nothing and every `DoesNotContain`
succeeds. It asserted an absence inside an absence.

Fixed by asserting the table's presence first (`google_user_id` must be there), so the
test now fails with the rest and becomes a real guard against a `role` column once the
migration exists. That class went from 6 failures to 7 — the seventh is the fix.

**Both vacuities shared one shape**: an assertion that something did not happen, in a
world where nothing happens yet. Worth remembering for later Stories, since the red
phase makes that shape easy to write and easy to miss.

## 8. Untested Acceptance Criteria

None. All nine have at least one mapped scenario in the `ac_test_matrix`, and each has
at least one scenario that fails before implementation.

## 9. Coverage limits `IMPLEMENTATION` and `SECURITY_REVIEW` should know

- **Google's real response shapes are only as accurate as the fixtures.** The adapter
  tests assert against synthetic JSON written from Google's documented shapes; a
  renamed field would pass here and fail in production. US-011's live "check access"
  is the mitigation, not a test.
- **Paging is proved with two pages.** A token mishandled only after the second page
  would not be caught.
- **The both-rosters tie-break tests the program's rule, not Google's behaviour.**
  Whether Classroom can return one person on both rosters of a course was never
  verified on a live domain (spec I-9 says so).
- **`ImpersonatedAs` and the JWT `sub` claim prove the address reaches Google's token
  endpoint**, not that Google honours it.
- **No log-content test was written at host level.** SC-10 compliance for the new lines
  is asserted only through the stored `SyncState` diagnosis (`AFailedImport_...`); the
  host-level assertion that a run's log lines carry no course name or address is left
  to `IMPLEMENTATION`, which is where those lines are written. This is a **gap to
  close**, recorded rather than hidden: US-013's `SyncLoggingTests` is the precedent to
  extend.
- **Truncation (VR-002) is not tested.** The bounds are asserted in the schema, but the
  entity's cut-rather-refuse behaviour has no test, because `Course.Import` throws in
  the skeleton. `IMPLEMENTATION` must add it.

## 10. Open Decisions

All twelve are resolved (open-decisions v2). OD-012 was raised and resolved during this
stage; OD-010 and OD-011 shape two tests each, as §12 of the test strategy records.

## 11. Overall result

**PASS.** 68 new tests across seven classes and two fixtures; five existing test files
modified, four of them the expected table-set and migration-count changes db-design §9
foresaw. Build 0 errors / 0 warnings. Whole suite 2321 total, 2252 passed, 69 failed,
0 skipped — every failure expected and classified, every test outside this Story's scope
green.

Three things a reader should carry forward rather than discover later:

1. **This stage corrected DB_DESIGN twice** (§4). Both defects were found by compiling
   the design, not by reading it: a `Domain` type taking an `Application` type (AD-3),
   and a port with no way to know whose data it reads (BR-015). `entity-model` is v2.
2. **Six tests passed vacuously at first** — five refusal tests and one schema test —
   and were fixed rather than accepted (§7, §7.1). Both had the same shape: an assertion
   that something did not happen, in a world where nothing happens yet.
3. **Two coverage gaps are `IMPLEMENTATION`'s to close** (§9): a host-level assertion
   that a run's log lines carry no course name or address (SC-10), and a test of the
   entity's cut-rather-refuse truncation (VR-002). Both are impossible now — the
   skeleton throws — and neither should be forgotten because this report passed.
