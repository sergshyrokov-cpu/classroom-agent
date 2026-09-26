---
artifact_type: implementation_report
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-26T13:12:00Z
updated_at: 2026-09-26T13:12:00Z
produced_by: dotnet-implementor
attempt: 1
inputs:
  - path: docs/stories/US-011-check-access.md
    version: null
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 2
  - path: docs/designs/api/US-011-api-design.md
    version: 1
  - path: docs/designs/api/US-011-openapi.yaml
    version: 1
  - path: docs/designs/database/US-011-db-design.md
    version: 1
  - path: docs/designs/database/US-011-entity-model.md
    version: 1
  - path: docs/tests/US-011-test-strategy.md
    version: 1
  - path: docs/tests/US-011-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-011-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-011 Implementation Report — Check access diagnostic

## 1. Summary

"Проверить доступ" is implemented end to end: the Admin-only page and its `POST` on
`/settings/access-check`, the eight-step check (six delegated-token requests, one per scope of
`GoogleDelegationScopes.All`, then the Classroom read and the Admin Reports read), the closed list of
step outcomes, the two refusals, the audit rows, the startup self-check, the 21 translation keys in
both languages, and the Google adapter that is the first real `IGoogleDataPort` implementation in the
system.

Status: **complete**. Every Acceptance Criterion of the Story is implemented and covered by at least
one passing test.

Validation: `dotnet build` 0 errors / 0 warnings; `dotnet test` **1976 passed, 0 failed, 0 skipped**;
`dotnet format --verify-no-changes` clean; `dotnet list package --vulnerable --include-transitive`
clean for all seven projects.

Limitations, all of them already recorded upstream and unchanged by implementation:

- the adapter's mapping of Google's answers is proven on synthetic answers in Google's documented
  formats; an answer it cannot classify is `GoogleUnavailable`, never a configuration diagnosis
  (spec FR-005, test strategy §7);
- the check cannot prove that the technical account sees every course, nor that it holds nothing
  beyond read access — `trebovaniya.md` §7 item 10 stays open (spec I-8);
- the forbidden-role case uses a synthetic Dean principal against the real policy, because no Dean can
  sign in until US-012 (carried US-010 finding F-1).

