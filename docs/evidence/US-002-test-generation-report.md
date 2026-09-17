---
artifact_type: test_generation_report
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-16T14:36:00Z
updated_at: 2026-09-16T14:36:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-002-test-strategy.md
    version: 1
  - path: docs/tests/US-002-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
supersedes: null
---

# US-002 Test Generation Report

## Overall result: PASS — red phase verified

78 new test methods (201 test cases) and 1 rewritten US-001 method cover AC-001 … AC-011 and the database
design. The solution builds with no error and no warning. Full suite: 359 cases,
161 pass, 198 fail. Every failure is missing production behaviour; no failure is
caused by the tests or the environment.

## Environment

| Check | Command | Result |
|---|---|---|
| .NET SDK | `dotnet --list-sdks` | `10.0.401` |
| Docker | `docker version --format '{{.Server.Version}}'` | `29.8.0` |
| PostgreSQL | Testcontainers `postgres:17-alpine` | started and removed by each run |
| Build | `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| Tests | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx` | see below |

## Files

### Created

| File | Purpose |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationRow.cs` | stored `installation` row |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationTestData.cs` | synthetic names, `*.example.test` domains, fake client IDs, unknown UUID, `uk` time format |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationHostExtensions.cs` | register over HTTP, read/insert rows, set status, forge a session without the `Owner` role |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationListTests.cs` | AC-001 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationRegistrationTests.cs` | AC-002 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationValidationTests.cs` | AC-003 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationUniquenessTests.cs` | AC-004 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationDetailTests.cs` | AC-005 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationRenameTests.cs` | AC-006 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationClientIdTests.cs` | AC-007 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationAuditTests.cs` | AC-008 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/InstallationAuthorizationTests.cs` | AC-009 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/InstallationAntiforgeryTests.cs` | AC-010 |
| `tests/ClassroomAgent.Tests/ControlPlane/Localization/InstallationTranslationTests.cs` | AC-011 |
| `tests/ClassroomAgent.Tests/ControlPlane/Persistence/InstallationSchemaTests.cs` | db-design §3 |

### Modified

| File | Change | Why |
|---|---|---|
| `tests/ClassroomAgent.Tests/ControlPlane/Persistence/MigrationTests.cs` | `InitialMigration_CreatesOnlyOwnerAndAudit` → `Migrations_CreateOwnerAuditEventAndInstallation_InOrder` | US-002 adds the approved `AddInstallation` migration (db-design §7); the old assertion "exactly one migration, two tables" would contradict it. The US-001 facts (its migration first, its tables and audit trigger) are still asserted |
| `tests/ClassroomAgent.Tests/TestInfrastructure/HostEndpoint.cs` | `SamplePath` puts a UUID in `{id}` | the US-001 antiforgery enumeration must reach the guid-constrained routes; with `sample` they would hit the 404 catch-all |
| `tests/ClassroomAgent.Tests/TestInfrastructure/PostgreSqlFixture.cs` | `DropDatabaseAsync` | environment fix, below |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ControlPlaneTestHost.cs` | the host that created a database drops it on disposal | environment fix, below |

No production file was changed. No package was added.

## Environment problem found and fixed

The first full runs failed 257 … 271 of 359 cases, including US-001 tests that
pass on their own (158 alone: 157 pass, the 1 failure being the intentionally
changed `MigrationTests`). About 180 failures were
`Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:<port>` (connection
refused).

Diagnosis:

- `docker events` showed the PostgreSQL container's log ending a few seconds into
  the run and the events stream itself stopping; the container exited with 255.
- The Docker Desktop VM log (`%LOCALAPPDATA%\Docker\log\vm\init.log`) shows the
  WSL VM rebooting (kernel boot messages) right after; the backend log shows the
  engine restart.
- Windows System log, event 2004 (Resource-Exhaustion-Detector), at the same
  minutes: "low virtual memory condition", with `vmmem` at up to 5.2 GB. The
  machine's commit limit is 16.0 GB (15.6 GB RAM + 0.4 GB page file), with ~2.7 GB
  free commit.
- Every test created a database and never dropped it; ~360 databases kept in the
  VM's page cache grew `vmmem` until Windows ran out of commit and stopped the VM.
  Limiting parallelism (`--max-threads 4` and `2`) did not help — the growth is
  cumulative, not concurrent.

Fix (test infrastructure only): each test's database is dropped
(`DROP DATABASE … WITH (FORCE)`) when the host that created it is disposed. A
restarted host over the same database is disposed first, so the drop happens
once nothing uses it. Result: 0 connection failures, full suite in 24 s
(previously 2 m 20 s with failures).

