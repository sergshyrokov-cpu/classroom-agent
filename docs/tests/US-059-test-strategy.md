---
artifact_type: test_strategy
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T12:30:00Z
updated_at: 2026-10-27T12:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-059-sync-marks-courses-gone-and-skips-negative-values.md
    version: null
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
  - path: docs/designs/api/US-059-api-design.md
    version: 1
  - path: docs/designs/database/US-059-db-design.md
    version: 1
  - path: docs/designs/database/US-059-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 101
supersedes: null
---

# US-059 Test Strategy

## 1. Scope

Synchronization behaviour at the end of a course list read to its end (spec FR-001 … FR-007), the account-change
instant recorded by the connection save (FR-002), and the reader's refusal of a negative value (FR-008). No endpoint,
page, role or translation changes (API_DESIGN NOT_APPLICABLE; spec §7, §9), so no contract, authorization, antiforgery
or translation tests are added — TC-5's per-endpoint tests have nothing new to cover.

## 2. Test levels

| Level | What it proves | Class |
|---|---|---|
| Unit (domain) | `Course.MarkAbsent` / `RecordListed`, `CourseAbsenceRule.MarkFor`, `SyncState.RecordCourseListRead`, `WorkspaceConnection.ChangeTo(…, changedAt)` | `CourseListPresenceInvariantTests` |
| Unit (use case, TC-1) | the run's end-of-list behaviour over in-memory ports (`SyncWorld`), run by run | `CourseListPresenceTests` |
| Unit (use case, TC-1) | the connection save records the change instant only for another account | `SaveWorkspaceConnectionAccountChangeTests` |
| Adapter (offline, TC-4) | the Google reader's conversion refuses negatives as `ValueOutOfRange` | `GoogleClassroomReaderValueRangeTests` (extended) |
| Integration (PostgreSQL, TC-2) | columns, check, defaults, EF mapping of the code, `GetAllAsync` tracked, round trips | `CourseListMarksPersistenceTests` |
| Integration (host) | real runs: stored codes and instants, change instant read from the DB, empty-list flag, the FR-007 log lines | `CourseListPresenceSyncTests` |
| Integration (host + real adapter over a scripted transport) | AC-009 end to end: negatives skipped and recorded, run completes, no values logged | `NegativeValueSyncTests` |

## 3. Scenarios

**Positive.** Absent course marked "not in Classroom" with the run instant (AC-001), archived course too (FR-001.1);
listed course sighted and unmarked (AC-003, AC-014); "not visible" after an account change (AC-012, AC-017); flag set on
an empty list (AC-006) and cleared by a non-empty one (AC-007); a negative value skips the course and records it (AC-009);
a change of account records the save's instant (AC-011).

**Negative.** A run that stops — list failing part-way, a course read failing — marks, unmarks and sights nothing and
leaves the flag (AC-005); an empty list marks nothing and removes no skip record (AC-006); same account in other case or
with spaces records no change, a first save records none (AC-011); an undefined mark is refused (VR-003); a change to an
address outside the domain is still refused and records nothing.

**Boundary.** Sighting one tick before / exactly at / after the change instant (FR-006.1 strict); no sighting with and
without a change (I-1, AC-017); a list with only id-less entries is empty (I-4); no course stored and an empty list
(AC-008); zero is a value, the smallest negative steps (−1·10⁻⁶, −0.25, −1) and the old lower bound −7.9·10²⁸ are refused
(VR-005, FR-008.3).

**Behaviour kept (I-7, FR-006.5).** A marked course listed but skipped (out of range) loses its mark; a marked course
keeps the kind it has after the account changes (AC-015); `UpdateFrom` leaves mark and sighting.

**Instants (db-design §6).** R1: the clock moves while the connection is read; the run's observation instants are the
ones from before. R2: an account change saved while the list is being read (the connection replaced by a new row
object) is seen at the end of the list and gives "not visible".