One deviation was necessary and is recorded in §7: three existing US-007 tests counted read-only
refusal log lines across the whole host and had to be scoped to the operation under test, because the
startup self-check now legitimately consults the same guard at every start.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-011-check-access.md` | — (human-authored) |
| Specification | `docs/specifications/US-011-spec.md` | 1 (APPROVED, gate recorded 2026-09-21T07:54:55Z) |
| Open Decisions | `docs/decisions/US-011-open-decisions.md` | 2 (OD-001…OD-006 all RESOLVED) |
| API design | `docs/designs/api/US-011-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-011-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-011-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-011-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-011-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-011-ac-test-matrix.md` | 1 |
| Test generation report | `docs/evidence/US-011-test-generation-report.md` | 1 |
| Requirements | `trebovaniya.md` | 79 |

No input is `SUPERSEDED`. `api_design` and `database_design` record `open_decisions` v1 while it is now
v2; v2 adds OD-006 (the TEST_WRITING skeleton) only and changes nothing either design consumed, as the
workflow state already notes.

## 3. Implemented Acceptance Criteria

| AC | Implementation (file → symbol) | Test (class → representative method) | Status |
|---|---|---|---|
| AC-001 Only an Admin can run the check | `Application/Authorization/InstallationPolicies.cs` → `RunAccessCheck`; `Web/Security/InstallationSecurityServices.cs` → policy registration; `Web/Controllers/AccessCheckController.cs` → `[Authorize(Policy = RunAccessCheck)]`, `Index`, `Run`; `Web/Security/SignInRoutes.cs` → `AccessCheck`; `Web/Views/AccessCheck/Index.cshtml`; `Web/Views/Home/Index.cshtml` → settings entry | `Web.Security.AccessCheckAuthorizationTests` → `ThePolicy_AdmitsAnAdmin`, `ThePolicy_RefusesADean`, `ARunWithoutTheAntiforgeryToken_IsRefused_AndNothingRuns`; `Web.Pages.AccessCheckPageTests` → `ThePage_ExplainsTheCheck_AndOffersTheRun`, `OpeningThePage_RunsNothing_AndRendersNoResult` | PASS |
| AC-002 Each of the six scopes is checked on its own | `Application/UseCases/AccessCheckSteps.cs` → `RunAsync` (loop over `GoogleDelegationScopes.All`); `Application/Ports/IGoogleAccessProbe.cs` → `RequestDelegatedTokenAsync`; `Infrastructure/Google/GoogleAccessProbe.cs` → `RequestDelegatedTokenAsync` (`Scopes = [scope]`, `User = technicalAccount`) | `Web.UseCases.AccessCheckRunTests` → `TheRun_RequestsExactlyTheSixScopes_OnePerRequest_InOrder`, `EveryDelegation_ImpersonatesTheStoredTechnicalAccount_NotTheAdmin`; `Infrastructure.Google.GoogleAccessProbeTests` → `ATokenRequest_IsForThatOneScope_ImpersonatingTheTechnicalAccount` | PASS |
| AC-003 One real read from each API | `Application/UseCases/AccessCheckSteps.cs` → local `ReadAsync`; `Infrastructure/Google/GoogleAccessProbe.cs` → `ReadCoursesAsync` (`PageSize = 1`), `ReadMeetActivityAsync` (`"all"`, `Meet`, `MaxResults = 1`) | `Web.UseCases.AccessCheckRunTests` → `WhenEverythingIsInPlace_TheRunAnswers200_WithTheVerdictAccessInPlace`, `TheReads_UseTheIssuedToken`, `TheIssuedToken_NeverReachesThePage`; `Infrastructure.Google.GoogleAccessProbeTests` → `TheClassroomRead_IsOneGetOfAtMostOneCourse_WithTheToken`, `TheReportsRead_IsOneGetOfAtMostOneMeetEvent_ForAllUsers` | PASS |
| AC-004 A failure says what is not configured | `Application/Models/AccessCheckStepOutcome.cs`; `Application/Models/AccessCheckResult.cs` → `Of`, `IsConfigurationFailure`; `Application/UseCases/AccessCheckSteps.cs` → `IsRunWide`, `NotAttempted`, `TimeLimit`; `Infrastructure/Google/GoogleAccessProbe.cs` → `ClassifyTokenError`, `ClassifyApiError`, `LoadKey`, `LogUnclassified`; `Web/Security/AccessCheckTextKeys.cs` → `Of(outcome)` | `Web.UseCases.AccessCheckFailureTests` → `AForgottenScope_IsNamed_AndOnlyItsStepFails`, `ARunWideCause_StopsTheRunAfterTheFirstCall`, `AnOwnerSideCause_RevealsNothingAboutTheKey`, `WhenGoogleNeverAnswers_TheRunEndsAtTheTimeLimit_AsInconclusive`; `Infrastructure.Google.GoogleAccessProbeTests` → the mapping and `KeyUnavailable` cases | PASS |
| AC-005 Without a saved connection there is nothing to check | `Application/UseCases/RunAccessCheckUseCase.cs` → step 2; `Application/Models/AccessCheckRefusal.cs`; `Web/Controllers/AccessCheckController.cs` → `409` + `MessageKey` | `Web.UseCases.AccessCheckRefusalTests` → `WithoutASavedConnection_TheRunIsRefused_AndCallsNothing`, `WithAConnectionForAnotherDomain_TheRunIsRefused_AndCallsNothing` | PASS |
| AC-006 In read-only mode no call reaches Google | `Application/UseCases/RunAccessCheckUseCase.cs` → step 1 (`IReadOnlyModeGuard.EnsureAllowedAsync` first, before port, repository or secret) | `Web.UseCases.AccessCheckRefusalTests` → `InReadOnlyMode_TheRunIsRefusedWith409_AndNoCallReachesGoogle` (×3 causes), `ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason`; `Web.Pages.AccessCheckPageTests` → `InReadOnlyMode_ThePageIsServed_WithTheButton_AndCallsNothing` | PASS |
| AC-007 Every run is audited without personal data | `Domain/Entities/AuditEvent.cs` → `AccessCheckRun`, `AccessCheckRefused`; `Domain/Enums/AuditAction.cs` → `AccessCheckRun`; `Domain/Enums/AuditRefusalCategory.cs` → `ConnectionNotUsable`; `Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` → the two amended check constraints and the code maps; migration `20260921091106_AddAccessCheckAudit` | `Web.UseCases.AccessCheckAuditTests` → `ACarriedOutRun_WritesOneSucceededRow_NamingTheConnection`, `ARunThatFindsProblems_IsStillAuditedAsSucceeded`, `TheAuditRows_CarryNoPersonalDataAndNoFindings`, `ARun_WritesNothingButTheAuditRow`; `Web.Persistence.AccessCheckAuditSchemaTests`; `Infrastructure.Persistence.AppUserMigrationTests` | PASS |
| AC-008 The self-check runs at every start and writes only to the log | `Application/UseCases/RunStartupSelfCheckUseCase.cs`; `Application/Models/StartupSelfCheckOutcome.cs`; `Web/BackgroundServices/StartupSelfCheckBackgroundService.cs`; `Web/Security/AccessCheckLog.cs` → `SelfCheckCompleted`, `SelfCheckSkipped`, `SelfCheckFailed` | `Web.BackgroundServices.StartupSelfCheckTests` → `WithAccessInPlace_TheSelfCheckRunsTheEightSteps_AndLogsInformation`, `InReadOnlyMode_TheSelfCheckIsSkipped_WithAWarning_AndCallsNothing`, `AFailingPort_DoesNotPreventTheStart`, `TheSelfCheck_WritesNothingToTheDatabase`, `TheLog_CarriesNoAddressDomainOrToken` | PASS |
| AC-009 Tests never reach Google | `Application/Ports/IGoogleAccessProbe.cs` (the substitution point); `Infrastructure/Google/GoogleAccessProbe.cs` → constructor takes the `HttpMessageHandler` | property of the whole suite: the fake port and the scripted transport are the only Google seams; no test registers the live adapter | PASS |
| AC-010 Every string is translated | `Application/Localization/SharedResource.uk.resx` and `.en.resx` (21 keys each); `Web/Security/AccessCheckTextKeys.cs` | `Web.Localization.AccessCheckTranslationTests` → `EveryKey_ExistsInBothLanguages`, `EveryKey_IsReallyTranslated`, `NoMessage_NamesAWorkspaceAdminRole`, `TheResult_RendersInEnglishForAnEnglishAccount` | PASS |

## 4. Change Set

Twenty-two modified files, eighteen new files, no deletion. Every file traces to the approved
artifacts.

### Created

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/Models/AccessCheckRefusal.cs` | spec FR-006 step 2, §8; api-design §2.4 (`NotConfigured`, `DomainMismatch`) |
| `src/ClassroomAgent.Application/Models/AccessCheckResult.cs` | spec FR-001 (the result and the overall verdict), FR-005 |
| `src/ClassroomAgent.Application/Models/AccessCheckStep.cs` | spec FR-001 (one entry per step), openapi `AccessCheckStep` |
| `src/ClassroomAgent.Application/Models/AccessCheckStepKind.cs` | openapi `AccessCheckStepKind`; spec FR-001 |
| `src/ClassroomAgent.Application/Models/AccessCheckVerdict.cs` | spec FR-001 (three verdicts), openapi `AccessCheckVerdict` |
| `src/ClassroomAgent.Application/Models/RunAccessCheckOutcome.cs` | spec FR-006 step 5 (an outcome, never an exception — AD-9) |
| `src/ClassroomAgent.Application/Models/StartupSelfCheckOutcome.cs` | spec FR-010 (ran / skipped read-only / skipped connection; OD-004) |
| `src/ClassroomAgent.Application/UseCases/AccessCheckSteps.cs` | spec FR-001…FR-003, I-2 (the 30-second bound), I-5 (run-wide causes stop early); one implementation shared by the run and the self-check |
| `src/ClassroomAgent.Application/UseCases/RunAccessCheckUseCase.cs` | spec FR-006 (all five steps in order), FR-008, FR-009 |
| `src/ClassroomAgent.Application/UseCases/RunStartupSelfCheckUseCase.cs` | spec FR-010, I-7 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20260921091106_AddAccessCheckAudit.cs` | db-design §7.1; spec FR-013; PC-2 (supporting change: the amending migration) |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20260921091106_AddAccessCheckAudit.Designer.cs` | generated with the migration above |
| `src/ClassroomAgent.Web/BackgroundServices/StartupSelfCheckBackgroundService.cs` | spec FR-010 (after the host starts, never delays or stops it) |
| `src/ClassroomAgent.Web/Controllers/AccessCheckController.cs` | openapi `GET`/`POST /settings/access-check`; spec FR-007, FR-011; api-design §3 |
| `src/ClassroomAgent.Web/Controllers/AccessCheckPageModel.cs` | openapi `AccessCheckPageModel`; AD-8 |
| `src/ClassroomAgent.Web/Security/AccessCheckLog.cs` | spec FR-014, FR-010; test strategy §5 (the event names `AccessSelfCheckCompleted` / `AccessSelfCheckSkipped`) |
| `src/ClassroomAgent.Web/Security/AccessCheckTextKeys.cs` | spec FR-012; test strategy §5 (the 21 keys) |
| `src/ClassroomAgent.Web/Views/AccessCheck/Index.cshtml` | spec FR-007; test strategy §5 (the markup ids, classes and data attributes) |