**Recommendation to the human, not required for PASS:** the page file is 400 MB.
A system-managed page file would give headroom for Docker on this machine.

## Execution evidence

Command:
`dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us002-red.trx`

Summary: total **359**, failed **198**, passed **161**, skipped **0**, 24.4 s.
Output: 0 × "Failed to connect".

### Existing tests (US-001, 158 cases)

- 157 pass.
- 1 fails as expected: `MigrationTests.Migrations_CreateOwnerAuditEventAndInstallation_InOrder`
  — "Expected `installation`, Actual `owner`" (the migration does not exist yet).

### New tests (201 cases)

| Class | Cases | Failed | Passed |
|---|---|---|---|
| `InstallationListTests` | 6 | 6 | 0 |
| `InstallationRegistrationTests` | 6 | 6 | 0 |
| `InstallationValidationTests` | 69 | 69 | 0 |
| `InstallationUniquenessTests` | 9 | 9 | 0 |
| `InstallationDetailTests` | 13 | 9 | 4 |
| `InstallationRenameTests` | 6 | 6 | 0 |
| `InstallationClientIdTests` | 4 | 4 | 0 |
| `InstallationAuditTests` | 5 | 5 | 0 |
| `InstallationAuthorizationTests` | 30 | 30 | 0 |
| `InstallationAntiforgeryTests` | 8 | 8 | 0 |
| `InstallationTranslationTests` | 23 | 23 | 0 |
| `InstallationSchemaTests` | 22 | 22 | 0 |

Failure causes (all expected — missing behaviour):

| Count | Message | Meaning |
|---|---|---|
| 115 | `Assert.Equal() Failure: Values differ` — Expected `OK`/`BadRequest`/`Found`, Actual `NotFound` | the `/installations…` routes do not exist (404 catch-all) |
| 45 | `42P01: relation "installation" does not exist` | the `AddInstallation` migration does not exist |
| 23 | `Translation key 'Installation.…' is missing for 'uk'` | the translation keys do not exist |
| 8 | Expected `23514`, Actual `42P01` (`InstallationSchemaTests`) | table and constraints missing |
| 4 | `Collections differ` — expected 5 endpoint patterns / 8 columns / ordering, actual empty | endpoints and table missing |
| 3 | `Assert.NotNull() Failure` (unique index definitions) | indexes missing |
| 1 | `Assert.Matches()` — home page has no `/installations` link | link missing |
| 1 | Expected `OK`, Actual `Found` (`CopyScript_IsServedAsStaticFile_WithoutHardCodedText`) | `/js/copy-identifier.js` does not exist; the unmatched file path reaches the fallback authorization challenge |

The 8 `BeforeSetup_RedirectsToSetup` failures (Expected `Found`, Actual
`NotFound`) are in the first row: before setup an unmatched path is answered by
the 404 catch-all, which the gate exempts; once the routes exist the gate applies.

### New tests passing before implementation

`InstallationDetailTests.NonUuidIdentifier_Returns404ErrorPage` (4 cases). They
pass because the US-001 404 catch-all already answers any unmatched path. This is
intended: the contract requires a non-UUID `{id}` to match no route
(`{id:guid}`), so these are guards that the implementation must not break (e.g. by
an unconstrained `{id}` route answering `400` or `302`). Marked `GUARD` in the
matrix.

### Unexpected failures

None.

## Untested Acceptance Criteria

None. Every AC maps to at least one `RED` test (matrix coverage summary).

Not automated (documented in the strategy §5): the browser actually writing to
the clipboard; English page rendering (no language switch before US-039).

## Open Decisions

None raised. OD-001 … OD-003 are resolved and asserted.

## Result envelope

```yaml
result:
  verdict: PASS
  stage: TEST_WRITING
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/tests/US-002-test-strategy.md
    - docs/tests/US-002-ac-test-matrix.md
    - docs/evidence/US-002-test-generation-report.md
  next_stage: IMPLEMENTATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Test infrastructure now drops each test database on disposal; without it the Docker WSL VM exhausted Windows commit memory (16 GB limit, 400 MB page file) and restarted mid-run."
    - "HostEndpoint.SamplePath and MigrationTests (US-001) modified to fit the approved US-002 routes and migration."
    - "NonUuidIdentifier_Returns404ErrorPage (4 cases) already passes via the US-001 catch-all; kept as a guard."
```
