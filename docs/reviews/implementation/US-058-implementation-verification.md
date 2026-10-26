---
artifact_type: implementation_verification
story: US-058
version: 1
status: APPROVED
created_at: 2026-10-26T20:51:48Z
updated_at: 2026-10-26T20:51:48Z
produced_by: implementation-verifier
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
  - path: docs/tests/US-058-test-strategy.md
    version: 1
  - path: docs/tests/US-058-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-058-test-generation-report.md
    version: 1
  - path: docs/evidence/US-058-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-058 Implementation Verification

## 1. Summary

**PASS.** The code does what Specification v1 and the database design v1 say: in a full read of a course,
stored items Google no longer returns are marked with the run's instant, they keep the first instant, and the
mark goes when an item comes back. A course skipped for out-of-range values or an unknown state is recorded,
keeps its first instant, and its record is removed on a full read, on aging out, or once a list read to its
end no longer holds it. The run's instant is stored as the course's last successful read, and one log line
per run carries the two counts. Every AC maps to tests that would fail without the behaviour. The five
deviations are disclosed and sound. 0 Critical, 0 Major, 3 Minor.

Delegated: one run of the Story's 16 test classes (build plus one `--filter` per class) went to `cheap-worker`
(Sonnet), whose report is quoted in §8. Inline: reading the diff, the tests and the artifacts, and every
judgement.

## 2. Reviewed Artifacts

| Artifact | Path | Version / status |
|---|---|---|
| Story | `docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md` | — |
| Specification | `docs/specifications/US-058-spec.md` | v1 APPROVED |
| Open decisions | `docs/decisions/US-058-open-decisions.md` | v1 APPROVED (OD-001 resolved a) |
| API design | `docs/designs/api/US-058-api-design.md` | v1, NOT_APPLICABLE (no endpoint, no `openapi.yaml`) |
| Database design | `docs/designs/database/US-058-db-design.md` | v1 |
| Entity model | `docs/designs/database/US-058-entity-model.md` | v1 |
| Test strategy | `docs/tests/US-058-test-strategy.md` | v1 |
| AC test matrix | `docs/tests/US-058-ac-test-matrix.md` | v1 |
| Test generation report | `docs/evidence/US-058-test-generation-report.md` | v1 |
| Implementation report | `docs/evidence/US-058-implementation-report.md` | v1; inputs name the current versions, so nothing is stale |

## 3. AC Verification

| AC | Tests (class) | Verdict |
|---|---|---|
| AC-001 | `AnItemGoogleNoLongerReturns_IsKeptWithItsGrade_AndMarkedWithTheRunInstant`, `TheMark_FollowsTheResourceAndTheGoogleId_Together` (CWP); `Marking_SetsTheInstant_…` (INV) | Proven: asserts the exact run instant, the unchanged title, maximum, submission state and grade, and that the returned item has no mark. |
| AC-002 | `AMaterialGoogleNoLongerReturns_IsKept_AndMarked` (CWP) | Proven. |
| AC-003 | `AMarkedItemStillAbsent_KeepsTheFirstInstant` (CWP), `MarkingAgain_…` (INV) | Proven: the clock moves between runs, so a re-mark would show. |
| AC-004 | `AMarkedItemThatComesBack_LosesTheMark_AndIsUpdated` (CWP), `UpdatingFromGoogle_ClearsTheMark_…` (INV) | Proven: the test first asserts that the mark exists, so the cleared mark is not vacuous. |
| AC-005 | `ACourseNotReadInFull_GainsAndLosesNoMark`, `ACourseReportedGone_GainsAndLosesNoMark` (CWP) | Proven: starts from existing marks; the control asserts that the archive was not read. |
| AC-006 | `ARunStoppedByTheCourseRead_…` (CWP); `AFailureInsideTheCourseTransaction_RollsItsMarksBack` (HOST, PostgreSQL) | Proven: the host test injects a check constraint that rejects a submission inserted after the items are written, so the failure is inside the transaction (SPEC_REVIEW m-4 satisfied). |
| AC-007 | `ACourseSkippedForEitherReason_IsRecorded`, `AnUnknownStateCourse_WithABlankName_…`, `RecordingAStoredCourse_LeavesTheCourseAsItWas` (SCR); INV entity tests; PER table tests; `ARealRun_RecordsTheSkippedCourse_…` (HOST) | Proven, including end to end against PostgreSQL. |
| AC-008 | `ARepeatedSkip_…`, `TheSameSkipAgain_…` (SCR); `SkippingAgain_…` (INV); `ARecordReadFromTheRepository_CanBeChangedAndRemoved` (PER) | Proven; see m-3 on the persistence level. |
| AC-009 | `ARecordedCourseReadInFull_LosesItsRecord` (SCR) | Proven at the use-case level; see m-3. |
| AC-010 | `ARecordedCourseNoLongerListed_LosesItsRecord_WhenTheListIsGoneThrough` (SCR) | Proven: the listed record is kept and the unlisted one removed. |
| AC-011 | `OtherSkips_CreateNoRecord`, `ARecordedCourse_GoneMidRunOrBlankNamed_KeepsItsRecord` (SCR) | Proven. The first test is green by design (test generation report §5); the second covers FR-007.4 non-vacuously. |
| AC-012 | `EachRun_LogsOneLineCountingMarkedAndUnmarkedItems_AndNamesNothing` (HOST) | Proven: exact counts [0,2] and [1,1], a single new line, and the forbidden values (titles, ids, person, grade 7.25) are scanned across every log file. |
| AC-013 | `AFullRead_StoresTheRunInstant_AndASkippedCourseKeepsItsOwn`, `AnUnchangedArchive_KeepsItsLastReadInstant` (CWP); INV; PER; HOST | Proven. |
| AC-014 | `ARecordedCourseAgedOut_IsNotLoaded_AndLosesItsRecord` (SCR) | Proven. |
| AC-015 | `AStoppedRun_KeepsTheMarksItCommitted_AndRemovesNoUnlistedRecord`, `AListThatFailsPartWay_RemovesNoUnlistedRecord` (SCR) | Proven. |

