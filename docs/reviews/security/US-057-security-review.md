---
artifact_type: security_review
story: US-057
version: 1
status: APPROVED
created_at: 2026-10-25T22:30:00Z
updated_at: 2026-10-25T22:30:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md
    version: null
  - path: docs/specifications/US-057-spec.md
    version: 2
  - path: docs/decisions/US-057-open-decisions.md
    version: 2
  - path: docs/designs/api/US-057-api-design.md
    version: 2
  - path: docs/designs/database/US-057-db-design.md
    version: 2
  - path: docs/tests/US-057-test-strategy.md
    version: 1
  - path: docs/tests/US-057-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-057-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-057-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: false
runtime_checks: FULL
---

# US-057 Security Review

## 1. Executive Summary

**PASS — 0 Critical, 0 Major, 0 Minor, 4 Informational.**

The Story changes how synchronization handles grades or maximum points that Google returns beyond the `decimal` range. It also bounds the report scale percent. It adds no endpoint, page, role, policy, Google scope, outbound destination, schema change, package or audited action. The only security-relevant surfaces are these:

- the new failure carries no Google value or identifier (SC-10);
- the new log line 5135 names the run and the course id only (SC-10, DC-10);
- the skipped course writes nothing.

All three were checked in the code and are pinned by tests. The independent build is 0/0. The full suite passed 6221/6221. No vulnerable package was found.

## 2. Reviewed Artifacts

The artifacts are listed in the front matter `inputs`. The API and DB designs v2 are NOT_APPLICABLE records. The specification and open decisions are v2 APPROVED (HUMAN_SPEC_APPROVAL attempt 2, 2026-10-25). The implementation verification is v1 PASS. No input is SUPERSEDED.

## 3. Security-Relevant Scope

Changed files (6 src, all `M`):

- `Application/Models/GoogleReadFailureKind.cs`: new member `ValueOutOfRange`.
- `Application/Models/SynchronizationRunOutcome.cs`: new list `CoursesValueOutOfRange` (course ids).
- `Application/UseCases/RunSynchronizationUseCase.cs`: catches `ValueOutOfRange` around the per-course reads, before the transaction.
- `Application/UseCases/ReportBuilder.cs`: `Grade` returns 100 % for points ≥ max and 0 % for points ≤ 0 without dividing.
- `Infrastructure/Google/GoogleClassroomReader.cs`: the `Points` helper turns a cast `OverflowException` into `GoogleReadFailedException(ValueOutOfRange, null)` with no inner exception.
- `Web/BackgroundServices/SynchronizationBackgroundService.cs`: log event 5135 `SyncCourseValueOutOfRange`, Warning.

Assets: student grades and course data read from Google, and the application log. Trust boundary: Google API response → Infrastructure adapter → Application. This is data returned by Google, which counts as external input under the Security Policy. No HTTP surface changes.

## 4. Environment and Tools

.NET SDK 10.0.401. Docker 29.8.0 with Testcontainers available. The commands were delegated to `cheap-worker`; their results are reported verbatim:

- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln`: 6221 total, 6221 passed, 0 failed, 0 skipped (8 min 06 s). No Npgsql or Ryuk errors.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`: no vulnerable packages in any of the 7 projects (sources: nuget.org and the local SDK packages).
- `git diff --stat` and the csproj/props diff: 8 files changed, none of them a project or package file.

