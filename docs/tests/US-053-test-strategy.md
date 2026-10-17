---
artifact_type: test_strategy
story: US-053
version: 1
status: DRAFT
created_at: 2026-10-16T16:40:00Z
updated_at: 2026-10-16T16:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-053-failed-sync-run-recorded.md
    version: null
  - path: docs/specifications/US-053-spec.md
    version: 1
  - path: docs/decisions/US-053-open-decisions.md
    version: 1
  - path: docs/designs/api/US-053-api-design.md
    version: 1
  - path: docs/designs/database/US-053-db-design.md
    version: 1
  - path: docs/reviews/specifications/US-053-spec-review.md
    version: 1
supersedes: null
---

# US-053 Test Strategy — A failed synchronization run is always recorded as failed

## 1. Scope

The failure path of `RunSynchronizationUseCase.ExecuteAsync` (spec FR-002 … FR-007): before
`SyncState.FailRun` is saved, every change the run staged and did not commit is discarded, so the
failure save writes the `sync_state` row only. No endpoint, schema, translation or audit change
(spec §1, §7, §9; API and DB design NOT_APPLICABLE).

## 2. Test level

**Integration, through the real host** (`SyncHostExtensions.StartAsync`): the composition root, the
synchronization background service, the real `UnitOfWork` and `ClassroomAgentDbContext`, and real
PostgreSQL via Testcontainers (TC-2). The defect lives in EF Core's change tracker after a rolled-back
transaction, which the Application-level `SyncWorld` doubles cannot reproduce — so no unit test is
added. The Google ports are the fakes `FakeClassroomReader` / `FakeMeetReportsReader` with synthetic
data (TC-4). The DI scope per run and the host's log lines are exercised as in production.

New class: `tests/ClassroomAgent.Tests/Web/BackgroundServices/FailedRunRecordedTests.cs` (8 tests).

## 3. How a failure is forced (spec I-5, §7 — no seam in `src/`)

| Failure | Mechanism (test database / test data only) |
|---|---|
| Database rejects a course write (AC-001, AC-004, AC-005) | `ALTER TABLE course ADD CONSTRAINT us053_test_reject_course CHECK (google_id <> <course 2>)` — a constraint the second course's data violates. AC-005 drops it ("the cause is gone"). |
| Database rejects a Meet page write (AC-002) | `ALTER TABLE meet_session ADD CONSTRAINT us053_test_reject_conference CHECK (conference_id <> <conference 2>)`; page 1 holds conference 1, page 2 conference 2. |
| Database rejects the linking write (AC-003) | The existing US-032 helper `FailLinkInsertsAsync` (a BEFORE INSERT trigger on `meeting_code_link`). |
| Non-database exception inside the course's transaction, before any save (AC-007a) | A roster entry with a blank Google user id: `ClassroomParticipant.Import` throws `ArgumentException` after the course and one valid person were staged. |
| Non-database exception inside the course's transaction, after its first save (AC-007b) | A second coursework item with a blank title: `CourseWork.Import` throws `ArgumentException` while the course's first item is staged and the course rows were saved inside the (then rolled-back) transaction. |

SPEC_REVIEW m-1 is therefore met: AC-007 is reachable without a seam, because the domain's own guards
run inside the course's transaction on data a substituted Google port returns. The real Google adapter
may never return such data; the test needs only an exception that is not a database rejection, at that
point (spec AC-007).

## 4. Scenarios

### Positive (what the failed run records)
- Classroom step rejected → `failed`, `Unexpected`, `classroom`, `processed_count` 1, `finished_at` set,
  `last_successful_run_at` and `meet_loaded_up_to` null (AC-001, FR-004).
- Meet step rejected → `failed`, `Unexpected`, `meet`, `processed_count` 1, watermark null (AC-002).
- Linking step rejected → `failed`, `Unexpected`, `linking` (AC-003).
- The "Last synchronization" query reads `Failed` / `Unexpected` / `Classroom` (AC-004, I-6).
- The next run completes and clears diagnosis and step (AC-005, FR-007).

### Negative (what must not be stored)
- Nothing of the rejected course: no `course` row, no participant, no `course_work` row (AC-001).
- Nothing of the rejected Meet page: no session, no participation (AC-002).
- No link, no auto-link audit row (AC-003).
- Nothing of a course whose transaction failed on a non-database exception — before (AC-007a) or after
  (AC-007b) its first in-transaction save.
- No `SyncRunException` line; one `SyncRunFailed` line without the course id or the constraint name (FR-005, SC-10).

### Boundary
- The first course committed before the failure stays and is counted (AC-001, AC-002 `processed_count` 1).
- AC-007 covers both sides of the course's first `SaveChangesAsync` inside its transaction.

### Validation
None new (spec §6). VR-001 is asserted through the stored codes; VR-002/VR-003 by existing `SyncState` tests.

### Security
- No exception message, Google text or rejected value in the stored row or the log line (SC-10): the
  stored value is `Unexpected`; the `SyncRunFailed` line is checked for absence of the course id and the
  constraint name.
- No endpoint, policy, read-only or audit change → no new allowed/forbidden-role test (TC-5 N/A).

### Persistence
All assertions read the database directly (SQL counts and `sync_state` columns) after the run.

## 5. Fixtures

`PostgreSqlFixture` (one database per test), `SyncHostExtensions`, `MeetLinkingTestData`
(`SeedClearRostersAsync`, `FailLinkInsertsAsync`, `FirstMeetingEvents`, `WaitForSecondRunAsync`),
`MeetTestData`, `CourseTestData`, `CourseWorkTestData`. A private `WaitForRecordedRunAsync` waits for a
terminal row and fails at once — rather than after the 30 s limit — when the host writes
`SyncRunException`, the visible symptom of the defect.

## 6. Excluded scenarios

- A failure save that fails for its own reason (database gone) — out of scope (spec §8, §10).
- Host shutdown (FR-009) and the US-032 / US-048 detaches (FR-008) — covered by existing tests
  (`SyncScheduleTests.HostStop_IsNotRecordedAsAFailedRun`, `MeetLinkingHostTests`,
  `AppUserConflictTranslationTests`); AC-006 is the full suite staying green.
- A page-level test of the "Last synchronization" block — I-6: the rendering is already tested by
  US-017/US-031; the Application query from a real failed run is the proof.
- A test of the new `IUnitOfWork` member itself — its design is the implementation's (spec I-2, DB
  design); the behaviour is proved through the run.

## 7. Known limitations

- AC-003 may pass before the change (US-032 already detaches a failed link) — kept as a regression test
  (spec AC-003, Story Notes).
- The forced rejections are a CHECK constraint violation (SQLSTATE 23514) and a trigger exception
  (P0001); a unique violation is not used, so the existing translations of `UnitOfWork` are not involved.

## 8. Open Decisions affecting testing

None. OD-001 is resolved (a).
