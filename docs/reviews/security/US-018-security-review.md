---
artifact_type: security_review
story: US-018
version: 1
status: APPROVED
created_at: 2026-10-12T08:37:44Z
updated_at: 2026-10-12T08:37:44Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-018-incremental-sync.md
    version: null
  - path: docs/specifications/US-018-spec.md
    version: 1
  - path: docs/decisions/US-018-open-decisions.md
    version: 2
  - path: docs/designs/database/US-018-db-design.md
    version: 1
  - path: docs/designs/database/US-018-entity-model.md
    version: 1
  - path: docs/tests/US-018-test-strategy.md
    version: 1
  - path: docs/tests/US-018-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-018-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: false
runtime_checks: PARTIAL
---

# US-018 Security Review — Incremental synchronization

## 1. Executive Summary

**Result: PASS.** US-018 changes only how the background synchronization run
chooses which courses to read. It adds no endpoint, page, policy, Google scope,
outbound destination, audit action, setting or package. The new state is two
nullable timestamps (`course.complete_read_update_time`,
`sync_state.last_full_run_at`), and they hold no personal data. The only new
log content is an integer count.

The security properties that matter here hold, and each was checked in the code:

- the read-only guard still runs before any lookup or Google call;
- skipping a course issues fewer Google reads and no new kind of read;
- the retention purge criteria do not depend on the skipped writes;
- no course id or name reaches the log.

There are no Critical, Major or Minor findings. Three Informational
observations are listed in §18. Next: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

All inputs are listed in the front matter. None is `SUPERSEDED`. The
Specification v1 was approved at `HUMAN_SPEC_APPROVAL` on
2026-10-12T06:06:15Z. API design and OpenAPI are `NOT_APPLICABLE`: the spec
(§1, §7, §10) defines no contract change.

## 3. Security-Relevant Scope

- **Hosts.** Data Plane only, through the background synchronization path (the
  scheduled run and the US-019 button run share `RunOnceAsync`). The Control
  Plane is untouched.
- **Assets.** The synchronized mirror (courses, rosters, items and
  submissions of minors), sync bookkeeping and the application log.
- **Trust boundaries.** Application → Google Classroom port (read-only), and
  Application → PostgreSQL.
- **Changed components.**
  - `IncrementalSyncRule` (new; Domain);
  - `Course.RecordCompleteRead`;
  - `SyncState.CompleteRun(…, fullRun)`;
  - `ICourseRepository.GetCompleteReadUpdateTimeAsync` and its EF
    implementation;
  - `RunSynchronizationUseCase` (skip decision, full-run decision);
  - `SynchronizationRunOutcome.CoursesSkippedUnchanged`;
  - two `LoggerMessage` templates in `SynchronizationBackgroundService`;
  - migration `20261012073623_AddIncrementalSync`.

## 4. Environment and Tools

