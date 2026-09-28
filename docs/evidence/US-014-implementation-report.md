---
artifact_type: implementation_report
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-28T07:05:00Z
updated_at: 2026-09-28T07:05:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-014-sync-courses-and-rosters.md
    version: null
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/decisions/US-014-open-decisions.md
    version: 2
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: docs/designs/database/US-014-entity-model.md
    version: 2
  - path: docs/tests/US-014-test-strategy.md
    version: 1
  - path: docs/tests/US-014-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-014 Implementation Report — Sync courses and rosters

## 1. Summary

The synchronization run of US-013 has its first pipeline step. A run now reads the
school's courses from Classroom — every page, with no `courseStates` and no per-user
filter — reads **both** rosters of each course, and commits one course at a time with
its people and their memberships. `SyncState.ProcessedCount` reports courses
processed, so the counter stops being zero.

Three tables exist where there were none: `course`, `classroom_participant` and
`course_membership`, created by the single migration `AddCoursesAndRosters`. This is
the first Story whose database holds personal data of students, and it deliberately
adds **no** way to read it: no endpoint, no page, no export (spec FR-018, S-08).

What the skeleton of OD-012 left behind is gone: every `NotImplementedException` and
every `#pragma warning disable CS9113` of this Story is removed, and no "compile-only
skeleton" remark of US-014 remains.

**Validation:** build 0 errors / 0 warnings; `dotnet test` **2337 passed, 0 failed, 0
skipped**; `dotnet format --verify-no-changes` clean. TEST_WRITING left 69 red tests
and two recorded coverage gaps; all 69 are green and both gaps are closed by 16 new
tests.

