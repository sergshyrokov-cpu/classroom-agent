---
artifact_type: implementation_report
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-17T08:37:14Z
updated_at: 2026-09-17T08:37:14Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-002-register-installation.md
    version: null
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/designs/database/US-002-entity-model.md
    version: 1
  - path: docs/tests/US-002-test-strategy.md
    version: 1
  - path: docs/tests/US-002-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 69
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-002 Implementation Report — Register an Installation

## 1. Summary

- **Capability.** The signed-in Owner sees the list of installations, registers
  an installation (name, domain, client ID; active; server-generated UUIDv4
  identifier), opens its detail page with a copyable identifier, corrects the
  name and changes the client ID. Domain and client ID are unique under
  concurrency (database unique indexes, lost race → `409`). Each completed
  change writes its audit row in the same transaction. Everything shown is
  translated (`uk`, `en`). The home page links to the list.
- **Status.** Every Acceptance Criterion AC-001 … AC-011 is implemented.
- **Validation.** `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 359 of 359
  pass, 0 skipped. `dotnet format --verify-no-changes`: exit 0.
- **Limitations.** None known that affect an AC. Non-blocking notes are in §7.
- **Security-sensitive.** Yes: new Owner-only endpoints, antiforgery-protected
  POSTs, audit rows, a new table with immutability triggers. No change to
  authentication, cookies, the fallback policy, the anonymous list or the
  antiforgery exemptions.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-002-register-installation.md` | — |
| Specification | `docs/specifications/US-002-spec.md` | 2 (APPROVED) |
| Open Decisions | `docs/decisions/US-002-open-decisions.md` | 2 (APPROVED; OD-001 … OD-003 resolved) |
| API design | `docs/designs/api/US-002-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-002-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-002-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-002-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-002-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-002-ac-test-matrix.md` | 1 |
| Requirements | `trebovaniya.md` | 69 |

`HUMAN_SPEC_APPROVAL` recorded 2026-09-16T13:45:30Z. No input is `SUPERSEDED`.
No `TODO`/`TBD`/`FIXME`/unresolved marker in the inputs.

## 3. Implemented Acceptance Criteria

All tests below live in `tests/ClassroomAgent.Tests/ControlPlane/` and pass.

| AC | Implementation (file → symbol) | Tests | Status |
|---|---|---|---|
| AC-001 | `InstallationsController.List`; `InstallationRegistry.ListAsync`; `Views/Installations/Index.cshtml`; `InstallationDisplay.StatusKey`, `.UtcTime`; `Views/Home/Index.cshtml` link | `Controllers.InstallationListTests` (6) | PASS |
| AC-002 | `InstallationsController.New`, `.Register`; `InstallationRegistry.RegisterAsync`; `Installation.Register`; `InstallationConfiguration`; migration `AddInstallation` | `Controllers.InstallationRegistrationTests` (6); `Persistence.InstallationSchemaTests.InstallationColumns_MatchDesign_NoKeyOrSecretColumn`; `Persistence.MigrationTests` (2) | PASS |
| AC-003 | `RegisterInstallationRequest`, `RenameInstallationRequest`, `ChangeInstallationClientIdRequest`; `InstallationNameAttribute`, `InstallationDomainAttribute`, `InstallationClientIdAttribute`; check constraints in `InstallationConfiguration` | `Controllers.InstallationValidationTests` (69 cases); `Persistence.InstallationSchemaTests` check-constraint tests | PASS |
| AC-004 | `InstallationRegistry.RegisterAsync` (pre-check + `23505` mapping + re-check), `.ChangeClientIdAsync`; unique indexes `uq_installation_domain`, `uq_installation_client_id`; `InstallationsController.Register`, `.ChangeClientId` (`409`) | `Controllers.InstallationUniquenessTests` (9, incl. two concurrent races); `Persistence.InstallationSchemaTests.UniqueIndexes_Exist`, `.DuplicateValue_ViolatesNamedUniqueConstraint` | PASS |
| AC-005 | `InstallationsController.Detail` (`{id:guid}`, `404`); `InstallationRegistry.GetAsync`; `Views/Installations/Detail.cshtml`; `wwwroot/js/copy-identifier.js`; triggers `trg_installation_immutable_columns`, `trg_installation_no_delete` | `Controllers.InstallationDetailTests` (13); `Persistence.InstallationSchemaTests` trigger tests | PASS |
| AC-006 | `InstallationsController.NameForm`, `.Rename`; `InstallationRegistry.RenameAsync`; `Installation.Rename`; `Views/Installations/Name.cshtml` | `Controllers.InstallationRenameTests` (6) | PASS |
| AC-007 | `InstallationsController.ClientIdForm`, `.ChangeClientId`; `InstallationRegistry.ChangeClientIdAsync`; `Installation.ChangeClientId`; `Views/Installations/ClientId.cshtml` | `Controllers.InstallationClientIdTests` (4) | PASS |
| AC-008 | `AuditEvent.InstallationCreated`, `.InstallationRenamed`, `.InstallationClientIdChanged`; `AuditAction`, `AuditTargetType`, `AuditEventConfiguration` codes; written in the registry transactions | `Controllers.InstallationAuditTests` (5) | PASS |
| AC-009 | `[Authorize(Policy = OwnerSession.OwnerPolicy)]` on `InstallationsController`; host setup gate and fallback policy unchanged | `Security.InstallationAuthorizationTests` (30); `Security.AnonymousEndpointTests` (US-001, guard) | PASS |
| AC-010 | all changes are POST; host `GlobalAntiforgeryFilter` unchanged; `@Html.AntiForgeryToken()` in the three forms | `Security.InstallationAntiforgeryTests` (8); `Security.AntiforgeryTests` (US-001, guard) | PASS |
| AC-011 | 41 new keys in `SharedResource.uk.resx` and `SharedResource.en.resx`; entered values rendered HTML-encoded, never translated | `Localization.InstallationTranslationTests` (23); `Localization.TranslationCompletenessTests` (US-001, guard) | PASS |

