---
artifact_type: test_strategy
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T20:20:00Z
updated_at: 2026-10-26T20:20:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md
    version: null
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
  - path: docs/designs/api/US-058-api-design.md
    version: 1
  - path: docs/designs/database/US-058-db-design.md
    version: 1
  - path: docs/designs/database/US-058-entity-model.md
    version: 1
supersedes: null
---

# US-058 Test Strategy — Synchronization marks items no longer in Classroom and records the courses it skips

## 1. Scope

Everything the Story stores, nothing it displays (spec §10):

- the "not in Classroom" mark on `CourseWork` — set, kept, cleared (FR-001 … FR-003);
- the one run-log line counting newly marked / unmarked items (FR-004);
- skip records in `sync_skipped_course` — created, refreshed, removed (FR-005 … FR-007);
- the course's last successful read instant (FR-008, OD-001 a);
- the migration `AddSyncDataHealthRecords` (db-design §5).

No endpoint, page, role, policy, audit event or translation is touched (spec §7, §9; API_DESIGN NOT_APPLICABLE), so
there are no contract, allowed/forbidden-role, antiforgery, read-only or translation tests in this Story. Read-only
mode is unaffected: synchronization does not run in it and its guard is already tested (US-013).

## 2. Test levels

| Level | Unit under test | Why this level | Class |
|---|---|---|---|
| Unit (Domain) | `CourseWork`, `Course`, `SyncSkippedCourse` | the invariants the entity model puts in the entities: the first instant kept, `UpdateFrom` clears the mark, cutting and refusals (VR-001 … VR-003, VR-005) | `SyncDataHealthInvariantTests` |
| Unit (Application) | `RunSynchronizationUseCase` over `SyncWorld` (all ports in memory, TC-1, TC-4) | the decision rules: which outcome marks, unmarks, records, refreshes, removes; run order and stop behaviour | `CourseWorkPresenceTests`, `SkippedCourseRecordTests` |
| Integration (persistence) | migration, EF Core configuration, `ISyncSkippedCourseRepository` against PostgreSQL (TC-2) | schema exactly as db-design §2 … §4; checks and unique index reject what they exist to reject; round trip through the real repository and reason code converter | `SyncDataHealthPersistenceTests`, `AppUserMigrationTests` (adapted) |
| Integration (host) | the real host, background service and PostgreSQL, Classroom port substituted | what only a real database and a real log show: rollback inside the course's transaction (AC-006, SPEC_REVIEW m-4), the run-log line and its content (AC-012, SC-10), DI wiring of the new repository in a real run | `CourseWorkPresenceSyncTests` |

## 3. Scenarios

### 3.1 Positive

- An item absent from a full read is marked with the run's `observedAt`; its fields and submissions are untouched (AC-001); a material likewise (AC-002); the key is (resource, Google id) together.
- A marked item still absent keeps its first instant (AC-003, PC-10 idempotency).
- A marked item returned again loses the mark and takes Google's new data (AC-004).
- A skipped course — out of range, unknown state — is recorded with id, list name, reason, run instant (AC-007).
- A repeated skip keeps the instant, takes the new name and reason (AC-008, I-3).
- The record goes on a full read (AC-009), on aging out (AC-014, I-5), when the course left a list gone through to its end (AC-010).
- A full read stores the run instant as the last successful read (AC-013).
- One log line per run counts newly marked and unmarked items (AC-012).

### 3.2 Negative

- A course not read in full — unchanged archive, out of range, `404` — gains and loses no mark (AC-005).
- A run stopped by the course's read changes no mark of that course (AC-006, unit); a failure **inside** the course's transaction rolls the marks back (AC-006, host — SPEC_REVIEW m-4: starts from an existing mark, and the item that would be unmarked and the item that would be marked both stay as they were).
- `404`, blank name and aged-out new course create no record (AC-011, I-1, I-2).
- A recorded course gone mid-run (`404`) or blank-named keeps its record and instant (FR-007.4).
- A stopped run — permission failure or a list failing part-way — removes no unlisted record but keeps the marks it committed (AC-015).
- An item already marked and still absent is not counted in the log line (I-4).
- Recording a skip of a stored course writes nothing else of it (FR-005.4).
- A skipped course keeps its last-read instant; so does an unchanged archive (AC-013, FR-008.1).

### 3.3 Boundary and validation