**Limitations, all of them recorded in the artifacts rather than discovered here:**
retry, backoff and the Admin-facing diagnosis are US-017 (FR-014, OD-008); nothing
re-uses what is already known, so every run is a full pass (OD-007); the "course older
than N years" rule is not applied (OD-001); and a course Google no longer returns is
left untouched, with no flag of any kind (FR-011, I-4).

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-014-sync-courses-and-rosters.md` | — |
| Specification (APPROVED at `HUMAN_SPEC_APPROVAL`, 2026-09-27T17:20:48Z) | `docs/specifications/US-014-spec.md` | 2 |
| Open Decisions (OD-001 … OD-012, all resolved) | `docs/decisions/US-014-open-decisions.md` | 2 |
| API design (`NOT_APPLICABLE`; no `openapi.yaml` by recorded decision) | `docs/designs/api/US-014-api-design.md` | 2 |
| Database design | `docs/designs/database/US-014-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-014-entity-model.md` | 2 |
| Test strategy | `docs/tests/US-014-test-strategy.md` | 1 |
| AC ↔ test matrix | `docs/tests/US-014-ac-test-matrix.md` | 1 |
| Requirements | `trebovaniya.md` | 79 |

No input is `SUPERSEDED`. The Specification, api-design, db-design and entity-model
record `open_decisions` at version 1 while the file is now version 2; version 2 adds
**OD-012 only** (the compile-only skeleton, raised and resolved during TEST_WRITING)
and changes no requirement, no endpoint and no table, so nothing downstream was read
against a value that has since changed.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 courses imported, paged, with the §3 fields and the state as Classroom reports it | `GoogleClassroomReader.ReadCoursesAsync` (paging, `PageSize = 100`), `RunSynchronizationUseCase.ImportCoursesAsync`, `Course.Import` | `CourseImportTests` (9 facts), `GoogleClassroomReaderTests.TheCourseList_IsPagedToTheEnd`, `CourseSchemaTests` | PASS |
| AC-002 a second run duplicates nothing and updates in place | `ICourseRepository.GetByGoogleIdAsync` + `Course.UpdateFrom`; `ClassroomParticipant.UpdateFrom` | `CourseImportTests.ASecondRun_AddsNoCourseAndKeepsTheIdentity`, `…ARenamedCourse_IsUpdatedInPlace`, `CourseSchemaTests.ADuplicateGoogleId_IsRejected` | PASS |
| AC-003 roster imported with the role on the membership, co-teachers included | `ReadRosterAsync` (both lists), `RunSynchronizationUseCase.RosterOf` (teacher-wins tie-break), `CourseMembership` | `RosterImportTests` (9 facts), `GoogleClassroomReaderTests.ARoster_ReadsBothListsPagedToTheEnd`, `CourseMembershipSchemaTests` | PASS |
| AC-004 first seen / last seen / on roster; never deleted; a return reuses it | `CourseMembership.FirstSeen`, `SeenAgain`, `NotOnRoster`; the off-roster pass of `ImportCourseAsync` | `RosterImportTests`, `CourseInvariantTests.LeavingTheRoster_KeepsTheLastSighting`, `CourseImportLoggingTests.ADepartureFromARoster_IsCountedAndTheMembershipSurvives` | PASS |
| AC-005 a course Google no longer returns is left alone | nothing marks, hides or deletes: no `MarkMissing`, no `Delete` member anywhere | `CourseImportTests.ACourseClassroomStoppedReturning_IsLeftUntouched` | PASS |
| AC-006 read-only mode never reaches the port and writes nothing | `IReadOnlyModeGuard.EnsureAllowedAsync` as the first statement, unchanged from US-013 | `CourseImportRefusalTests` (6 facts, each with its control case) | PASS |
| AC-007 a failure part-way leaves a consistent database and a failed `SyncState` | `IUnitOfWork.ExecuteInTransactionAsync` per course; the US-013 failure handler untouched | `CourseImportTests.ARunThatFailsPartWay_…`, `…AFailedImport_StoresADiagnosisWithNoPayload` | PASS |
| AC-008 no personal data in a log; nothing leaves the installation; no audit row | `SkippedCourse` carries an id and a state only; `LogCourseSkipped` / `LogRunCompleted` carry counters and ids | `CourseImportLoggingTests` (4 facts), `SyncLoggingTests.AScheduledRun_WritesNoAuditRow` | PASS |
| AC-009 tests never reach Google; the schema is tested on real PostgreSQL; paging and the BR-051 sequence covered | `FakeClassroomReader` (host) and `SyncWorld.ClassroomReader` (Application); `ScriptedHttpHandler` for the adapter | the whole suite: 2337 passed, 0 skipped | PASS |

## 4. Change Set

Every file traces to the Specification, a design element, an `ac_test_matrix` test, or
is the named supporting change beside it.

### Production code — created

| File | Trace |
|---|---|
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseConfiguration.cs` | FR-017; db-design §3 (columns, bounds, `uq_course_google_id`, `ck_course_course_state`) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/ClassroomParticipantConfiguration.cs` | FR-006, FR-017; db-design §4 (unique `google_user_id`, **non-unique** email index — OD-011) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseMembershipConfiguration.cs` | FR-007, FR-017, VR-004; db-design §5 (unique `(course_id, participant_id)`, two check constraints, two `Restrict` foreign keys, the roster-on-a-date composite) |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20260927201534_AddCoursesAndRosters.cs` (+ `.Designer.cs`) | FR-017, PC-2; db-design §6 — the one migration of this Story |
| `src/ClassroomAgent.Application/Models/SkippedCourse.cs` | FR-003, FR-016, OD-010 — supporting change: the Application layer has no logger, so the run reports the skip and the host writes the `Warning` line (see §7 D-1) |

### Production code — modified

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Entities/Course.cs` | FR-005, VR-002: `Import` / `UpdateFrom`, truncation against the public bounds, blank optional → absent |
| `src/ClassroomAgent.Domain/Entities/ClassroomParticipant.cs` | FR-006, VR-003: `Import` / `UpdateFrom`, address trimmed and lower-cased through `WorkspaceConnection.NormalizeEmail` |
| `src/ClassroomAgent.Domain/Entities/CourseMembership.cs` | FR-007, FR-009, FR-010, BR-051: `FirstSeen`, `SeenAgain` (never moves `FirstSeenAt`, refuses a backwards sighting), `NotOnRoster` (takes no instant) |
| `src/ClassroomAgent.Domain/Entities/CourseDetails.cs` | entity model §1.2 — skeleton remark removed, no code change |
| `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` | FR-001, FR-003, FR-004, FR-008 … FR-013, VR-006, I-5 … I-9, OD-009, OD-010: the pipeline step, the state parser, the teacher-wins tie-break, one transaction per course, the counter |
| `src/ClassroomAgent.Application/Models/SynchronizationRunOutcome.cs` | FR-003, FR-016 — supporting change (§7 D-1); the three US-013 factory signatures stay call-compatible |
| `src/ClassroomAgent.Application/Ports/IClassroomReader.cs`, `ICourseRepository.cs`, `IClassroomParticipantRepository.cs`, `ICourseMembershipRepository.cs` | FR-002, entity model §6, §7 — skeleton remarks removed, no signature change |
| `src/ClassroomAgent.Application/Models/CourseSnapshot.cs`, `CourseRoster.cs`, `RosterEntry.cs` | entity model §6 — skeleton remarks removed, no code change |
| `src/ClassroomAgent.Infrastructure/Google/GoogleClassroomReader.cs` | FR-002, FR-003, FR-004, VR-005, S-02, S-03, S-04: the adapter, both list reads paged to the end, the key from `ISecretStore` per call, the technical account impersonated, the library's retry off |
| `src/ClassroomAgent.Infrastructure/Persistence/ClassroomAgentDbContext.cs` | FR-017: three `DbSet`s and three configurations |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | generated by `dotnet ef migrations add` |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/CourseRepository.cs`, `ClassroomParticipantRepository.cs`, `CourseMembershipRepository.cs` | entity model §7, AD-7: staged changes, no `SaveChangesAsync` |
| `src/ClassroomAgent.Infrastructure/Persistence/TimestampInterceptor.cs` | PC-6; db-design §3.1, §4.1, §5.1 ("interceptor-stamped") — supporting change (§7 D-2) |
| `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` | FR-016, OD-010: one `Warning` line per skipped course; the finish line carries the course counter and the off-roster count |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | FR-020: the port and the three repositories registered; the reader takes no logger (§7 D-3) |

### Tests — created

| File | Trace |
|---|---|
| `tests/ClassroomAgent.Tests/Application/UseCases/CourseInvariantTests.cs` (11 facts) | VR-002, VR-003, entity model §4.2 — closes the **truncation gap** the test-generation report §9 left to IMPLEMENTATION |
| `tests/ClassroomAgent.Tests/Web/Logging/CourseImportLoggingTests.cs` (4 facts) | FR-010, FR-016, S-06, SC-10, OD-010 — closes the **host-level log-content gap** of the same §9; also the only end-to-end proof of the off-roster path against real PostgreSQL |
| `tests/ClassroomAgent.Tests/TestInfrastructure/FakeClassroomReader.cs` | TC-4 — supporting change (§7 D-4) |

### Tests — modified

| File | Trace |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationFactory.cs`, `InstallationTestHost.cs` | TC-4: the Classroom port substituted in every host test (§7 D-4) |
| `tests/ClassroomAgent.Tests/TestInfrastructure/SyncHostExtensions.cs` | the two optional seeding callbacks the new host tests need, applied before `Start` |
| `tests/ClassroomAgent.Tests/Application/UseCases/RosterImportTests.cs` | one expectation corrected on the Owner's recorded decision (§7 D-5, §8) |