## 4. Change Set

Production code was written in this stage. The test files were written at
`TEST_WRITING` (see the test generation report) and were not changed here.

### Created

| File | Trace |
|---|---|
| `src/ClassroomAgent.ControlPlane/Persistence/Installation.cs` | entity model §2.1; FR-004, FR-006, FR-007, FR-012; S-03, S-05 |
| `src/ClassroomAgent.ControlPlane/Persistence/InstallationStatus.cs` | entity model §2.2; FR-012 |
| `src/ClassroomAgent.ControlPlane/Persistence/Configurations/InstallationConfiguration.cs` | db-design §3, §3.1, §3.2; entity model §3 |
| `src/ClassroomAgent.ControlPlane/Persistence/Migrations/20260917083016_AddInstallation.cs` | db-design §7 (table, constraints, indexes, both triggers and functions; Down drops them) — PC-2 supporting migration |
| `src/ClassroomAgent.ControlPlane/Persistence/Migrations/20260917083016_AddInstallation.Designer.cs` | generated with the migration (PC-2) |
| `src/ClassroomAgent.ControlPlane/Services/InstallationRegistry.cs` | entity model §4; FR-001, FR-003 … FR-009; db-design §5 |
| `src/ClassroomAgent.ControlPlane/Services/InstallationListItemDto.cs` | api-design §6 `InstallationListItem`; AD-8 |
| `src/ClassroomAgent.ControlPlane/Services/InstallationDetailDto.cs` | api-design §6 `InstallationDetail`; AD-8 |
| `src/ClassroomAgent.ControlPlane/Services/RegisterInstallationResult.cs` | entity model §4 `Registered` / `Conflict` |
| `src/ClassroomAgent.ControlPlane/Services/RenameInstallationResult.cs` | entity model §4 `Renamed` / `Unchanged` / `NotFound` |
| `src/ClassroomAgent.ControlPlane/Services/ChangeClientIdResult.cs` | entity model §4 `Changed` / `Unchanged` / `NotFound` / `ClientIdTaken` |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationsController.cs` | openapi paths `/installations`, `/installations/new`, `/installations/{id}`, `…/name`, `…/client-id`; FR-010, FR-011 |
| `src/ClassroomAgent.ControlPlane/Controllers/RegisterInstallationRequest.cs` | openapi `RegisterInstallationRequest`; VR-001 … VR-003 |
| `src/ClassroomAgent.ControlPlane/Controllers/RenameInstallationRequest.cs` | openapi `RenameInstallationRequest`; VR-001 |
| `src/ClassroomAgent.ControlPlane/Controllers/ChangeInstallationClientIdRequest.cs` | openapi `ChangeInstallationClientIdRequest`; VR-003 |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationNameAttribute.cs` | VR-001, OD-002; api-design §5 message keys and rule order |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationDomainAttribute.cs` | VR-002, OD-001; api-design §5 message keys and rule order |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationClientIdAttribute.cs` | VR-003, spec I-9 |
| `src/ClassroomAgent.ControlPlane/Controllers/RegisterInstallationPageModel.cs` | VR-004 (values refilled); pattern of US-001 `SetupPageModel` |
| `src/ClassroomAgent.ControlPlane/Controllers/RenameInstallationPageModel.cs` | FR-006 form; VR-004 |
| `src/ClassroomAgent.ControlPlane/Controllers/ChangeInstallationClientIdPageModel.cs` | FR-007 form; VR-004 |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationDisplay.cs` | FR-001 status label; OD-003 time display (openapi `datetime-display`) |
| `src/ClassroomAgent.ControlPlane/Views/Installations/Index.cshtml` | FR-001; AC-001 |
| `src/ClassroomAgent.ControlPlane/Views/Installations/New.cshtml` | FR-003; AC-002 |
| `src/ClassroomAgent.ControlPlane/Views/Installations/Detail.cshtml` | FR-005; AC-005 |
| `src/ClassroomAgent.ControlPlane/Views/Installations/Name.cshtml` | FR-006; AC-006 |
| `src/ClassroomAgent.ControlPlane/Views/Installations/ClientId.cshtml` | FR-007; AC-007 |
| `src/ClassroomAgent.ControlPlane/wwwroot/js/copy-identifier.js` | openapi `/js/copy-identifier.js`; FR-005, spec I-6 |
| `docs/evidence/US-002-implementation-report.md` | this report (`implementation_report`) |

### Modified

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.ControlPlane/Persistence/ControlPlaneDbContext.cs` | `Installations` set; applies `InstallationConfiguration` | entity model §2.5 |
| `src/ClassroomAgent.ControlPlane/Persistence/TimestampInterceptor.cs` | stamps `Installation` too | entity model §2.4; PC-6 |
| `src/ClassroomAgent.ControlPlane/Persistence/AuditAction.cs` | three installation actions | entity model §2.3; FR-009 |
| `src/ClassroomAgent.ControlPlane/Persistence/AuditTargetType.cs` | `Installation` target | entity model §2.3 |
| `src/ClassroomAgent.ControlPlane/Persistence/AuditEvent.cs` | factories `InstallationCreated`, `InstallationRenamed`, `InstallationClientIdChanged`; the private `OwnerActs` helper generalised to a target type and id (entity model §2.3 leaves that to the implementor); US-001 rows unchanged | entity model §2.3; FR-009 |
| `src/ClassroomAgent.ControlPlane/Persistence/Configurations/AuditEventConfiguration.cs` | codes `installation_created`, `installation_renamed`, `installation_client_id_changed`, `installation` | db-design §4 |
| `src/ClassroomAgent.ControlPlane/Persistence/Migrations/ControlPlaneDbContextModelSnapshot.cs` | regenerated by `dotnet ef migrations add` | PC-2 supporting change |
| `src/ClassroomAgent.ControlPlane/Program.cs` | `AddScoped<InstallationRegistry>()` | DI registration (supporting) |
| `src/ClassroomAgent.ControlPlane/Controllers/HomeController.cs` | doc comment only: the page now also links to the installations | FR-002 |
| `src/ClassroomAgent.ControlPlane/Views/Home/Index.cshtml` | link to `/installations` (`Home.Installations`) | FR-002; openapi `GET /` |
| `src/ClassroomAgent.ControlPlane/Localization/SharedResource.uk.resx` | 41 keys: the 21 contract keys plus page titles, labels and buttons | FR-013; NFR-073; openapi `ValidationMessageKeys`, `PageTextKeys` |
| `src/ClassroomAgent.ControlPlane/Localization/SharedResource.en.resx` | the same 41 keys in English | FR-013; NFR-073 |
| `src/ClassroomAgent.ControlPlane/wwwroot/css/site.css` | table, definition-list and note styles for the new pages | FR-001, FR-005 presentation (supporting) |

