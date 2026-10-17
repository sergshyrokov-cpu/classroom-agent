---
artifact_type: security_review
story: US-053
version: 1
status: APPROVED
created_at: 2026-10-17T00:00:00Z
updated_at: 2026-10-17T00:00:00Z
produced_by: security-reviewer
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
  - path: docs/tests/US-053-test-strategy.md
    version: 1
  - path: docs/tests/US-053-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-053-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-053-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: false
runtime_checks: FULL
---

# US-053 Security Review — A failed synchronization run is always recorded as failed

## 1. Executive Summary

**Result: PASS.** 0 Critical, 0 Major, 0 Minor, 4 Informational.

The change adds one port method, `IUnitOfWork.DiscardUncommittedChanges()`, which
detaches every `Added`/`Modified`/`Deleted` tracker entry except `SyncState`, and
calls it first in the run's general failure catch of
`RunSynchronizationUseCase`. Both decorators (`ReadOnlyModeUnitOfWork`,
`LoggingUnitOfWork`) forward it. No endpoint, page, policy, role, Google call,
schema, package, log event, audit event or configuration changes.

Security-wise the change only narrows what a failure save can write: the failure
save now writes the `SyncState` row only, and an uncommitted automatic Meet link
and its audit row are discarded together (never one without the other). The
stored diagnosis stays a closed-list code (SC-10).

Independent evidence: build 0 warnings / 0 errors; full suite 6076/6076 green
(12 m 12 s) after the Docker/WSL backend was recovered (see §4, INFO-4);
story class 8/8 green; no vulnerable packages.

## 2. Reviewed Artifacts

Listed in front matter `inputs`, all v1 and not `SUPERSEDED`; specification and
open decisions `APPROVED` (HUMAN_SPEC_APPROVAL recorded 2026-10-16); OD-001
resolved (a). API and DB designs are `NOT_APPLICABLE` records.

## 3. Security-Relevant Scope

- Hosts: Data Plane background synchronization service only. No HTTP surface.
- Changed production files: `Application/Ports/IUnitOfWork.cs`,
  `Application/UseCases/ReadOnlyModeUnitOfWork.cs`,
  `Application/UseCases/RunSynchronizationUseCase.cs`,
  `Infrastructure/Persistence/UnitOfWork.cs`,
  `Infrastructure/ReadOnly/LoggingUnitOfWork.cs`.
- Assets touched: mirrored Google data (courses, participants, course work, Meet
  sessions) staged in the run's scope; `SyncState` row; `MeetCodeAutoLinked`
  audit rows staged by `LinkMeetCodesStep`.
- Trust boundaries: Application → persistence port (in process); application →
  PostgreSQL. No Google or Control Plane boundary is touched.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 with Testcontainers PostgreSQL.
- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors (delegated, cheap-worker).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no vulnerable packages in any of the 7 projects (delegated).
- `git diff --stat` on `*.csproj` / `Directory.*.props` — empty.
- `dotnet test --filter FullyQualifiedName~FailedRunRecordedTests` — 8/8 passed (22 s).
- `dotnet test ClassroomAgent.sln` — 6076 passed, 0 failed, 0 skipped (12 m 12 s).
- Two earlier full runs (delegated) failed with 64 and 3783 infrastructure errors
  (`NpgsqlException: Exception while reading from stream`, `TimeoutException` in
  `PostgreSqlFixture.CreateDatabaseAsync`; then `ResourceReaperException`) and a
  Docker restart hit `Wsl/Service/CreateInstance/ERROR_TIMEOUT`. After stopping
  Docker Desktop, `wsl --shutdown` and a restart, the same code ran green. These
  were environment failures, not test assertions (INFO-4).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed change |