Not touched, and asserted so: `PermittedServiceWrites`, `SyncState` and its migration,
`AuditAction` / `AuditTargetType`, `GoogleDelegationScopes`, `ClassroomAgent.Contracts`,
every US-008 … US-012 page and endpoint, and the whole Control Plane. No secret, no
generated database file and no IDE-local config is in the change set; no NuGet package
was added.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | **0 errors, 0 warnings** (`TreatWarningsAsErrors`) |
| Whole suite | `dotnet test ClassroomAgent.sln` | **2337 total, 2337 passed, 0 failed, 0 skipped** |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no changes |
| Migration | `dotnet ef migrations add AddCoursesAndRosters …` | applied by the tests' Testcontainers host; installation migrations **6 → 7**, tables **6 → 9** |

Incremental evidence, in the order it was gathered:

1. **Baseline before any production change** — the 36 new Application and adapter tests
   ran 34 failed / 2 passed, confirming the red phase was real.
2. **Schema, migration and adapter** — 44 passed (`CourseSchemaTests`,
   `ClassroomParticipantSchemaTests`, `CourseMembershipSchemaTests`,
   `AppUserMigrationTests`, `GoogleClassroomReaderTests`), all against real PostgreSQL
   through Testcontainers (TC-2); no test calls Google (TC-4).
3. **Whole suite after the pipeline step** — 2321 total, 2320 passed, **1 failed**: the
   single test of §7 D-5. No US-013 test and no test of any earlier Story failed.
4. **The two new classes** — 16 passed.
5. **Whole suite after the corrected expectation** — 2337 passed, 0 skipped.

## 6. Configuration Changes

**None.** No `appsettings` key, no environment variable, no DC-3 entry. The Classroom
page size is a constant in the adapter, not configuration: it describes how this
program talks to Google (VR-005, I-10). No translation key was added, because nothing
in this Story reaches a screen (FR-018, FR-020).