### Modified

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs` | spec FR-011, I-9; api-design §2.7 (`RunAccessCheck`) |
| `src/ClassroomAgent.Application/Localization/SharedResource.en.resx` | spec FR-012, AC-010 (supporting change: translation entries) |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx` | as above; the Ukrainian file is the default (NFR-073) |
| `src/ClassroomAgent.Application/Models/AccessCheckStepOutcome.cs` | spec FR-005 (the closed list); OD-006 skeleton filled in |
| `src/ClassroomAgent.Application/Models/DelegatedToken.cs` | spec FR-002, S-07 (`ToString` never prints the token); OD-006 skeleton filled in |
| `src/ClassroomAgent.Application/Models/DelegationAttempt.cs` | spec FR-002; OD-006 skeleton filled in |
| `src/ClassroomAgent.Application/Ports/IGoogleAccessProbe.cs` | spec FR-004 (the port; AD-4); OD-006 skeleton filled in |
| `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` | spec FR-008; db-design §3.2 (the two factories) |
| `src/ClassroomAgent.Domain/Enums/AuditAction.cs` | spec FR-008, FR-013; db-design §3.1 (`AccessCheckRun`) |
| `src/ClassroomAgent.Domain/Enums/AuditRefusalCategory.cs` | api-design §2.4; db-design §3.1 (`ConnectionNotUsable`) |
| `src/ClassroomAgent.Infrastructure/ClassroomAgent.Infrastructure.csproj` | spec FR-015, OD-001 (the three Google packages, Infrastructure only) |
| `src/ClassroomAgent.Infrastructure/Google/GoogleAccessProbe.cs` | spec FR-002…FR-005, FR-016, VR-004; OD-006 skeleton filled in |
| `src/ClassroomAgent.Infrastructure/Google/GoogleServiceAccountSettings.cs` | spec FR-016; OD-006 skeleton filled in |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` | db-design §3.1, §7.1 (the two amended check constraints and the code maps) |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | generated by the migration |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | spec FR-015 (supporting change: DI registration of the port, the two use cases and the hosted service) |
| `src/ClassroomAgent.Web/Configuration/InstallationSettings.cs` | spec FR-016 (the optional key reference; I-1) |
| `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs` | spec FR-016, VR-004; test strategy §5 (`Google:ServiceAccountKeyReference`) |
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | spec FR-011 (supporting change: the policy registration) |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | openapi path `/settings/access-check` |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | spec FR-007; api-design §7 (the settings section gains its third entry) |
| `tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyRefusalLoggingTests.cs` | spec FR-010, I-7 — see the deviation in §7 |

No secret, no service-account key, no generated database file, no generated `.xlsx` and no IDE-local
configuration is in the change set. No file outside the Story's scope was touched, and nothing was
refactored opportunistically.

## 5. Validation Evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | 0 | Сборка успешно завершена — **0 errors, 0 warnings** (`TreatWarningsAsErrors`) |
| Full suite | `dotnet test ClassroomAgent.sln` | 0 | **итог 1976 · успешно 1976 · сбой 0 · пропущено 0** (2 m 33 s) |
| Targeted | `dotnet test --filter FullyQualifiedName~ReadOnlyRefusalLoggingTests` | 0 | 5 / 5 passed (after the §7 change) |
| Format | `dotnet format --verify-no-changes` | 0 | no change required |
| Dependencies | `dotnet list package --vulnerable --include-transitive` | 0 | no vulnerable package in any of the seven projects (spec S-13) |

Integration tests ran against real PostgreSQL through Testcontainers (TC-2); the Docker daemon had to
be started first (server 29.8.0). The EF Core InMemory provider is used nowhere.

Baseline before the production work was adopted: `dotnet build` was already clean and the suite stood
at **1976 total, 1973 passed, 3 failed** — the three failures are the ones §7 describes, all in
`ReadOnlyRefusalLoggingTests`, none of them a US-011 test. All other US-011 tests that
`test_generation_report` recorded as RED (183 failing cases) were already green in the adopted work and
stayed green.

## 6. Configuration Changes

| Change | Required by |
|---|---|
| New optional installation setting `Google:ServiceAccountKeyReference` — the **secret-store reference** of the service-account key, trimmed, blank counted as absent, resolved per check through `ISecretStore`. Absent or unusable → the outcome `KeyUnavailable`; it is **not** a start-up requirement, so viewing and export keep working without it. | spec FR-016, VR-004, I-1; test strategy §5 |
| `GoogleServiceAccountSettings` registered as a singleton carrying only the reference; the adapter registered with a `SocketsHttpHandler` that follows no redirect and keeps no cookie. | spec FR-004, FR-015, FR-016; SC-13 |
| `RunAccessCheckUseCase` and `RunStartupSelfCheckUseCase` registered scoped; `StartupSelfCheckBackgroundService` registered as a hosted service next to `LegitimacyCheckBackgroundService`. | spec FR-010, FR-015 |
| The `RunAccessCheck` authorization policy registered in the installation's security configuration. | spec FR-011 |

No repository configuration file holds a key, a reference or any school-specific value; the reference
itself is set per deployment (DC-3, SC-7). No `appsettings.json` exists in the installation host, so no
committed configuration file changed.

## 7. Deviations and Discovered Problems

**Three existing US-007 tests had to be scoped to the operation under test.**
`tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyRefusalLoggingTests.cs` counted
`ReadOnlyWriteRefused` log events **across the whole host log**:
`ARefusal_WritesOneWarningLine_NamingTheOperationAndTheReason` and `TheBackstopRefusal_IsLoggedAsWell`
asserted a single line, `RepeatedRefusals_DoNotRelogTheModeChange` asserted exactly two.

US-011 spec FR-010 and I-7 require the startup self-check to decide read-only mode **through the same
guard as every use case**, at every start. In a read-only host the guard therefore logs one refusal of
its own (`Operation: AccessCheck.StartupSelfCheck`), and the three assertions saw an extra line. The
production behaviour is what the approved Specification asks for; the assertions were over-broad for a
host in which an operation also runs on its own.

The fix keeps every assertion and adds precision instead of removing it: the private helper
`RefusalsAsync` now takes the operation name and filters on it, so each test counts the refusals of the
operation it performs (`SyntheticWriteUseCase.Operation`, `ReadOnlyModeUnitOfWork.Operation`). What
US-007 AC-010 states — a refused write writes exactly one `Warning` line naming its operation and the
reason category — is asserted exactly as before. The incidental property the old count also carried,
that no other component logs a refusal in the same host, was never the test's subject and is not
required by any artifact.

This is an **expected change to an existing test, traced to US-011 spec FR-010**, in the same category
as the anticipated `AppUserMigrationTests` change (db-design §8) — with the difference that
`test_strategy` §5 did not foresee it. It is recorded here so `SECURITY_REVIEW` and
`HUMAN_PR_APPROVAL` see it rather than discover it.

**Observation, no change made.** The self-check runs in every installation host, including the hosts
other Stories' tests start. It sends nothing when the key reference is absent (spec FR-016, VR-004), and
no test configures one, which is why no test reaches a live Google endpoint (TC-4, AC-009). The
property holds by the adapter's own precondition, not by luck, but it is worth stating: a future test
host that sets a real reference and seeds a usable connection would make a real call at start.

No conflict was found between the approved artifacts and the repository, and no loop-back is needed.

## 8. Open Decisions

No new Open Decision. OD-001…OD-006 are all RESOLVED (`docs/decisions/US-011-open-decisions.md` v2)
and were implemented as resolved:

- **OD-001** — the three Google client libraries are referenced by `ClassroomAgent.Infrastructure`
  only, on the 1.76 line: `Google.Apis.Auth` **1.76.0**, `Google.Apis.Classroom.v1` **1.76.0.4254**,
  `Google.Apis.Admin.Reports.reports_v1` **1.76.0.4252** (spec FR-015 records the exact versions here).
- **OD-002** — one token request per scope plus one minimal read per API.
- **OD-003** — nothing about a result is stored: no table, no column, no TempData.
- **OD-004** — a skipped self-check logs one `Warning` line naming the reason.
- **OD-005** — nothing limits how often the check runs.
- **OD-006** — the six compile-only skeleton types are now implemented and registered; no
  `NotImplementedException` remains in the changed code.

`trebovaniya.md` §7 item 10 (the technical account's minimum Workspace roles) stays open and untouched,
as spec I-8 requires.

No `TODO`, `TBD`, `FIXME` or `???` remains in the changed code or in this Story's artifacts.

---

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-011
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-011-implementation-report.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Three existing US-007 tests in ReadOnlyRefusalLoggingTests were scoped to the operation under test, because US-011 spec FR-010/I-7 makes the startup self-check consult the read-only guard at every start, which adds a legitimate refusal line to the host log. Assertions were preserved and sharpened, not weakened; test_strategy §5 did not foresee this change (§7)."
    - "The self-check runs in every installation host, including other Stories' test hosts. It sends nothing while no key reference is configured (spec FR-016, VR-004), which is why no test reaches Google; a future test host that sets a real reference and seeds a usable connection would make a real call at start (§7)."
    - "Google packages pinned: Google.Apis.Auth 1.76.0, Google.Apis.Classroom.v1 1.76.0.4254, Google.Apis.Admin.Reports.reports_v1 1.76.0.4252 — Infrastructure only; dotnet list package --vulnerable clean for all seven projects (spec FR-015, S-13)."
    - "api_design and database_design record open_decisions v1 while it is now v2; v2 adds OD-006 only and changes nothing either design consumed."
    - "Integration tests need the Docker daemon; it was not running and had to be started before the suite could run (TC-2)."
```