## 4. Specification Coverage

- FR-001 … FR-003: `RunSynchronizationUseCase.ImportCourseWorkAsync` marks the stored items whose
  `(Resource, GoogleId)` was not returned, and `CourseWork.UpdateFrom` clears the mark. Marking runs only
  inside `ImportCourseAsync`, after the age check, so only a committed full read marks anything (FR-001.1).
  `GoogleClassroomReader.ReadCourseWorkAsync` pages both resources to the end or throws, so a partial page
  cannot cause false marks.
- FR-004: counts accumulate only from committed `CourseResult`s; the line is written before the `Failed`
  branch in `SynchronizationBackgroundService.LogOutcome`, so a stopped run logs it too; nothing is written
  when both counts are zero (I-4); the line carries the run id and two integers only.
- FR-005, FR-006: `RecordSkipAsync` in the unknown-state and out-of-range branches only; it saves on its own,
  with no course transaction open. `SyncSkippedCourse.SkipAgain` keeps `SkippedSince`.
- FR-007.1/.2: the removal is staged inside the course transaction (full read: with `RecordCompleteRead`;
  aged out: the only write). FR-007.3: runs after the `await foreach`, so an exception during enumeration or
  a course stop skips it. FR-007.4: no other branch touches records.
- FR-008: `Course.RecordCompleteRead(observedAt)`.
- VR-001 … VR-005: `RecordKeyOf` and `SyncSkippedCourse.Record` trim and cut the id to 64 and the name to
  750, store a blank name as null, refuse an undefined reason, and take every instant from `observedAt`.
- No behaviour beyond the artifacts was found.

## 5. Design Conformance

- Migration `20261026202733_AddSyncDataHealthRecords`: two nullable `timestamptz` columns with no default and
  no index, and table `sync_skipped_course` whose columns, `pk_`, `uq_` and three `ck_` constraints match
  db-design §2–§5 exactly; `Down` reverses them. The snapshot is updated.
- Write timing matches db-design §4.1.
- `TimestampInterceptor` stamps the new entity (PC-6).
- AD-3/AD-4: the port lives in `Application/Ports` and its implementation in `Infrastructure`; no
  Infrastructure or Google type reaches `Application`. AD-6: unaffected (synchronization does not run in
  read-only mode). AD-8: no controller or DTO change. API: no endpoint, consistent with NOT_APPLICABLE.

## 6. Deviations

| # | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | TEST_WRITING skeleton (optional last `skippedCourses` parameter) | Made required and placed before the optional `linking`; no artifact names the constructor; 4 hand-built test constructions adapted | Accepted |
| D-2 | Test generation report (only `AppUserMigrationTests` updated) | 4 registry tests that list tables or `course` columns follow from db-design §3/§4; updated as supporting changes | Accepted |
| D-3 | FR-004.4 left the id open | 5136 `SyncCourseWorkPresenceChanged`, the next free id in the sequence | Accepted |
| D-4 | — (db-design §4 guard) | Records keyed by the trimmed and cut id; a blank id gets no record | Accepted; matches the design |
| D-5 | Same-site doc fix in `Course.cs` | US-018 summary moved back onto `UpdateFrom` | Accepted |