### Test files in the working tree (from `TEST_WRITING`, unchanged here)

The 12 test classes and 3 infrastructure files created at `TEST_WRITING`, and
the 4 modified US-001 test files (`MigrationTests.cs`, `HostEndpoint.cs`,
`PostgreSqlFixture.cs`, `ControlPlaneTestHost.cs`), are traced in
`docs/evidence/US-002-test-generation-report.md` and the `ac_test_matrix`.

### Not in the change set

No package added; no `.csproj` changed. No secret, database file, `.xlsx` or
IDE-local config. `TestResults/` (the trx file) is git-ignored. No Python
prototype file touched. No workflow state written.

## 5. Validation Evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Docker | `docker version --format '{{.Server.Version}}'` | 0 | `29.8.0` (after starting Docker Desktop, see §7) |
| Build | `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| Migration | `dotnet ef migrations add AddInstallation --project src/ClassroomAgent.ControlPlane --startup-project src/ClassroomAgent.ControlPlane --output-dir Persistence/Migrations` | 0 | `20260917083016_AddInstallation` generated; triggers added by hand as db-design §3.3 / §7 require |
| Tests | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us002-impl.trx` | 0 | total **359**, passed **359**, failed 0, skipped 0, 45.7 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes |

Covered by the 359: every new US-002 class (201 cases: list, registration,
validation, uniqueness incl. both concurrent races, detail, rename, client ID,
audit, authorization, antiforgery, translations, schema) and the full US-001
suite (158 cases), including model drift
(`MigrationTests.Model_HasNoPendingChangesAgainstMigrations`), the anonymous
endpoint enumeration, the host-wide antiforgery enumeration and translation
completeness.

