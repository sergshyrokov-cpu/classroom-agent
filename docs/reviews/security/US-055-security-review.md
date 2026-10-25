---
artifact_type: security_review
story: US-055
version: 1
status: APPROVED
created_at: 2026-10-25T13:50:00Z
updated_at: 2026-10-25T13:50:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-055-grade-columns-fit-any-value.md
    version: null
  - path: docs/decisions/US-055-open-decisions.md
    version: 1
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/designs/api/US-055-api-design.md
    version: 1
  - path: docs/designs/database/US-055-db-design.md
    version: 1
  - path: docs/designs/database/US-055-entity-model.md
    version: 1
  - path: docs/tests/US-055-test-strategy.md
    version: 1
  - path: docs/tests/US-055-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-055-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-055-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: false
runtime_checks: FULL
---

# US-055 Security Review

## 1. Executive Summary

**Result: PASS — 0 Critical, 0 Major, 0 Minor, 4 Informational.**

The Story changes the PostgreSQL type of three columns
(`course_work.max_points`, `submission.assigned_grade`,
`submission.draft_grade`) from `numeric(10,4)` to unconstrained `numeric`, via
two EF Core configuration lines, the regenerated model snapshot and migration
`20261025104345_WidenGradeColumns` (three `AlterColumn`, nothing else). No
endpoint, page, policy, role, Google call, log line, audit event, package or
configuration value is added or changed. The values are student data already
stored before the Story; the Story only lets values that used to overflow be
stored like any other.

Independent evidence: `dotnet build` 0 warnings / 0 errors; full
`dotnet test ClassroomAgent.sln` **6139/6139 green, 0 skipped** (this closes
IMPLEMENTATION_VERIFICATION N-2); `dotnet list package --vulnerable` reports no
vulnerable package in any project.

Recommended next action: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As in `inputs` (API design v1 is the `NOT_APPLICABLE` record; there is no
`openapi` by design). Specification v1 and Open Decisions v1 are `APPROVED`;
`HUMAN_SPEC_APPROVAL` is recorded in `docs/workflow/history.jsonl`. No input is
`SUPERSEDED`.

Change set reviewed (working tree against `0cd6ad5`):

| File | Change |
|---|---|
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseWorkConfiguration.cs` | `MaxPoints` → `numeric` |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs` | `AssignedGrade`, `DraftGrade` → `numeric` |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | three column types |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261025104345_WidenGradeColumns(.Designer).cs` | new migration |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` | migration count 20 → 21 |
| `tests/.../GradeColumnsPersistenceTests.cs`, `tests/.../LargeGradesSyncTests.cs`, `tests/.../LargeGradesPageTests.cs` | new story tests |
| `docs/...` | workflow artifacts and state |

## 3. Security-Relevant Scope

- **Exposed functionality:** none new. Journal, report and export pages
  (Dean/Admin, unchanged policies) now display values that previously could not
  be stored.
- **Asset:** grades and maximum points (student personal data, SC-10).
- **Trust boundaries touched:** Google → installation (the background sync
  writes Google-returned values); Application → PostgreSQL (column types).
- **Security components affected:** none (no `Security` namespace, policy,
  middleware, audit or logging change).

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0, WSL 3.0.3; Testcontainers PostgreSQL.
- Commands: `git status`, `git diff`; `dotnet build ClassroomAgent.sln`
  (0 warnings, 0 errors); `dotnet list ClassroomAgent.sln package --vulnerable`
  (none); `dotnet test ClassroomAgent.sln` (6139/6139, 11 min 41 s).
- The first full run was stopped by the host for low memory; the second failed
  all tests at the Testcontainers Docker ping (Docker Engine answered 500 after
  that memory event). After restarting Docker Desktop and `wsl --shutdown`, a
  probe class (`GradeColumnsPersistenceTests`, 7/7) and then the full suite ran
  green. Infrastructure only (INFO-4).
- Delegation: none — the change set is three source files; all reading and
  commands were done directly.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed change |
