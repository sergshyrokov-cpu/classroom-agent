---
artifact_type: security_review
story: US-059
version: 1
status: APPROVED
created_at: 2026-10-27T15:20:00Z
updated_at: 2026-10-27T15:20:00Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-059-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-059-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: false
runtime_checks: FULL
---

# US-059 Security Review

## 1. Executive Summary

**PASS.** US-059 changes the synchronization run and the save of `WorkspaceConnection` only: stored courses the
course list no longer yields are marked beside the mirror, listed courses record their last sighting, an empty list
sets a `SyncState` flag, a change of technical account records its instant, and a negative grade or maximum skips the
course as `ValueOutOfRange`. No endpoint, page, role, policy, credential, Google scope, outbound flow, package or
audit event is added or changed. The two new log lines carry counts only. Independent build (0 warnings) and full
suite (6363/6363, 0 skipped) are green; no vulnerable package. 0 Critical, 0 Major, 0 Minor, 2 Informational.

## 2. Reviewed Artifacts

As in the front matter. `api_design` v1 records NOT_APPLICABLE (no endpoint); there is no `openapi` file.
`open_decisions` v1 APPROVED, none open. Specification v1 APPROVED at HUMAN_SPEC_APPROVAL (`88d1023`).

## 3. Security-Relevant Scope

- **Exposed functionality:** none new. The Web host's synchronization background service and the existing Admin
  save of the connection (`SaveWorkspaceConnectionUseCase`) are the only touched paths.
- **Assets:** `course` rows (Google mirror + three new bookkeeping columns), `sync_state`, `workspace_connection`
  (new `technical_account_changed_at`), the run log.
- **Trust boundaries:** Google Classroom API → `GoogleClassroomReader` (input validation of Google data);
  Application → PostgreSQL.