| SC-2 Authentication | NOT_APPLICABLE | no sign-in code touched |
| SC-3 AllowedAdmin | NOT_APPLICABLE | not touched |
| SC-4 Authorization | NOT_APPLICABLE | no endpoint or page; "Last synchronization" stays on the Admin-only connection page (AC-004 test reads it through the Application query) |
| SC-5 Read-only mode | PASS | `EnsureAllowedAsync` still guards the run at its start (`RunSynchronizationUseCase.ExecuteAsync`); `ReadOnlyModeUnitOfWork.DiscardUncommittedChanges` forwards — a discard writes nothing, `PermittedServiceWrites` unchanged; `SaveChangesAsync` backstop untouched |
| SC-6 No DB UI | NOT_APPLICABLE | nothing exposed |
| SC-7 Key | NOT_APPLICABLE | not touched |
| SC-8 Google | PASS | no Google call added; permission failures still stop the run with their code (not swallowed) |
| SC-9 Channel | NOT_APPLICABLE | not touched |
| SC-10 Hygiene | PASS | stored value is `DiagnosisOf(failure)` (closed list); host line carries exception type name only for `Unexpected`; no new log statement; test-only CHECK constraints live in the test database (spec I-5), no seam in `src/` |
| SC-11 Audit | PASS | no audited action added; `MeetCodeAutoLinked` row and its link are staged in one transaction (`LinkMeetCodesStep.LinkAsync`), so the discard drops both or neither (INFO-2) |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` / Control Plane untouched |
| SC-13 Outbound | PASS | no new destination |

## 6. Authentication and Authorization

No change. The run is a background service path guarded by read-only mode and
connection usability exactly as before; no request-supplied identifier reaches it.

## 7. Credentials, Key and Google Access

Not touched. Google ports are substituted in all story tests with synthetic data (TC-4).

## 8. Sensitive Data Exposure

- Responses/views/DTOs: unchanged.
- Logs: no new log call; `LoggingUnitOfWork` forwarder logs nothing.
- `SyncState.LastError`: diagnosis code only (VR-001).
- Audit rows: none added; see INFO-2.
- Exports, telemetry: untouched.

## 9. Input Validation

Google-returned data stays validated by the domain; AC-007 tests prove a domain
refusal inside a course transaction now leaves nothing of that course stored —
before this change the failure save could persist part of a course whose
transaction had failed (AC-007a). This strengthens the "mirror, never partial"
property.

## 10. API Security

No endpoint added or changed; `NOT_APPLICABLE` API design confirmed by the diff.

## 11. Persistence and Configuration

- No migration, no schema change, no `EnsureCreated`.
- `DiscardUncommittedChanges` uses `EntityState.Detached`, not `ChangeTracker.Clear()`; excludes `SyncState` by type (one per installation; INFO-3).
- No configuration change.

## 12. Logging, Audit and Telemetry

No sensitive logging introduced. Audit coverage unchanged. Hook telemetry not touched.

## 13. Dependencies

No package or project reference added; `Application → Infrastructure` boundary kept
(the port lives in `Application/Ports`). Vulnerability scan clean.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| Failure save writes `SyncState` only (course / Meet / linking rejection) | `FailedRunRecordedTests` AC-001…AC-003 |
| Nothing staged of a failed course is stored on a non-DB failure | AC-007 (two tests) |
| Diagnosis code, not message, recorded; one failure log line | `ARejectedCourseWrite_LogsTheFailureLine_AndNotTheEscapedLine` |
| Admin view shows the failure | AC-004 |
| Read-only mode of the run | existing US-013 tests, green in the full run |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Run starts in read-only mode | skipped before any staging | guard unchanged at method start; existing tests green | PASS |
| Failing transaction leaves half a course / link without audit | nothing of it persisted | discard + AC-003/AC-007 tests | PASS |
| Exception text with Google data reaches DB or log | never | closed-list code; type name only | PASS |

## 16. Repository Hygiene

- `google_credentials.json` git-ignored; no `.db`, generated `.xlsx` or key file tracked.
- `.claude/settings.local.json` is modified (adds a `Bash(git push)` allow rule) — IDE-local, out of the Story's scope; must not be staged (INFO-1, DoD item 8).
- New untracked files are Story artifacts and `FailedRunRecordedTests.cs` only.

## 17. Deviations

Disclosed in the implementation report and confirmed by verification: D-1
rolled-back `Unchanged` entries stay tracked (I-2), D-2 no-op test doubles, D-3
US-032/US-048 detaches kept. None has a security effect.

## 18. Findings

- **INFO-1** (REPOSITORY_HYGIENE) `.claude/settings.local.json` modified; keep it out of the Story commit. No correction to code.
- **INFO-2** (AUDIT, SC-11) The discard also drops a staged-but-uncommitted `MeetCodeAutoLinked` audit row; it is always staged together with its link in one transaction, so audit and data stay consistent. Recorded so a future Story that stages an audit row outside a transaction knows the failure path discards it.
- **INFO-3** (PERSISTENCE) `SyncState` is excluded by type, not instance (= verification N-2); correct while the run scope tracks a single `SyncState`.
- **INFO-4** (OTHER) The first two full runs failed for Docker/WSL reasons; the green 6076/6076 run came after `wsl --shutdown` and a Docker restart.

## 19. Positive Controls

Read-only guard at run start; decorator chain preserved; closed-list diagnosis
code; no seam in `src/` (test CHECK constraints only); atomic link + audit row;
no new dependency; clean vulnerability scan.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus local runs only; no penetration test.
Delegated to cheap-worker: build, vulnerability list, csproj diff, the two
infrastructure-failed full runs. The green full run and the story run were
executed and read by the reviewer.

## 22. Verdict Rationale

Green build and full suite independently reproduced; every touched SC item PASS;
no Critical, Major or Minor finding; no open security decision. Verdict **PASS**,
next stage `HUMAN_PR_APPROVAL`.