**Baseline.** A run before any production change failed all 359 cases with
connection errors because Docker Desktop was not running. Docker Desktop was
started; the pre-implementation baseline recorded by `TEST_WRITING` stands (359
cases: 161 pass, 198 fail for missing behaviour). No pre-existing failure remains.

## 6. Configuration Changes

None. No `appsettings` key, environment variable or connection-string option
was added or changed. `Program.cs` only registers the new service.

## 7. Deviations and Discovered Problems

No deviation from the approved artifacts. Choices the artifacts leave to the
implementor, and observations:

1. **Existence before validation.** `POST …/name` and `POST …/client-id` look the
   installation up before reading `ModelState`, so an unknown UUID is `404` even
   with an invalid value (api-design §4 step 2 before step 3). This costs one
   extra read per submission.
2. **Rule order per field.** Each field reports only its first failing rule, in
   the order of api-design §5, via one custom attribute per field behind
   `[Required]`. `Required` uses `AllowEmptyStrings = true` (as US-001), so a
   whitespace-only name is reported as `EdgeWhitespace`, not `Required`.
3. **Lost race, all fields.** After a unique violation on registration, the
   service rolls back, clears the change tracker and re-queries both fields; if
   neither is taken any more it reports the field named by the violated index
   (db-design §5).
4. **Lost-race logging (non-blocking, for SECURITY_REVIEW).** On a lost race EF
   Core logs its failed save at `Error` (category `Microsoft.EntityFrameworkCore`,
   which passes the `Warning` override). The logged SQL carries parameter
   placeholders, not values (sensitive-data logging is off), and Npgsql redacts
   the PostgreSQL `DETAIL` (`Include Error Detail` is not set anywhere). No value
   reaches the log by this path, but no automated test drives a race and then
   reads the log file; `RejectedAndStoredValues_NeverReachTheLogFile` covers the
   pre-check conflict path.
5. **DTO status type.** `InstallationListItemDto` / `InstallationDetailDto` carry
   the `Persistence.InstallationStatus` enum, as entity model §4 and api-design
   §6 name it. It is a value enum, not an entity (AD-8 not affected).
6. **Additional translation keys.** Page titles, field labels, buttons and back
   links got their own keys (openapi `PageTextKeys` allows this), all in `uk` and
   `en`.
7. **Environment.** Docker Desktop was not running at the start of the stage; it
   was started from its standard install path. The test-writer's earlier
   recommendation (system-managed page file) still applies; the full run
   completed without connection failures.

## 8. Open Decisions

None touched, none newly required. OD-001 … OD-003 are implemented as resolved.

## Result envelope

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-002
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-002-implementation-report.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Lost-race registration path: EF Core logs the failed save at Error; values are not included (placeholders, redacted Npgsql detail) but no automated test reads the log after a race."
    - "POST …/name and …/client-id read the installation before validation (404 precedes 400), one extra query per submission."
    - "Docker Desktop had to be started at the beginning of the stage; the first test run failed on connections only."
```
