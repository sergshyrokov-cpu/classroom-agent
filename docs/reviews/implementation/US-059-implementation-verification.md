---
artifact_type: implementation_verification
story: US-059
version: 1
status: APPROVED
created_at: 2026-10-27T14:30:00Z
updated_at: 2026-10-27T14:30:00Z
produced_by: implementation-verifier
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
  - path: docs/tests/US-059-test-strategy.md
    version: 1
  - path: docs/tests/US-059-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-059-test-generation-report.md
    version: 1
  - path: docs/evidence/US-059-implementation-report.md
    version: 1
supersedes: null
---

# US-059 Implementation Verification

## 1. Summary

**Verdict: PASS** — 0 Critical, 0 Major, 1 Minor, 1 INFO. The code does what Specification v1, db-design v1 and the
entity model say; every AC has a test that would fail without the behaviour; the three deviations in implementation
report §7 are disclosed and sound. The open finding US-059/IMPLEMENTATION/m-1 (db-design §2 constraint text) stays
OPEN: correcting a design artifact is DB_DESIGN's work, not this stage's (this review confirms the defect, §6 D-1).

Delegation: one `cheap-worker` (Sonnet) run of `dotnet build` and the Story's test classes (§8), told to touch no files
and report verbatim. Kept here: reading the diff, the tests against the ACs, the deviations and the classification.

## 2. Reviewed Artifacts

As in the front matter; all inputs current (implementation report names Spec v1, db-design v1, entity model v1, test
strategy v1, matrix v1). API design NOT_APPLICABLE, no `openapi`. Working tree: 17 changed/new production files,
16 changed/new test files, matching implementation report §4 and test generation report.

## 3. AC Verification

| AC | Tests read | Proves the AC? |
|---|---|---|
| AC-001 | CLP `ACourseGoogleNoLongerLists_…`, `AnArchivedCourseNoLongerListed_…`; HOST `ARealRun_StoresTheMarkCode_…` | yes — mark kind + run instant asserted, data (state, item, item mark, grade) asserted unchanged; stored code checked in PostgreSQL |
| AC-002 | CLP `AMarkedCourseStillAbsent_KeepsTheFirstInstant`; INV `MarkingAgain_…` | yes — instant of run 2 after run 3 |
| AC-003 | CLP `AMarkedCourseThatComesBack_…`; INV `RecordingListed_…` | yes — precondition mark asserted, then no mark, new name, sighting |
| AC-004 | CLP `ListedCoursesTheRunDoesNotRead_…`, `AMarkedCourseOnTheListButNotRead_…` | yes — the three skips are proved to have happened (outcome counters), then no mark + sighting |
| AC-005 | CLP `ARunWhoseListFailsPartWay_…`, `ARunStoppedByACourseRead_…` | yes — not vacuous: course 1 is listed and course 3 absent, so a reached end-of-list would have changed both |
| AC-006 | CLP `AnEmptyListWhileCoursesAreStored_…`, `AListWithOnlyIdlessEntries_IsEmpty`; HOST `AnEmptyList_…` | yes — mark, sighting, skip record instant and flag; host proves the flag is saved |
| AC-007 | CLP `AFlaggedEmptyList_…`; INV flag tests | yes — flag asserted set first |
| AC-008 | CLP `AnEmptyListWithNoCourseStored_…` | yes — starts from a set flag, so "cleared" is observable |
| AC-009 | RDR `ANegativeValue_…` (×12), `TheNegativeValueFailure_…`, `Zero_…`; NEG both | yes for the skip, skip record, run completion and log; "already loaded data unchanged" only for the negative maximum (Minor m-2) |
| AC-010 | HOST `EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing` | yes — counts 1/0/0 and 1/0/1, single new line, no names/ids/persons/grade in any log file |
| AC-011 | SWC (3); INV connection tests; PER `TheChangeInstant_IsStored` | yes — case-only change records nothing, other account records the save's `now`, one audit row per save |
| AC-012 | CLP `ACourseLostAfterTheAccountChanged_…`; HOST `ARealRun_AfterTheAccountChanged_…` | yes — host reads the change instant from the database |
| AC-013 | CLP `ACourseTheNewAccountSaw_…`; INV rule | yes |
| AC-014 | CLP `ANotVisibleCourse_…` | yes |
| AC-015 | CLP `ACourseMarkedNotInClassroom_…` | yes |
| AC-016 | CLP `AnUnchangedArchiveOnTheList_…` | yes — unread proved by `RostersRead` empty and unchanged last complete read |
| AC-017 | CLP `ACourseNeverSighted_…` (×2) | yes |
| AC-018 | CLP `ACourseListedButGoneDuringItsReads_…` | yes — `CoursesGone` asserted |
| R1, R2 | CLP `TheRunInstant_IsTakenBeforeTheConnectionIsRead`, `AnAccountChangeSavedDuringTheRun_…` | yes — clock moved inside the connection read; change replaced mid-list |

## 4. Specification Coverage

- FR-001 … FR-006: `RunSynchronizationUseCase.CloseCourseListAsync`, called only after the `await foreach` ends
  (FR-001.4, FR-005.4). Empty list (`listed.Count == 0 && stored.Count > 0`) sets only the flag and returns before the
  skip-record removal (FR-005.2). `MarkAbsent` only when `AbsenceMark is null` (FR-006.5). `CourseAbsenceRule.MarkFor`
  is strict `<`, never-sighted → not visible only with a change (FR-006.1…3). Matching is ordinal on `listed` built by
  `RecordKeyOf` (VR-001).
- FR-002: `WorkspaceConnection.ChangeTo(domain, email, changedAt)` compares the normalised email ordinally with the
  stored (normalised) one; `Create` unchanged; `SaveWorkspaceConnectionUseCase` passes its `now` inside the existing
  transaction; no new audit event.