- VR-001: Google id trimmed, cut to `Course.MaxGoogleIdLength`; blank refused (`ArgumentException`); database check `ck_sync_skipped_course_google_id`.
- VR-002: name trimmed, cut to `Course.MaxNameLength`, blank → null (in the entity, through a run, and `ck_sync_skipped_course_name` rejects `''`).
- VR-003: undefined reason refused (`ArgumentOutOfRangeException`) in `Record` and `SkipAgain`; database check rejects a non-code value.
- VR-004: every instant is the run's `observedAt` (asserted equal to the run start in `SyncWorld`, and equal between the record and the last-read instant in the host run).
- VR-005: the mark is absent or an instant — a mark, a repeat, a clear and a fresh mark.

### 3.4 Security

- SC-10, FR-004.3: the run-log line holds two numbers and no course id; no line of the three runs holds an item title or id, the student's name, email or id, or the grade (`7.25` chosen to be unmistakable).
- No new Google call: the unit tests use the same reads the run already makes; the control assertion in AC-005 shows an unchanged archive is not read.
- No endpoint, role, audit or read-only change — nothing to test (§1).

### 3.5 Persistence

- Columns `course_work.not_in_classroom_since`, `course.last_complete_read_at`: nullable `timestamptz`, no default.
- Table `sync_skipped_course`: exactly seven columns with types, lengths and nullability; `pk_…`, `uq_…_course_google_id`, three checks, no foreign key.
- Migration list: 22 migrations, `_AddSyncDataHealthRecords` last; table set gains `sync_skipped_course` (`AppUserMigrationTests` adapted); no pending model changes (existing test).
- Repository: records round-trip with the lower-case reason code and interceptor timestamps; read tracked, so `SkipAgain` and `Remove` are saved by the unit of work.

## 4. Fixtures

- `SyncWorld` gains `SkippedCourses` — an in-memory `ISyncSkippedCourseRepository` unique on the Google course id (a second `Add` for one course throws, as the index would) — passed to the use case by the named argument `skippedCourses:`.
- `FakeClassroomReader.WithoutCourseWork(course, item)` — Classroom stops returning an item between host runs.
- Host runs use the 1-minute interval and the existing `WaitForSecondRunAsync`.
- AC-006 host: a CHECK constraint added in the test's own database rejects one new submission (the US-053 `FailedRunRecordedTests` precedent, no seam in `src`).
- All data synthetic (`CourseTestData`, `CourseWorkTestData`); no live Google, no Control Plane (TC-4).

## 5. Design decisions taken here (test design only)

- **The log line is found without pinning its id or name.** FR-004.4 leaves both to IMPLEMENTATION. The host test takes
  the run's events in the synchronization range 5100–5199 whose id is not one of the events that existed before
  (5121 … 5128, 5130 … 5135) and asserts exactly one, whose numeric properties are exactly the two counts. This also
  pins that the line carries no other number.
- **The use case receives the new port as a constructor parameter named `skippedCourses`.** The entity model gives
  the port, not the constructor; the skeleton adds it optional (last, `= null`) so the host still starts with nothing
  registered (standing rule). IMPLEMENTATION registers the repository and may make the parameter required; the tests
  pass it by name.
- **`RecordCompleteRead()` callers adapted.** `IncrementalSyncRuleTests` (4 calls) and
  `IncrementalSyncPersistenceTests` (1 call) now call `RecordCompleteRead(T)`, as DB_DESIGN noted. Until
  IMPLEMENTATION they fail with `NotImplementedException` — expected red, not a regression.

## 6. Excluded scenarios

- Display of marks, banner, «Стан даних» — Stories 2 and 3 (spec §10).
- Telling a deleted item from one returned to draft — impossible from Google's answer (v99).
- A course deleted as a whole in Google — v99 says nothing.
- Unchanged archived course holding a record (I-7) — the spec itself calls it near-impossible (archiving moves `updateTime`); the rule "no other outcome removes a record" is covered by the `404` and blank-name cases.
- "No log line when both counts are zero" (I-4, second half) — proving an absence of a line whose name is not fixed would need waiting for nothing; the one-line-per-run count is asserted where counts are non-zero.
- Failure writing a skip record itself (spec §8) — an ordinary database failure of the run, already covered by US-053.

## 7. Known limitations

- `SyncWorld`'s unit of work does not roll back; rollback is proven only in the host test (AC-006).
- Removal of a record inside the course's transaction (FR-007.1, db-design §4.1) is observed as "gone after the run" in both levels; that a failed transaction keeps the record is not separately tested (a failure stops the run, BR-043, and the record staying follows from the same rollback AC-006 proves).
- The exact moment of the log line within a run is not fixed; the host test waits for the line itself.

## 8. Open Decisions affecting testing

None. OD-001 is resolved (a); FR-008/AC-013 are tested.