I read the code and judged the findings myself.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed change. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in path touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched. |
| SC-4 Authorization | NOT_APPLICABLE | No endpoint, page or antiforgery change. The report page change is inside `ReportBuilder`, behind the existing report policies. |
| SC-5 Read-only mode | PASS | Synchronization is still skipped in read-only mode (`SkippedReadOnly` unchanged). The new path adds no write; a skipped course writes nothing at all. |
| SC-6 No DB UI | NOT_APPLICABLE | — |
| SC-7 Key | PASS | Not touched. Tests use `SyntheticServiceAccountKey` and an in-memory `DictionarySecretStore`. |
| SC-8 Google | PASS | No scope change. Same impersonation. The new classification neither retries nor swallows a permission failure: `GuardAsync` is unchanged, and `Points` runs outside it on already returned data. |
| SC-9 Channel | NOT_APPLICABLE | — |
| SC-10 Hygiene | PASS | The exception message is `"A Google read failed: ValueOutOfRange."`, with `Diagnosis` null and no inner exception (`GoogleClassroomReader.Points`). `TheFailure_CarriesNoValueAndNoIdentifier` asserts that `ToString()` holds no value, item id, title, submission id or user id. The log test in `ValueOutOfRangeSyncTests` asserts that no log line holds the value texts, item id or title, course name, email, user id or submission id. |
| SC-11 Audit | NOT_APPLICABLE | No audited action introduced (sync audit unchanged). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` untouched. |
| SC-13 Outbound | PASS | No new destination. Tests reach Google only through `ScriptedHttpHandler` (TC-4). |

## 6. Authentication and Authorization

No change. No new or changed endpoint or page; the anonymous surface is unchanged.

## 7. Credentials, Key and Google Access

No change to key handling, scopes or impersonation. The `Points` conversion runs on deserialized response objects after `GuardAsync` returns. A `GoogleReadFailedException` thrown there propagates unchanged. Only `RunSynchronizationUseCase` calls `ReadCourseWorkAsync` and `ReadSubmissionsAsync`, at lines 507–508, both inside the new `catch`. So `ValueOutOfRange`, which has a null `Diagnosis`, cannot reach the run-failure path.

## 8. Sensitive Data Exposure

- Log 5135 holds the run id and the Google course id, bounded by `GoogleLogValue.Bounded`. This is the same shape as event `SyncCourseGone`. No grade, title or name is logged (INFO-2).
- `SynchronizationRunOutcome.CoursesValueOutOfRange` holds ids only and is never serialized to a response.
- Report output: the bounded percent changes only which scale label appears, and that label is the school's own text. No new data reaches a view or an export.

## 9. Input Validation

Google-returned points are now checked against the `decimal` range before conversion. Spec FR-001 / VR-001 says what happens: the course is skipped, not stored, and read again on the next run. NaN and ±infinity also make the cast overflow, so they are classified the same way (INFO-1). An in-range negative value still meets the DB check constraint and stops the run. That is known limitation L-1 of the approved spec, not a security finding.

## 10. API Security

NOT_APPLICABLE: no endpoint, route, body or error shape changed (API design v2 is a NOT_APPLICABLE record).

## 11. Persistence and Configuration

No migration, entity or configuration change. The skip happens before `ExecuteInTransactionAsync`, so no partial row is written, including the complete-read `updateTime`. `ck_*_non_negative` constraints are kept (OD-001 a). No generated `.db` or `.xlsx` file in the working tree.

## 12. Logging, Audit and Telemetry

- Event id 5135 is unique in `src/`. It is source-generated through `[LoggerMessage]` with structured placeholders only.
- No audit change.
- `docs/hooks/tool-usage.jsonl` is git-ignored (`.gitignore:66`).

## 13. Dependencies

No package or project reference added. The vulnerability scan is clean (see §4).

## 14. Security Test Coverage

| Property | Test |
|---|---|
| Failure carries no value or identifier | `GoogleClassroomReaderValueRangeTests.TheFailure_CarriesNoValueAndNoIdentifier` |
| Log line 5135 shape, no value or personal data in any log line | `ValueOutOfRangeSyncTests` (log test, lines ~192–226) |
| Skipped course writes nothing | `ValueOutOfRangeSkipTests`, `ValueOutOfRangeSyncTests` |
| Report percent bounded, no overflow | `ReportScaleBoundsTests`, `ScaleBoundsReportPageTests` |
| No live Google | `GoogleClassroomScript` routes the real reader over `ScriptedHttpHandler` with a synthetic key |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A teacher enters an absurd grade (≥ 7.9·10²⁸) to stop the school's sync | Only that course is skipped; the run completes | `ValueOutOfRangeSyncTests` (three courses, the second out of range, run Completed) | PASS |
| Such a value leaks into the logs or the run error | Not logged; `LastError` is null | Log test; `Assert.Null(run.LastError)` | PASS |
| A stored pair with points ≫ max overflows the report division and returns a 500 | Bounded before dividing | `ReportBuilder.Grade`; `ScaleBoundsReportPageTests` | PASS |

## 16. Repository Hygiene

`google_credentials.json` and `classroom_cache.db` are git-ignored (verified with `git check-ignore`; neither file opened). No secret-like, `.env`, `.db` or `.xlsx` file in `git status`. Untracked files are the Story's tests and artifacts only.

## 17. Deviations

None with security effect. The implementation verification recorded these accepted deviations:

- D-1: `Diagnosis` null.
- D-2: overflow detected by the cast.
- D-3: NaN/infinity.

All three were re-checked above.

## 18. Findings

- **INFO-1** (INPUT_VALIDATION). `GoogleClassroomReader.Points` also classifies NaN and ±infinity as `ValueOutOfRange`, because the cast overflows. The effect is benign: the course is skipped and nothing is logged. No correction needed.
- **INFO-2** (LOGGING, SC-10). Event 5135 logs the Google course id. This is consistent with existing events 5131 and `SyncCourseGone`; a course id is not personal data under current practice. No correction needed.
- **INFO-3** (OTHER). The summary of `Application/Ports/GoogleReadFailedException.cs` still names only `CourseGone` as having no diagnosis (verification N-1). It is a documentation mismatch only, in a file outside the change set. A candidate for a later cleanup.
- **INFO-4** (REPOSITORY_HYGIENE). Three modified src files have LF endings (verification N-4), and git will normalize them on commit. Stage only the Story's files at commit time.

## 19. Positive Controls

- The new failure type carries no Google data and has no inner exception.
- The per-course skip happens before the transaction, so a skipped course writes nothing.
- `GuardAsync` classification and the permission-failure handling are unchanged.
- The log event is source-generated with ids only.
- Tests are offline and use synthetic data.
- The report percent is bounded without arithmetic overflow.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 is RESOLVED (a).

## 21. Review Limitations

This is a code, configuration and test review, not a penetration test. Behaviour against live Google with a real out-of-range value was not exercised; it was only simulated through the scripted transport. Delegated commands (build, test, package scan, diff stat) were run by `cheap-worker`.

## 22. Verdict Rationale

- The build and the full suite are green.
- There are no Critical, Major or Minor findings.
- Every touched SC item (SC-5, SC-7, SC-8, SC-10, SC-13) is PASS with code and test evidence.
- No security Open Decision is open.

Verdict: **PASS** → HUMAN_PR_APPROVAL.