**Security / SC-10.** The FR-007 lines carry counts only; no course name or id, item, person, e-mail or grade appears in
any line of the runs (AC-010). The negative-value failure carries no value and no identifier, and the run log has the
course id without the value (AC-009). FR-002.4: the save writes exactly one `WorkspaceConnectionSaved` row, no new event.

**Persistence.** db-design §2 … §4 column shapes and defaults; `ck_course_absence_mark` accepts the three valid shapes
and rejects kind-without-instant, instant-without-kind and unknown codes; both kinds stored as their codes; the change
instant and the flag stored and read back; the migration `AddCourseListMarks` is the 23rd.

## 4. Fixtures

- `SyncWorld` (extended): `CourseRepository.GetAllAsync`; `ConnectionRepository.Stored`, `OnRead` (moves the clock for
  R1), `Replace` (a save made elsewhere, R2); `ClassroomReader.AfterCourses` (something happens at the end of the
  list); the run built with `workspaceConnections: Connections`.
- `FakeClassroomReader.WithoutCourse` — a course leaves the host's list between runs.
- `GoogleClassroomScript` (unchanged) for the real adapter. All identities synthetic (`CourseTestData`,
  `CourseWorkTestData`, `WorkspaceConnectionTestData`).

## 5. Design decisions taken here (test design only)

- **FR-007 line found by exclusion.** Event ids and names are IMPLEMENTATION's (FR-007.4), so a line is "a
  synchronization event (5100–5199) of the run outside 5121 … 5136". Its three counts are asserted as a sorted multiset
  (`[0,1,1]` for 1 / 0 / 1); which property is which is not observable without the names. The marks themselves are
  proven per kind at the unit level.
- **Account change in host tests by SQL.** `technical_account_changed_at` is set directly between runs; the save's own
  writing of it is proven through the use case and through EF Core.
- **AC-008 made non-vacuous** by setting the flag first and removing the stored course (as the purge would) before the
  empty list.
- **Course fingerprint without the course row's `updated_at`** (US-057 `ValueOutOfRangeSyncTests` adapted, and the new
  `NegativeValueSyncTests`): FR-003.1 records a skipped listed course's sighting on its row, which moves `updated_at`
  but no mirrored value.

## 6. Existing tests adapted (requirement changes, not weakening)

| Test | Change | Why |
|---|---|---|
| `GoogleClassroomReaderValueRangeTests.AValueJustInsideTheRange_IsConvertedAsToday` | data: −7.9·10²⁸ moved to the new `Negative` set | v100 BR-043 / FR-008: every negative is refused |
| `ValueOutOfRangeSyncTests` fingerprint | course row's `updated_at` left out | FR-003.1 sighting on a skipped listed course |
| `AppUserMigrationTests.TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` | 22 → 23 migrations, `_AddCourseListMarks` last | db-design §7 |
| `SyncStateSchemaTests.TheMigration_CreatesTheTable` | 14 → 15 columns, `last_course_list_empty` | db-design §4 |
| `CourseSchemaTests.TheOnlyCheckConstraint_IsTheStateVocabulary` → `TheCheckConstraints_AreTheStateVocabularyAndTheAbsenceMark` | expects `ck_course_absence_mark` too | db-design §2 |

`WorkspaceConnection.ChangeTo(domain, email)` has no test caller (DB_DESIGN N-1), and every test clock is manual — no
test pins the order of clock reads (N-2).

## 7. Excluded scenarios

- Display of marks and the flag, the Admin's confirmation (Stories 2, 3; spec §10).
- A course vanishing mid-run is covered only as "listed, so not marked" (AC-018); marking happens in a later run, which
  AC-001 covers.
- No endpoint ⇒ no TC-3 / TC-5 endpoint tests; no translation keys (NFR-073 unaffected).
- The single `SaveChangesAsync` of the end-of-list writes (db-design §5) is not externally observable apart from
  "nothing changes when the run stops", which AC-005 covers.

## 8. Known limitations

- The FR-007 counts are matched as a multiset (§5).
- R2 is proven only in the Application layer (the in-memory connection replaced mid-list); the host test proves the
  change instant is read from the database.

## 9. Open Decisions affecting testing

None.