## 7. Report Accuracy

The change set in report §4 equals the working tree: 13 modified and 7 new production files, and 13
modified and 5 new test files, each with a trace. No undisclosed file. The workflow-state files and the
Story's docs are in the closed list. No secret, database file, `.xlsx` or IDE config is in the change set.
Markers: `rg` over the new and changed files finds no `TODO`/`TBD`/`FIXME`/`NotImplementedException`/skipped
test. The only hit is the stale "skeleton" remark (m-1). The report's "full suite not rerun after a
test-only fix" stands as disclosed; `security-reviewer` runs the full suite.

## 8. Story Test Run (delegated to cheap-worker, verbatim totals)

Build: `dotnet build ClassroomAgent.sln` exit 0, 0 warnings, 0 errors. Docker was running and about 5.9 GB of
RAM was free.

| Class | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| CourseWorkPresenceTests | 10 | 10 | 0 | 0 |
| SkippedCourseRecordTests | 12 | 12 | 0 | 0 |
| SyncDataHealthInvariantTests | 20 | 20 | 0 | 0 |
| SyncDataHealthPersistenceTests | 12 | 12 | 0 | 0 |
| CourseWorkPresenceSyncTests | 3 | 3 | 0 | 0 |
| AppUserMigrationTests | 6 | 6 | 0 | 0 |
| IncrementalSyncRuleTests | 37 | 37 | 0 | 0 |
| IncrementalSyncPersistenceTests | 14 | 14 | 0 | 0 |
| CourseWorkFailureTests | 3 | 3 | 0 | 0 |
| CourseWorkImportTests | 10 | 10 | 0 | 0 |
| SubmissionImportTests | 11 | 11 | 0 | 0 |
| SubmitterWithoutRosterTests | 4 | 4 | 0 | 0 |
| CourseSchemaTests | 12 | 12 | 0 | 0 |
| DeanAccountSchemaTests | 7 | 7 | 0 | 0 |
| AdminLoginCheckEveryTimeTests | 7 | 7 | 0 | 0 |
| AccessCheckAuditSchemaTests | 7 | 7 | 0 | 0 |

175/175 passed. There were no Npgsql, Ryuk or WSL timeouts.

## 9. Findings

- **m-1 (Minor)**: `src/ClassroomAgent.Application/Ports/ISyncSkippedCourseRepository.cs`, remarks. The
  remark still reads "Compile-only skeleton created at TEST_WRITING …; IMPLEMENTATION owns it from here",
  but the interface is final. It is a stale comment and has no effect on behaviour. Loop-back key, if fixed:
  `changes_required`.
- **m-2 (Minor)**: `tests/.../Application/UseCases/SkippedCourseRecordTests.cs`, class summary. It cites
  `SyncSkippedCoursePersistenceTests`, but the class is `SyncDataHealthPersistenceTests`. It is a stale
  reference.
- **m-3 (Minor)**: the use-case tests run over `SyncWorld.SkippedCourseRepository`, which applies
  `Add`/`Remove` at once without waiting for `SaveChangesAsync`. Through a real run against PostgreSQL, only
  record creation is proven (`ARealRun_RecordsTheSkippedCourse_…`). Persisting a refresh (FR-006) and the
  three removals (FR-007.1–.3) is proven by reading the code (each path ends in `SaveChangesAsync` or rides
  on the course transaction's last save) and by the repository round trip
  `ARecordReadFromTheRepository_CanBeChangedAndRemoved`. If a save were dropped, no test would fail. This is
  advisory and in line with the project's TC-1 use-case-level pattern.

## 10. Verdict

**PASS**: no Critical or Major findings. Next stage: SECURITY_REVIEW.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_VERIFICATION
  story: US-058
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/implementation/US-058-implementation-verification.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-1: ISyncSkippedCourseRepository remark still calls it a compile-only skeleton"
    - "m-2: SkippedCourseRecordTests summary cites a non-existent SyncSkippedCoursePersistenceTests"
    - "m-3: persistence of skip-record refresh/removals proven by code reading + repository round trip, not by a real run"
    - "Accepted deviations D-1 (required ctor parameter), D-2 (4 registry tests), D-3 (event 5136), D-4 (blank id no record), D-5 (Course.cs doc fix)"
```
