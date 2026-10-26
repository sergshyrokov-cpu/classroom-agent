---
artifact_type: security_review
story: US-058
version: 1
status: APPROVED
created_at: 2026-10-26T21:20:00Z
updated_at: 2026-10-26T21:20:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md
    version: null
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
  - path: docs/designs/database/US-058-db-design.md
    version: 1
  - path: docs/designs/database/US-058-entity-model.md
    version: 1
  - path: docs/tests/US-058-test-strategy.md
    version: 1
  - path: docs/tests/US-058-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-058-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-058-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: false
runtime_checks: FULL
---

# US-058 Security Review

Delegation: the build, the full test run, the vulnerable-package listing and the change set (`git status`,
`.csproj` diff) were run by a `cheap-worker` subagent and reported verbatim. Every source file of the change set
was read by the reviewer directly; the read-only gate, the log line and the persistence configuration were checked
in the code, not from the reports.

## 1. Executive Summary

**PASS — 0 Critical, 0 Major, 0 Minor, 3 Informational.**

The Story changes only the background synchronization: a nullable instant on `course_work`
(`not_in_classroom_since`) and on `course` (`last_complete_read_at`), a new table `sync_skipped_course`, and one
new log event (5136) carrying two counts. No endpoint, page, role, policy, Google scope, Google request, outbound
destination or package is added. All new writes happen after the use case's read-only gate. The independent build
is clean and the full suite is green (6278/6278).

## 2. Reviewed Artifacts

As listed in `inputs`. API_DESIGN was recorded NOT_APPLICABLE (no endpoint; no `openapi.yaml` by design).
HUMAN_SPEC_APPROVAL is recorded (2026-10-26T19:49:19Z). No input is SUPERSEDED.

## 3. Security-Relevant Scope