## 7. Deviations and Discovered Problems

**D-1 — `SynchronizationRunOutcome` carries what the host logs.** OD-010 requires one
`Warning` line per skipped course, and FR-016 requires the count of memberships marked
off a roster, but the step runs in `Application`, which has no logger and may not gain
one (`Microsoft.Extensions.Logging.Abstractions` would be a new package reference,
forbidden by FR-020 without an Open Decision). The outcome therefore gained
`SkippedCourses` and `MembershipsMarkedOffRoster`, and
`SynchronizationBackgroundService` writes the lines. Both values are an identifier, a
state string and a count — nothing SC-10 forbids. Entity model §8 does not list this
type, so it is recorded here rather than assumed; the US-013 factory signatures stay
call-compatible through optional parameters.

**D-2 — `TimestampInterceptor` gained the three entities.** db-design calls
`created_at` / `updated_at` "interceptor-stamped" (PC-6), and the interceptor names the
entities it stamps explicitly. **Observation for a later Story, not corrected here:**
`SyncState` and `WorkspaceConnection` are *not* on that list, so their stamps are the
CLR default (`0001-01-01`). That is a pre-existing defect of US-009 / US-013, outside
this Story's scope (Git Policy: no opportunistic refactoring), and it is recorded so it
is not lost.

**D-3 — `GoogleClassroomReader` takes no `ILogger`.** The OD-012 skeleton had an
optional one. Nothing in the adapter may be logged: no Google error text, no token, no
key detail (SC-10), and unlike US-011's probe it classifies nothing (OD-008). The
parameter was removed rather than left unread, and the DI registration follows.

**D-4 — the Classroom port is substituted in every host test.** Before this Story the
host registered the real adapter and nothing called it, so host tests were safe by
accident. With the step implemented, a host run would try to reach a live Google API —
which TC-4 forbids outright. `FakeClassroomReader` is now registered in
`InstallationFactory` for every host test, empty unless a test seeds it, which is also
what keeps the eight US-013 host expectations true.

**D-5 — one test expectation was wrong and could not be satisfied.**
`RosterImportTests.OnePersonOnTwoCourses_IsOneParticipantAndTwoMemberships` asserted
that `.Order()` over `ClassroomRole` yields `[Student, Teacher]`. Entity model §5 fixes
the enum's members as `Teacher, Student`, so `Teacher = 0` and ordering by value yields
`[Teacher, Student]`: no implementation faithful to the design can pass it. The slip
came from the schema tests, where `ORDER BY role` sorts the stored codes `'student'`
before `'teacher'` alphabetically. **The Owner decided on 2026-09-28 to correct the
expectation** rather than reorder the enum (which would contradict entity model §5) or
loop back to TEST_WRITING. The assertion keeps its full strength — one person, two
memberships, one of each role — and a comment records why the two orders differ.

**D-6 — a failed run still reports a zero counter in `SyncState`.** US-013's handler
passes `0` to `FailRun`, so a run that committed two courses and then failed stores
`processed_count = 0`. No US-014 requirement changes that line, and changing it would
alter US-013's recorded behaviour, so it was left alone. Courses already committed are
not lost — the next run finds them by their Google id (FR-008).

Nothing else diverged: every table, column, constraint, index and port signature is as
the approved designs fix them.

## 8. Open Decisions

No new Open Decision. OD-001 … OD-012 are all resolved, and no unresolved marker
remains in the changed code or in this Story's artifacts.

Two resolutions were load-bearing in code and are cited where they act: **OD-010** at
`RunSynchronizationUseCase.TryParseState` plus `LogCourseSkipped` (the course is
skipped before it can become an entity, so `ck_course_course_state` is never what stops
it) and **OD-011** at `ClassroomParticipantConfiguration` (the email index is not
unique; `ClassroomParticipantSchemaTests.TwoParticipantsSharingAnEmailAddress_AreBothStored`
fails the moment someone adds `IsUnique()`).

**OD-012 is fully discharged:** every `NotImplementedException`, every
`#pragma warning disable CS9113` and every "compile-only skeleton" remark of this Story
is gone. The two remaining such remarks in the repository belong to US-005 and US-013
artifacts and are not this Story's to touch.

The decision recorded in §7 D-5 was taken by the Owner in conversation, not written as
a new `OD-`: it corrected a test's expectation, changed no requirement and no design,
and both alternatives were stated before the choice.