| SC-2 Authentication | NOT_APPLICABLE | untouched |
| SC-3 AllowedAdmin | NOT_APPLICABLE | untouched |
| SC-4 Authorization | PASS | no endpoint or page added (diff); spec S-01; anonymous-access and §2 matrix tests in the full suite green |
| SC-5 Read-only mode | PASS | no new write use case; sync's read-only gating unchanged |
| SC-6 No DB UI | NOT_APPLICABLE | untouched |
| SC-7 Key | NOT_APPLICABLE | untouched |
| SC-8 Google | PASS | no scope or Google call change; `GoogleClassroomReader` `double?`→`decimal?` cast untouched (spec I-1) |
| SC-9 Channel | NOT_APPLICABLE | untouched |
| SC-10 Hygiene | PASS | no logger call writes grade or points (grep over `src`); errors go through the unchanged `IExceptionHandler` |
| SC-11 Audit | PASS | no audited action introduced; run failure/success records unchanged (spec S-04) |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` / Control Plane untouched |
| SC-13 Outbound | PASS | no new destination |

## 6. Authentication and Authorization

No change. Pages showing the values keep their existing policies; the full
suite (allowed/forbidden-role tests from US-052, the anonymous-endpoint
enumeration) is green.

## 7. Credentials, Key and Google Access

No change. No credential, key reference or scope touched.

## 8. Sensitive Data Exposure

- Responses/views: values shown only where they were shown before (journal,
  report, export), now formatted for larger numbers; no new field.
- DTOs: unchanged (`decimal?` throughout).
- Logs / audit / exceptions: no grade or points value is logged or audited.
- Test fixtures: synthetic values (`12345678.5`, `99999999999999999999.123456`,
  `10.123456` …); the value that overflowed on the college domain is not
  reproduced (spec S-03, TC-4).
- Telemetry: untouched.

## 9. Input Validation

Values from Google: spec VR-001 deliberately adds and removes no validation
(§3: stored as Google returned them). Removing the column precision removes no
security control — the previous limit was a storage defect, not a validation
rule; the Domain still receives `decimal?`. Extreme values outside `decimal`
range are bounded by the pre-existing cast (INFO-1).

## 10. API Security

No endpoint added, removed or changed (API design `NOT_APPLICABLE`).

## 11. Persistence and Configuration

- Schema change only through the migration (PC-2); no `EnsureCreated`.
- `numeric` without precision is PostgreSQL's arbitrary-precision type; no
  table rewrite (db-design §3, measured). Nullability unchanged.
- No connection string or setting changed; no generated database or `.xlsx`
  in the change set.
- `Down` fails with 22003 once a value above 999 999.9999 is stored — by design,
  not run by deployment (INFO-3).

## 12. Logging, Audit and Telemetry

No logging or audit change; `docs/hooks/tool-usage.jsonl` remains git-ignored.

## 13. Dependencies

No `.csproj` or `Directory.*.props` change; no package or project reference
added. `dotnet list package --vulnerable`: no vulnerable packages in any of the
seven projects (nuget.org source).

## 14. Security Test Coverage

The Story has no security-sensitive AC. Spec S-01…S-04 are covered by: the
unchanged authorization suite (S-01), synthetic fixtures (S-03, inspected), and
the absence of logging/audit changes (S-04, diff + grep). Story tests 16/16 and
`AppUserMigrationTests` green within the full run.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A teacher in Google enters an enormous grade to break the sync | value stored or the course fails as a recorded failed run; other courses continue | `LargeGradesSyncTests.TheCoursesAfterTheUnusualOne_…`; values beyond `decimal` range → INFO-1 | PASS |
| Extreme points/max pair breaks the report page | generic error page, no internals | `ReportBuilder.Grade` division in `decimal`; overflow would reach the single `IExceptionHandler` (INFO-2) | PASS |
| Grades leak into logs when a value is unusual | no value in logs | no logger call with grade/points | PASS |

## 16. Repository Hygiene

`git status --ignored`: live credential files (`google_credentials.json`,
`dac-classroom-agent-*.json`), `classroom_cache.db`, the root `.xlsx` exports
and `.claude/settings.local.json` are all ignored (`!!`), none staged or
untracked. No secret-like content in the change set. Credential files were not
opened.

## 17. Deviations

None security-relevant. The two accepted functional deviations
(journal test draft on a second ungraded student; migration count 20 → 21) do
not touch security.

## 18. Findings

- **INFO-1** (GOOGLE_ACCESS / INPUT_VALIDATION) —
  `GoogleClassroomReader.cs` casts Google's `double` to `decimal`; a value above
  ≈ 7.9 × 10²⁸ (or NaN) throws `OverflowException` there. Pre-existing and out of
  scope (spec §10, I-1). Effect: that course's transaction fails and the run is
  recorded as failed (US-053); no data exposed. No correction required.
- **INFO-2** (ERROR_HANDLING) — `ReportBuilder.Grade` (`points / max * 100m`)
  could overflow `decimal` for an extreme points/max pair under a range scale
  (SPEC_REVIEW m-1); the exception reaches the single `IExceptionHandler`,
  which shows the generic error page without internals. Unrealistic values; no
  correction required.
- **INFO-3** (PERSISTENCE) — migration `Down` cannot narrow back once a wide
  value is stored (22003). Deployment never runs `Down`; rollback would be a
  restore from backup (DC). No correction required.
- **INFO-4** (OTHER) — the first full run was killed by host memory pressure
  and Docker Engine then answered 500; after a Docker/WSL restart the full suite
  ran green. Environment only; see the Owner's 16 GB memory note.

## 19. Positive Controls

- Change confined to Infrastructure persistence mapping and a migration; AD-3/AD-4
  boundaries unchanged.
- Schema changed only by an EF Core migration with a matching snapshot.
- No logging or audit of grade values.
- Synthetic test data only.
- Full suite green, including the authorization matrix and anonymous-endpoint
  tests.
- No vulnerable dependency.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No penetration testing; code, configuration and test review only.
- Vulnerability check limited to `dotnet list package --vulnerable` against
  nuget.org.
- INFO-1 behaviour beyond `decimal` range was reasoned from the cast, not run.

## 22. Verdict Rationale

The implementation report records a green build and green tests, confirmed
independently by a full green run. No Critical, Major or Minor finding; every
touched SC item is `PASS`; no blocking Open Decision. Verdict **PASS**.