- .NET 10 SDK. Docker 29.8.0 was available (Testcontainers).
- Delegated to `cheap-worker`, which reported facts only:
  - `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors;
  - `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`:
    no vulnerable packages in any of the 7 projects;
  - `git diff HEAD --stat -- '*.csproj'`: empty;
  - a filtered `dotnet test` run: 398 passed, 0 failed, 0 skipped. The filter
    covered the US-018 classes, the sync and schema classes, and the
    anonymous-endpoint and endpoint enumeration classes.
- Read by the reviewer:
  - the full `git diff HEAD -- src/`;
  - the new rule and migration;
  - `RunRetentionPurgeUseCase` and `RetentionPurgeStore` (expiry criteria);
  - the host log test;
  - `.gitignore` coverage of the credential, cache and telemetry files.
- Not run by the reviewer: the full suite. The implementation report records
  5307 tests with 274 environmental Npgsql timeouts, and all affected classes
  were rerun green. That evidence is accepted; see §21.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed change. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie code touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin login untouched. |
| SC-4 Authorization | PASS | No endpoint or page added. The anonymous/endpoint enumeration tests ran green in the filtered run. |
| SC-5 Read-only mode | PASS | `RunSynchronizationUseCase.ExecuteAsync`: the read-only and connection guards return before `BeginAsync`. The new lookup and the skip run only inside `ImportCoursesAsync`, so read-only mode reaches neither. AC-009 test `InReadOnlyMode_…` is green. |
| SC-6 No DB UI | NOT_APPLICABLE | Nothing exposed. |
| SC-7 Key | PASS | No configuration change. The key and its reference are untouched. |
| SC-8 Google | PASS | No scope or port change. A skip *removes* reads (`ReadRosterAsync`, items, submissions) for that course. The weekly full run restores the full read set. The impersonation user is unchanged. Failure handling is unchanged: a failed read still propagates and is not swallowed (AC-005 tests). |
| SC-9 Channel | NOT_APPLICABLE | No Control Plane or channel change. |
| SC-10 Hygiene | PASS | The new log properties are `SkippedUnchanged` (an `int`) on events 5122/5123. The skip branch logs nothing per course. `TheLog_NamesNoSkippedCourse` asserts that neither the course id nor the name appears in the log files, with a control that a skip happened. |
| SC-11 Audit | NOT_APPLICABLE | Synchronization is not a new audited action. No audit code touched. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and `ControlPlane` untouched. |
| SC-13 Outbound | PASS | No new destination. Google traffic only decreases. |

## 6. Authentication and Authorization

No change. The button run (US-019) keeps its endpoint, policy and contract;
both paths reach the use case through `RunOnceAsync` (AC-007 tests). The
background path has no caller-supplied identifier, so it cannot be steered to
another installation's or course's data.

## 7. Credentials, Key and Google Access

No password or key handling. The Google access model is unchanged:
service account, technical impersonation user, read-only scopes. The skip
decision uses only `snapshot.Details.UpdateTime` and the listed state. Both
arrive already parsed and validated by the existing course-list mapping
(`TryParseState`; `DateTimeOffset?`), so no raw Google string reaches the rule.

## 8. Sensitive Data Exposure

- **Responses and views:** unchanged; no DTO change.
- **`SynchronizationRunOutcome`:** gains an `int`. The outcome is internal to
  the host.
- **Logs:** count only, verified by a test (SC-10).
- **Audit, exports, telemetry:** untouched.
- **Fixtures:** synthetic (`CourseTestData`, `FakeClassroomReader`), per TC-4.

## 9. Input Validation

The only new external input is Google's `updateTime`, a nullable
`DateTimeOffset` already produced by the existing mapper. The rule handles all
of these cases explicitly:

- a null value on either side means not skipped (VR-003);
- a stored time later than the run start makes the run full (I-5, VR-002);
- values are compared after truncation to microseconds (VR-001).

The `IncrementalSyncRuleTests` cover each case. A malformed or absent value
fails safe, toward a full read.

## 10. API Security

No endpoint added or changed. API design `NOT_APPLICABLE` was confirmed against
the change set: no controller, page or `Contracts` file changed.

## 11. Persistence and Configuration

- **Migration** `AddIncrementalSync`. `Up` adds two nullable `timestamptz`
  columns and `ck_sync_state_last_full_run_at`; `Down` reverses all three. No
  `EnsureCreated`. The migration ships in the Story (PC-2).
- **Constraint.** `last_full_run_at IS NULL OR (last_successful_run_at IS NOT
  NULL AND last_full_run_at <= last_successful_run_at)`. It backs the domain
  rule that only a completed run writes the full-run time.
- **Isolation.** The new lookup is untracked and single-column, so it cannot
  alter the tracked course loaded later in the course transaction.
  `RecordCompleteRead` is staged before that transaction's last
  `SaveChangesAsync`, so a rollback keeps the old value. A rollback therefore
  makes the next run read the course in full, which fails safe.
- **Retention purge (PC-11).** Checked because skipping means fewer writes:
  - course expiry (`RunRetentionPurgeUseCase.IsExpired`) uses Google dates,
    never observation times;
  - leaver expiry (`RetentionPurgeStore.GetCourseIdsWithExpiredLeaversAsync`)
    needs `!OnRoster && LastSeenAt < cutoff`, and a skipped course's
    memberships are not touched at all, so no membership becomes a leaver
    because of a skip.

  The purge therefore neither deletes early nor keeps data longer because of
  US-018.
- **Configuration.** No configuration change; the 7-day period is a constant
  (I-6).
- **Generated files.** None in the change set.

## 12. Logging, Audit and Telemetry

- **Logs:** the event templates 5122/5123 gain `{SkippedUnchanged}`, as covered
  in §8.
- **Audit:** no new audited action.
- **Telemetry:** `docs/hooks/tool-usage.jsonl` remains git-ignored
  (`.gitignore:63`).

## 13. Dependencies

No `.csproj` change, so there is no new package or project reference. The
`Domain → Domain.Rules` usage stays inside Domain (AD-3). The vulnerability scan
found nothing against nuget.org at review time.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| Read-only mode: no lookup, no Google call | `IncrementalSyncRunTests.InReadOnlyMode_…` (AC-009) |
| No course identity in logs | `IncrementalSyncHostTests.TheLog_NamesNoSkippedCourse` (with a positive control) |
| Failure is not swallowed; the failed course's value does not advance | `IncrementalSyncRunTests.AFailedCourseRead_…` (9 cases), `ANewCourseWhoseReadFailed_…` |
| Fail-safe toward a full read (null, future, precision) | `IncrementalSyncRuleTests` |
| Schema constraint | `IncrementalSyncPersistenceTests` (column, check) |
| No live Google | All tests use `FakeClassroomReader` (TC-4, AC-010) |

These ran green: 398 in the filtered run by `cheap-worker`.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A sync is triggered while read-only | Refused before any read | Guard order in `ExecuteAsync`; AC-009 test | PASS |
| A course is changed in Google without its `updateTime` changing, so it is hidden from the mirror | Limited to archived courses; the weekly full run re-reads everything (requirements v90, OD-001 a) | `IsUnchangedArchive` requires `Archived`; `IsFullRunDue` | PASS (accepted by requirements; see INFO-3) |
| The clock is moved back to suppress full runs | A future stored time forces a full run | `IsFullRunDue` (`last > runStartedAt`); rule test | PASS |
| Logs are inspected for which courses were skipped | Count only | Host log test | PASS |

## 16. Repository Hygiene

- `google_credentials.json`, `dac-classroom-agent-*.json` and
  `classroom_cache.db` are git-ignored. The files were not opened.
- The working tree's untracked files are the Story's code, tests and artifacts
  only. There is no `.db`, `.xlsx`, `.env` or `appsettings` change.

## 17. Deviations

- The implementation report §7 records two items:
  - the corrected `AFullRunTimeInTheFuture` assertion, made with the Owner's
    agreement;
  - an inaccuracy in db-design §3.

  Neither has a security effect. A run after the clock moves back stays full,
  which is the safe direction.
- No undocumented security behavior was found.

## 18. Findings

**INFO-1 — OTHER (documentation) — `src/ClassroomAgent.Domain/Entities/Course.cs`.**
- **Observed.** `RecordCompleteRead` was inserted between the existing XML
  `<summary>` of `UpdateFrom` and `UpdateFrom` itself. As a result,
  `RecordCompleteRead` carries two summaries (its own and `UpdateFrom`'s), and
  `UpdateFrom` has none.
- **Risk.** None to security; it misleads a reader.
- **Correction (optional).** Move the `UpdateFrom` summary back above
  `UpdateFrom`.
- **Loop-back.** None required.

**INFO-2 — PERSISTENCE — `ClassroomAgentDbContextModelSnapshot.cs`.**
- **Observed.** The snapshot's `ck_sync_state_terminal_fields` text changed from
  `\r\n` to `\n`. The constraint is a raw string literal in
  `SyncStateConfiguration.cs`, so the model's SQL depends on the line endings of
  the working copy (`core.autocrlf=true`; the file is `w/lf` now).
- **Risk.** A checkout with CRLF endings produces a model that differs from the
  snapshot. A later `migrations add` may then emit a spurious drop/add of that
  constraint, or EF may report pending model changes. This is pre-existing
  fragility (US-013), surfaced here, with no security impact.
- **Suggestion.** Pin `*.cs` line endings in `.gitattributes`, or normalise the
  constraint string, in a future Story.

**INFO-3 — GOOGLE_ACCESS (data freshness) — `RunSynchronizationUseCase.ImportCoursesAsync`.**
- **Observed.** A skipped archived course's roster, items and submissions are
  not re-read until the weekly full run, unless its `updateTime` moves. If
  Google does not move a course's `updateTime` on a roster-only change, an
  archived course's roster in the mirror can lag by up to 7 days.
- **Risk.** Low. This is the behavior requirements v90 approves, with OD-001 a
  and OD-002 a resolved. Access control does not depend on the roster (v1 has no
  Teacher or Student accounts), and retention is unaffected (§11).
- **Correction.** None required. Recorded for the human gate.

## 19. Positive Controls

- The read-only guard precedes all new behavior.
- Every uncertain input fails safe toward a full read: null values, a future
  stored time, and precision differences.
- The remembered value advances only inside the committed course transaction.
- The full-run time is written only by a completed run, and a DB check backs
  this.
- No personal data in the new log content, test-proven.
- No new package, scope, endpoint or destination.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 … OD-003 are
resolved by the Owner.

## 21. Review Limitations

- The reviewer did not repeat the full 5307-test suite. Evidence:
  - the implementation report (environmental Npgsql timeouts, affected classes
    rerun green);
  - the 398-test filtered run during this review.
- Not checked against a live domain: whether Google moves a course's
  `updateTime` on roster-only changes (INFO-3). TC-4 forbids live calls.
- This is a code, configuration and test review, not a penetration test.

## 22. Verdict Rationale

The implementation is green: the build has 0 warnings, the targeted tests pass
and no vulnerable packages were found. Every touched SC item is `PASS`, and
there is no Critical, Major or Minor finding. The Story narrows Google reads
without widening any access, data flow or exposure, so the verdict is **PASS**.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-018
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-018-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "INFO-1: Course.cs — UpdateFrom's XML summary now sits on RecordCompleteRead (doc placement only)"
    - "INFO-2: model snapshot check-constraint text follows working-copy line endings (pre-existing, US-013 raw string)"
    - "INFO-3: archived course roster may lag up to 7 days if Google keeps updateTime on roster-only changes — approved by requirements v90"
```