- FR-007: `CourseListMarkCounts` → `SynchronizationRunOutcome.CourseList` → events 5137 `SyncCourseListPresenceChanged`
  (counts, only when `AnyChange`) and 5138 `SyncCourseListEmpty` (stored count). Message templates carry counts and
  the run id only (SC-10).
- FR-008: `GoogleClassroomReader.Points` throws `ValueOutOfRange` with no value for `points < 0`; zero passes; the use
  case's skip path is unchanged.
- No behaviour beyond the artifacts found. The course list and card show Google's `update_time`
  ("Оновлено в Google"), not the row's `updated_at`, so writing the sighting every run changes nothing a user sees.

## 5. Design Conformance

- db-design §2–§4, §7: migration `AddCourseListMarks` adds exactly the five columns with the designed types,
  nullability and default, and `ck_course_absence_mark` (stricter text, §6 D-1); `Down` drops all. Snapshot updated.
  Converter codes `not_in_classroom` / `not_visible_to_technical_account`, `HasMaxLength(40)`.
- db-design §5: one `SaveChangesAsync` at the end of the list for marks, sightings, flag and skip-record removal
  (conditional, §6 D-2). §6 R1: `observedAt` taken before `connectionQuery.ExecuteAsync`; R2: `GetForReadAsync` at the
  end of the list.
- Entity model §1–§6: signatures as designed; `ICourseRepository.GetAllAsync` tracked; `RunSynchronizationUseCase`
  gains `IWorkspaceConnectionRepository` (already registered in `InstallationServices`).
- AD-3/AD-4: no Google type or `DbContext` crosses into Application/Domain; AD-6: synchronization's read-only refusal
  unchanged; AD-8: no DTO/controller touched.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | db-design §2 `ck_course_absence_mark` text | The design text evaluates to NULL for (no kind, an instant) — `NULL IN (…)` is NULL, `FALSE OR NULL` is NULL, and PostgreSQL accepts a NULL CHECK — so it would accept what VR-003 forbids. Added `absence_mark IS NOT NULL`. Verified by hand; caught by `AnInvalidMark_IsRejectedByTheCheck`. | **Sound.** The design text should still be corrected (finding US-059/IMPLEMENTATION/m-1, stays OPEN). |
| D-2 | db-design §5 "one save" every list | Save skipped only when nothing can have changed (no course stored, flag already clear, no skip record removed); keeps US-013's two-commit run on an empty installation. | **Sound** — the skipped save would write nothing. |
| D-3 | TEST_WRITING tests | `CourseSchemaTests` column count 19 → 22 (+ three column asserts); `TheChangeInstant_IsStored` reads `timestamptz` via `GetFieldValue<DateTimeOffset>`. Assertions unchanged in meaning, not weakened. | **Sound.** |

## 7. Report Accuracy

Change set in implementation report §4 plus the test generation report equals `git status` (tracked and untracked,
checked with `git status --untracked-files=all`). Every AC-to-test location in report §3 and the matrix exists. No
`TODO`/`TBD`/`FIXME`, skipped test or `NotImplementedException` in any changed `.cs` file. No secret, database file or
IDE config in the change set.

## 8. Story Test Run

Delegated to `cheap-worker`, 2026-10-27, from the repository root:

- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~<Class>"`, one class per call — all exit 0,
  0 failed, 0 skipped: CourseListPresenceTests 22, CourseListPresenceInvariantTests 25,
  SaveWorkspaceConnectionAccountChangeTests 3, GoogleClassroomReaderValueRangeTests 36,
  CourseListMarksPersistenceTests 14, CourseListPresenceSyncTests 4, NegativeValueSyncTests 2, AppUserMigrationTests 6,
  SyncStateSchemaTests 11, CourseSchemaTests 12, SynchronizationRunTests 7, CourseImportTests 12,
  CourseWorkFailureTests 3, CourseWorkImportTests 10, SubmissionImportTests 11, SubmitterWithoutRosterTests 4,
  ValueOutOfRangeSyncTests 6, SaveWorkspaceConnection* 23.

The full suite is not rerun here (SECURITY_REVIEW runs it).

## 9. Findings

- **m-2 (Minor, test_gap, advisory).** AC-009 says each of the three courses keeps "its already loaded data unchanged";
  `NegativeValueSyncTests.CoursesWithNegativeValues_…` proves it only for course 3 (negative maximum). Courses 1 and 2
  (negative assigned / draft grade) are new in that run, so the test shows they are not created, not that a stored
  course keeps its grades. The risk is low — all three values go through the same `Points` conversion before the
  course's transaction, and RDR covers each field — so this does not block.
- **INFO-1.** Every run now writes `last_listed_at` (and so the interceptor's `updated_at`) on every stored listed
  course — a few hundred rows per school, as spec §9 foresaw. No screen shows the course row's `updated_at`.
- **US-059/IMPLEMENTATION/m-1 (carried, OPEN).** Confirmed: the db-design §2 text accepts an instant without a kind;
  the code is right, the design text should be corrected (DB_DESIGN's artifact) or the finding accepted by the Owner.

## 10. Verdict

PASS — no Critical or Major finding; next stage SECURITY_REVIEW.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_VERIFICATION
  story: US-059
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/implementation/US-059-implementation-verification.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-2: AC-009 'already loaded data unchanged' is proved only for the negative maximum; the negative assigned/draft grade courses are new in NegativeValueSyncTests, so a stored course keeping its grades is not shown (low risk: same Points conversion)"
    - "INFO-1: every run writes last_listed_at (and updated_at) on every stored listed course; no screen shows the row's updated_at"
  closed_findings: []
```