- **Changed files:** 17 production files (Domain 5, Application 5, Infrastructure 6 incl. migration, Web 1) and
  test files, per `git status` and implementation report §4. No `.csproj`, configuration or `Program.cs` change.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 running (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6363 total, 6363 passed, 0 failed, 0 skipped (8 m 26 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no vulnerable packages in any of the
  7 projects (source api.nuget.org).
- Delegated to `cheap-worker`: build, full test run, vulnerability listing, SDK version. Kept inline: reading the
  full source diff, the migration, the configurations, the log messages and the security-relevant tests; every
  judgement.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed touched |
| SC-2 Authentication | NOT_APPLICABLE | no sign-in or password path touched |
| SC-3 AllowedAdmin | NOT_APPLICABLE | not touched |
| SC-4 Authorization | NOT_APPLICABLE | no endpoint or page added or changed; the save's existing Admin policy unchanged |
| SC-5 Read-only mode | PASS | the new end-of-list writes live inside `RunSynchronizationUseCase.ExecuteAsync` after the existing read-only refusal (returns `SkippedReadOnly` before `observedAt`/connection read); `SaveWorkspaceConnectionUseCase` read-only refusal unchanged (spec FR-002.4) |
| SC-6 No DB UI | NOT_APPLICABLE | nothing added |
| SC-7 Key | PASS | no key or key reference read, stored or logged; `workspace_connection` gains only a timestamp |
| SC-8 Google | PASS | no new scope or call; the reader still impersonates the technical account; negative values are now classified `ValueOutOfRange` (`GoogleClassroomReader.Points`), not swallowed — the course is skipped and recorded as before |
| SC-9 Channel | NOT_APPLICABLE | domain check of the save unchanged; no Control Plane change |
| SC-10 Hygiene | PASS | events 5137/5138 log run id and counts only; the negative-value failure carries no value and no inner exception; `CourseListPresenceSyncTests.EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing` asserts no course name/id, person or grade in the run's lines |
| SC-11 Audit | PASS | spec §7: sync's own writes are not in SC-11; the account change is part of the existing audited `WorkspaceConnectionSaved` (unchanged); the v101 Admin confirmation is Story 3 |
| SC-12 Owner | NOT_APPLICABLE | `Contracts`/`ControlPlane` untouched |
| SC-13 Outbound | PASS | no new destination |

## 6. Authentication and Authorization

No endpoint or page in scope. The background run has no caller identity; it cannot be triggered by a request in a
new way. The Admin save path keeps its policy and its tests (unchanged).

## 7. Credentials, Key and Google Access

No password or key handling. Google access unchanged in scope and identity. Validation of Google data tightened:
negative `maxPoints`, `assignedGrade`, `draftGrade` are refused before the Application (FR-008, VR-005); NaN still
fails through the existing `OverflowException` path; zero passes. Covered by
`GoogleClassroomReaderValueRangeTests.ANegativeValue_IsAValueOutOfRangeFailure` and `NegativeValueSyncTests`.

## 8. Sensitive Data Exposure

- Responses/views/DTOs: none added. `CourseListMarkCounts` holds four integers, consumed only by the host's logger.
- Logs: counts only (see SC-10). The empty-list Warning gives the stored-course count, not ids.
- Audit: unchanged. Exceptions: `GoogleReadFailedException(ValueOutOfRange, null)` — no value.
- Exports and telemetry: untouched.

## 9. Input Validation

Google-returned grades and maxima are validated for sign and range at the infrastructure boundary; the database
CHECK on `course.course_state` is kept and a new `ck_course_absence_mark` enforces "kind and instant together" over a
closed code set (defense in depth; implementation D-1 made the CHECK strictly correct for NULL). The value converter
throws on an unknown code in either direction.

## 10. API Security

No endpoint added, changed or removed (API_DESIGN NOT_APPLICABLE confirmed by the diff).

## 11. Persistence and Configuration

- One migration `20261027110753_AddCourseListMarks` with the matching snapshot (PC-2); additive, nullable columns
  plus one non-null boolean with default `false`; a symmetrical `Down`. No `EnsureCreated`.
- No sensitive column added. Installation database only (AD-1).
- No configuration or profile change.

## 12. Logging, Audit and Telemetry

New events 5137 (Information) and 5138 (Warning) — counts only, tested. No audit change. Hook telemetry
`docs/hooks/tool-usage.jsonl` remains git-ignored (`.gitignore:66`).

## 13. Dependencies

No package or project reference added (no `.csproj` in the change set). Vulnerability scan clean.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| No personal data in the run's new log lines | `CourseListPresenceSyncTests.EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing` |
| Negative Google values do not reach the Application and carry no value | `GoogleClassroomReaderValueRangeTests.ANegativeValue_…`, `TheNegativeValueFailure_…`; `NegativeValueSyncTests.*` |
| Invalid mark rows rejected by the database | `CourseListMarksPersistenceTests.AnInvalidMark_IsRejectedByTheCheck` |
| Read-only refusal of the run and the save | existing US-007 / US-009 tests, unchanged and green |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A Google course carries a crafted negative grade to break the run (DB CHECK abort) | course skipped, run continues | `Points`, `NegativeValueSyncTests` | PASS |
| An Admin swaps the technical account to make courses look deleted to the Dean | courses lost after the change are marked "not visible", not "not in Classroom" | `CourseAbsenceRule.MarkFor`; `CourseListPresenceTests.ACourseLostAfterTheAccountChanged_…` | PASS |
| An empty list (account lost all visibility) marks every course gone | nothing marked; flag + Warning | `AnEmptyListWhileCoursesAreStored_…` | PASS |
| Run in read-only mode writes marks | no run, no write | existing read-only refusal precedes the new code | PASS |

## 16. Repository Hygiene

No secret-like, `.db` or `.xlsx` file in the change set; `classroom_cache.db`, `google_credentials.json`, `*.xlsx`
and telemetry are git-ignored (checked with `git check-ignore`). Credential files were not opened.

## 17. Deviations

Implementation D-1 (stricter CHECK than the db-design text) strengthens a constraint; D-2 (no empty save) and D-3
(test fixes without weakening) have no security effect. None from security requirements.

## 18. Findings

- **INFO-1** (AUDIT, SC-11) — course marks set by the run write no audit event. Matches the approved spec §7 and
  SC-11 (synchronization's own writes are not audited; the Admin confirmation that a hidden course was deleted is
  Story 3). No correction.
- **INFO-2** (DATA_EXPOSURE, SC-10) — the empty-list Warning (5138) reveals the number of stored courses in the
  application log. A count, not personal data; within SC-10. No correction.

## 19. Positive Controls

Read-only refusal ahead of every new write; validation of Google data at the boundary; database CHECK with an
explicit NOT NULL guard; log lines restricted to counts and tested for absence of names and ids; no new dependency;
migration-only schema change.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus a full test run; no penetration testing. Live Google behaviour of negative
values is covered only by synthetic fixtures (TC-4).

## 22. Verdict Rationale

Green build and suite verified independently; no Critical, Major or Minor finding; every touched SC item PASS; no open
security decision. The OPEN findings US-059/IMPLEMENTATION/m-1, US-059/IMPLEMENTATION_VERIFICATION/m-2 and
US-059/IMPLEMENTATION_VERIFICATION/INFO-1 have no security impact (the implemented CHECK is the stricter one; the
others are test coverage and write volume) and are left OPEN for the human.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-059
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-059-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "INFO-1: course marks set by the run write no audit event — matches spec §7 and SC-11; the Admin confirmation is Story 3"
    - "INFO-2: the empty-list Warning (5138) logs the number of stored courses — a count only, within SC-10"
  closed_findings: []
```