- **Host:** installation (`ClassroomAgent.Web`) background service only. Nothing reachable by HTTP changes.
- **Assets:** stored Google mirror (course work, courses); course names (may contain a teacher's name, spec §7);
  run log.
- **Trust boundaries:** Google → Application (data returned by the Classroom API — validated); Application →
  PostgreSQL (new columns and table via migration).
- **Components:** `RunSynchronizationUseCase`, `CourseWork`, `Course`, `SyncSkippedCourse`,
  `SyncSkippedCourseConfiguration`/`Repository`, migration `20261026202733_AddSyncDataHealthRecords`,
  `SynchronizationBackgroundService.LogCourseWorkPresenceChanged`, DI registration.

## 4. Environment and Tools

- .NET 10 SDK, Windows 10; Docker running (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6278 total, 6278 passed, 0 failed, 0 skipped (8 m 26 s); no Npgsql/Ryuk/WSL
  timeouts in the log.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — nuget.org reachable; no vulnerable
  packages in any of the seven projects.
- No penetration testing; code, configuration and test review only.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed changed. |
| SC-2 Authentication | NOT_APPLICABLE | No authentication code changed. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | NOT_APPLICABLE | No endpoint or page added; no `[AllowAnonymous]` / antiforgery exemption in the change set. |
| SC-5 Read-only mode | PASS | `RunSynchronizationUseCase.ExecuteAsync` calls `readOnlyMode.EnsureAllowedAsync` first and returns `SkippedReadOnly` before `ImportCoursesAsync`, the only path to the new writes (marks, skip records, `LastCompleteReadAt`). No Google port is reached in read-only mode. Spec §7: no new BR-026 service write. |
| SC-6 No DB UI | NOT_APPLICABLE | No UI. |
| SC-7 Key | PASS | No configuration, key or key reference touched; no `appsettings` change. |
| SC-8 Google | PASS | No new scope or request; every fact comes from the existing course list and course-work reads. |
| SC-9 Channel | NOT_APPLICABLE | Untouched. |
| SC-10 Hygiene / logs | PASS | Event 5136 carries `RunId` and two integers only; written only when one is non-zero. `CourseWorkPresenceSyncTests.EachRun_LogsOneLineCountingMarkedAndUnmarkedItems_AndNamesNothing` asserts exactly two numbers and absence of course id, titles and a sentinel grade. No exception text from Google is stored (VR-003: reason is a closed enum). |
| SC-11 Audit | NOT_APPLICABLE | Synchronization writes are not in the SC-11 list (spec §7). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and `ControlPlane` untouched. |
| SC-13 Outbound | PASS | No new destination, HTTP client or package. |

## 6. Authentication and Authorization

No change. No endpoint, page, policy or role is added or modified; the background service runs without a user.

## 7. Credentials, Key and Google Access

No password, key or credential handling changed. Google access remains read-only through the existing port;
the technical-account impersonation path is unchanged.

## 8. Sensitive Data Exposure

- **Responses/views/DTOs:** none added. `SynchronizationRunOutcome` gains two `int` counts only.
- **Persistence:** `sync_skipped_course` stores Google course id, Google course name (cut to 750), reason code,
  instants — no student data. The course name is the same value already stored in `course.name`. See INFO-1.
- **Logs:** see SC-10. The pre-existing per-course lines (e.g. 5130 value-out-of-range with course Google id) are
  unchanged.
- **Exceptions:** none new carry data; `ArgumentOutOfRangeException` for an undefined reason carries an enum value.
- **Exports, telemetry:** untouched.

## 9. Input Validation

Data returned by Google is validated before storage:
- course id trimmed and cut to `Course.MaxGoogleIdLength` (64); blank id → no record (`RecordKeyOf`,
  `SyncSkippedCourse.Record` throws on blank);
- course name trimmed and cut to `Course.MaxNameLength` (750), blank → null;
- reason restricted by `Enum.IsDefined` and by the converter;
- database backs each rule: `varchar(64)`/`varchar(750)`, check constraints on reason, id length and name length,
  unique index on `course_google_id`.

## 10. API Security

No API surface. NOT_APPLICABLE.

## 11. Persistence and Configuration

- Schema change ships with migration `AddSyncDataHealthRecords` (PC-2); `Down` reverses it. No `EnsureCreated`.
- New columns nullable, no sensitive column. Single installation database (AD-1).
- `SyncSkippedCourse` stamped by `TimestampInterceptor` (PC-6).
- No configuration file changed.

## 12. Logging, Audit and Telemetry

One new structured log event with counts only (tested). No audit change required. `docs/hooks/tool-usage.jsonl`
remains git-ignored (`.gitignore:66`).

## 13. Dependencies

`git diff --stat HEAD -- '*.csproj'` is empty: no package or project reference added. Vulnerability scan: none
reported. New Application port `ISyncSkippedCourseRepository` uses only Domain types (AD-3, AD-4).

## 14. Security Test Coverage

| Property | Test |
|---|---|
| Log line carries two counts and names nothing (SC-10) | `CourseWorkPresenceSyncTests.EachRun_LogsOneLineCountingMarkedAndUnmarkedItems_AndNamesNothing` |
| Stored values cut, blank id refused, reason closed (validation) | `SkippedCourseRecordTests`, `SyncDataHealthPersistenceTests` |
| Marks roll back with a failed course transaction | `SyncDataHealthInvariantTests` (AC-006) |
| Read-only mode blocks the run before any write | pre-existing read-only tests of `RunSynchronizationUseCase` (unchanged gate); see INFO-2 |
| No live Google call; synthetic fixtures (TC-4) | `FakeClassroomReader`, `SyncWorld` |
| Real PostgreSQL (TC-2) | `SyncDataHealthPersistenceTests` via Testcontainers |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Sync triggered while installation is read-only | No mark or record written | Gate at the top of `ExecuteAsync` | PASS |
| A teacher names a course with an oversized or blank name | Cut / null, never a failed insert | `SkipAgain`, check constraints | PASS |
| Google returns an oversized course id | Cut to 64 consistently for lookup and storage | `RecordKeyOf` = `Record` cut | PASS |
| Inspecting the run log for personal data | Only counts | SC-10 test | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db` and `tool-usage.jsonl` are git-ignored
(checked with `git check-ignore`; credential files not opened). No generated database file, `.xlsx` export,
`.env` or secret-like file in the change set.

## 17. Deviations

None with security effect. Implementation deviations D-1…D-5 (accepted at IMPLEMENTATION_VERIFICATION) concern
constructor shape and tests only.

## 18. Findings

**INFO-1 — DATA_EXPOSURE (SC-10, PC-11).** `sync_skipped_course.course_name` may contain a teacher's name and is not
touched by the retention purge (spec I-6, approved). The record leaves only when the course is read fully, ages out,
or disappears from Google's list, so a course that Google keeps listing with a persisting problem keeps its name
here indefinitely — no longer than `course.name` would for a listed course. No correction required; worth keeping in
mind if personal-data erasure is ever extended to teachers.

**INFO-2 — TEST_COVERAGE (TC-5).** No US-058-specific test asserts that a read-only run leaves the new tables
untouched. The gate is unchanged and precedes the only code path to the new writes, and pre-existing tests cover
the gate, so the property holds by construction. No correction required.

**INFO-3 — PERSISTENCE.** `RecordSkipAsync` saves outside any course transaction (spec §8, as designed). A failure
there stops the run like any database failure; nothing partial of a course is written. No security effect.

## 19. Positive Controls

- Read-only gate precedes all new writes (verified in code).
- Google-sourced values bounded in code and in the database.
- Count-only log line, tested for absence of identifiers and grades.
- Migration-only schema change; no new package; no new outbound flow.
- Full independent build and test run green.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 is RESOLVED (a).

## 21. Review Limitations

- No penetration testing; no live Google or Control Plane (by policy).
- Persistence of skip-record refresh/removal across real runs is proven by repository round trips and code reading
  (implementation verification m-3), not by an end-to-end run against Google.

## 22. Verdict Rationale

The build and full suite are green, no Critical or Major finding exists, every touched SC item is PASS, and no
security Open Decision is open. Verdict **PASS**; next stage HUMAN_PR_APPROVAL.
