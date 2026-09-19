---
artifact_type: implementation_report
story: US-006
version: 2
status: DRAFT
created_at: 2026-09-19T09:35:00Z
updated_at: 2026-09-19T09:50:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-006-control-plane-push.md
    version: null
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/designs/database/US-006-db-design.md
    version: 1
  - path: docs/designs/database/US-006-entity-model.md
    version: 1
  - path: docs/tests/US-006-test-strategy.md
    version: 1
  - path: docs/tests/US-006-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-006-test-generation-report.md
    version: 2
  - path: trebovaniya.md
    version: 77
supersedes: docs/evidence/US-006-implementation-report.md@v1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-006 Implementation Report — Control Plane push on status change

## 1. Summary

Every Acceptance Criterion of the Story is implemented: the push address on
`Installation` with its validation, canonical storage, audit row, detail-page
warning and translations; the outbound push with its 10-second attempt timeout,
the 5 s / 30 s / 2 min retries, replacement of an unfinished push and its log
events; the push receiver on the installation's private port with the identifier
check, the `202` answer, the one-minute limit and the single pending check; and
the mandatory `Hosting:PrivateAddress` setting that binds the private endpoint.

Validation status: **build clean, format clean, 1085 of 1085 test cases green,
none skipped.**

v1 of this report returned `BLOCKED`: four cases contradicted an approved
artifact. On 2026-09-19 the human decided to correct the four cases rather than
the requirement, and they are corrected — section 7 records what changed and why.
No production behaviour was changed to make them pass.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-006-control-plane-push.md` | — |
| Specification | `docs/specifications/US-006-spec.md` | 2 (APPROVED) |
| Open Decisions | `docs/decisions/US-006-open-decisions.md` | 2 (APPROVED, OD-001 resolved) |
| API design | `docs/designs/api/US-006-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-006-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-006-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-006-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-006-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-006-ac-test-matrix.md` | 2 (four rows corrected, section 7.1) |
| Test generation report | `docs/evidence/US-006-test-generation-report.md` | 2 |
| Requirements | `trebovaniya.md` | 77 |

`HUMAN_SPEC_APPROVAL` recorded 2026-09-17T15:37:50Z. No input is `SUPERSEDED`;
no unresolved marker remains in any of them.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 | `InstallationSettingsReader.PrivateAddress`, `InstallationSettings.PrivateAddress`, `Web/Program.Main` (Kestrel URL) | `Web.Configuration.PrivateAddressConfigurationTests` | green (19/19) |
| AC-002 | `PushAddressRules`, `InstallationPushAddressAttribute`, `InstallationsController.PushAddressForm` / `.ChangePushAddress`, `InstallationRegistry.ChangePushAddressAsync`, `Installation.ChangePushAddress` | `ControlPlane.Controllers.InstallationPushAddressTests`, `…ValidationTests` | green (62/62) |
| AC-003 | `AuditAction.InstallationPushAddressChanged`, `AuditEvent.InstallationPushAddressChanged`, `AuditEventConfiguration` codes, one transaction in `ChangePushAddressAsync` | `ControlPlane.Controllers.InstallationPushAddressAuditTests` | green (8/8) |
| AC-004 | `Views/Installations/Detail.cshtml` push-address row, `InstallationDetailDto.PushAddress` | `ControlPlane.Controllers.InstallationPushAddressDetailTests` | green (7/7) |
| AC-005 | `InstallationStatusService.ChangeStatusAsync` (read in the transaction, hand-off after commit), `StatusPushDispatcher.Enqueue` | `ControlPlane.Push.StatusPushDeliveryTests` | green |
| AC-006 | `StatusPushClient`, `StatusPushDelivery`, `StatusPushDispatcher.DeliverAsync` | `ControlPlane.Push.StatusPushDeliveryTests`, `…StatusPushLoggingTests` | green (26/26) |
| AC-007 | `StatusPushDispatcher.Enqueue` (cancel-and-replace per installation) | `StatusPushDeliveryTests.NewerPush_ReplacesTheUnfinishedOne` | green |
| AC-008 | `StatusPushDispatcher.Dispose` (root cancellation, nothing persisted) | `StatusPushDeliveryTests.PendingRetries_DoNotOutliveTheHost` | green |
| AC-009 | `StatusPushEndpoint.ReceiveAsync`, `PushCheckCoordinator.Request`, `LegitimacyCheckBackgroundService.WaitForNextCheckAsync` | `Web.Security.StatusPushEndpointTests` | green (19/19) |
| AC-010 | `StatusPushEndpoint` identifier comparison → `404 unknown_installation` | `StatusPushEndpointTests.PushForAnotherInstallation_…` | green |
| AC-011 | `PushCheckCoordinator` (running flag, minute window, single pending flag) | `Web.BackgroundServices.PushCheckCoordinationTests` | green (9/9) |
| AC-012 | `StatusPushEndpoint` body size, content type and JSON validation | `StatusPushEndpointTests`, `Web.Logging.StatusPushLoggingTests` | green |
| AC-013 | `PrivateEndpoints.MapPrivateEndpoints` (POST, `AllowAnonymous`, `DisableAntiforgery`, `PrivatePortEndpointFilter`) | `Web.Security.InstallationEndpointTests`, `ControlPlane.Security.PushAddressSecurityTests` | green |
| AC-014 | no read-only guard on the receiver or the triggered check (BR-026 closed list) | `StatusPushEndpointTests.InReadOnlyMode_…` | green |
| AC-015 | 13 `Installation.PushAddress.*` keys in `SharedResource.uk.resx` and `.en.resx`, `Views/Installations/PushAddress.cshtml` | `ControlPlane.Localization.PushAddressTranslationTests` | green (15/15) |

## 4. Change Set

### Created — production

| File | Trace |
|---|---|
| `src/ClassroomAgent.Contracts/StatusPushRequest.cs` | FR-008, api-design §3 |
| `src/ClassroomAgent.ControlPlane/Controllers/PushAddressRules.cs` | VR-001, api-design §7 |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationPushAddressAttribute.cs` | VR-001 |
| `src/ClassroomAgent.ControlPlane/Controllers/ChangeInstallationPushAddressRequest.cs` | api-design §7 request models |
| `src/ClassroomAgent.ControlPlane/Controllers/ChangeInstallationPushAddressPageModel.cs` | api-design §7 |
| `src/ClassroomAgent.ControlPlane/Services/ChangePushAddressResult.cs` | entity model §3 |
| `src/ClassroomAgent.ControlPlane/Push/StatusPushAttemptResult.cs` | api-design §6 |
| `src/ClassroomAgent.ControlPlane/Push/StatusPushAttempt.cs` | api-design §6 |
| `src/ClassroomAgent.ControlPlane/Push/IStatusPushClient.cs` | entity model §4, TC-4 seam |
| `src/ClassroomAgent.ControlPlane/Push/StatusPushClient.cs` | FR-006, api-design §6 |
| `src/ClassroomAgent.ControlPlane/Push/StatusPushDelivery.cs` | FR-006, I-7, I-8, NFR-014 |
| `src/ClassroomAgent.ControlPlane/Push/StatusPushDispatcher.cs` | FR-005 … FR-007, FR-011 |
| `src/ClassroomAgent.ControlPlane/Views/Installations/PushAddress.cshtml` | FR-002, FR-013 |
| `src/ClassroomAgent.ControlPlane/Persistence/Migrations/20260919091628_AddInstallationPushAddress.cs` (+ `.Designer.cs`) | db-design §5, PC-2 |
| `src/ClassroomAgent.Web/BackgroundServices/PushCheckCoordinator.cs` | FR-010, api-design §5 |
| `src/ClassroomAgent.Web/BackgroundServices/PushCheckRequestResult.cs` | api-design §5 |
| `src/ClassroomAgent.Web/Security/StatusPushEndpoint.cs` | FR-009, VR-002, api-design §4 |

### Modified — production

| File | Change | Trace |
|---|---|---|
| `Contracts/ServiceChannel.cs` | `StatusPushPath` | api-design §3 |
| `ControlPlane/Persistence/Installation.cs` | `PushAddress`, `Register` parameter, `ChangePushAddress` | entity model §2.1 |
| `ControlPlane/Persistence/AuditAction.cs` | `InstallationPushAddressChanged` | entity model §2.2 |
| `ControlPlane/Persistence/AuditEvent.cs` | factory for the new action | entity model §2.2 |
| `ControlPlane/Persistence/Configurations/AuditEventConfiguration.cs` | code ↔ member mapping | db-design §3.3 |
| `ControlPlane/Persistence/Configurations/InstallationConfiguration.cs` | `push_address` property, `ck_installation_push_address_format` | db-design §3.1 |
| `ControlPlane/Persistence/Migrations/ControlPlaneDbContextModelSnapshot.cs` | regenerated by `dotnet ef` | PC-2 |
| `ControlPlane/Services/InstallationDetailDto.cs` | `PushAddress` | api-design §7 |
| `ControlPlane/Services/InstallationRegistry.cs` | address in `GetAsync` / `RegisterAsync`, new `ChangePushAddressAsync` | FR-002, FR-003, db-design §4.1, §4.2 |
| `ControlPlane/Services/InstallationStatusService.cs` | address read in the transaction, hand-off after commit, events 5015/5016 | FR-005, db-design §4.3 |
| `ControlPlane/Controllers/RegisterInstallationRequest.cs`, `RegisterInstallationPageModel.cs` | optional `PushAddress` | FR-002 |
| `ControlPlane/Controllers/InstallationsController.cs` | push-address GET/POST, canonical address at registration | api-design §7 |
| `ControlPlane/Views/Installations/New.cshtml`, `Detail.cshtml` | field, row, warning, link | FR-002, FR-004 |
| `ControlPlane/Localization/SharedResource.uk.resx`, `.en.resx` | 13 keys each | FR-013, AC-015 |
| `ControlPlane/Program.cs` | named `status-push` client (no redirects, no cookies, no client logging), `IStatusPushClient`, `StatusPushDispatcher` | api-design §6, SC-10 |
| `Web/Configuration/InstallationSettings.cs`, `InstallationSettingsReader.cs` | `Hosting:PrivateAddress` and VR-003 | FR-001 |
| `Web/Program.cs` | private Kestrel endpoint bound to the configured address | FR-001, S-03 |
| `Web/Security/PrivateEndpoints.cs` | receiver mapped, anonymous, antiforgery-exempt, private-port filter | FR-009, S-01, S-02 |
| `Web/Configuration/InstallationServices.cs` | `PushCheckCoordinator` singleton | FR-010 |
| `Web/BackgroundServices/LegitimacyCheckBackgroundService.cs` | push trigger, pending check, schedule reset, event 5114 | FR-010, api-design §5 |

### Modified — tests

Story-level test files come from `TEST_WRITING`. Five were changed — one for the
schema the Story adds, four on the human's decision of 2026-09-19 (section 7.1):

| File | Change | Why |
|---|---|---|
| `tests/…/ControlPlane/Persistence/InstallationSchemaTests.cs` | `push_address` added to the column enumeration and `ck_installation_push_address_format` to the constraint enumeration | A US-002 guard that enumerates the `installation` table exactly. The Story adds a column and a constraint, so the enumeration must list them (db-design §3.1, §7). `TEST_WRITING` updated the four other enumeration guards but missed this one; nothing was weakened — both entries are asserted with their exact type, length and nullability. |
| `tests/…/Web/BackgroundServices/PushCheckCoordinationTests.cs` | two cases now let a push start a check before pushing inside its minute; one renamed to `PushWithinAMinuteOfThePreviousPushCheck_LeavesOnePendingCheck` | the minute is counted from the previous **push-triggered** check (`trebovaniya.md` v77 §9; AC-011; I-11) — section 7.1 |
| `tests/…/Web/Logging/StatusPushLoggingTests.cs` | `DeferredPush_IsNotLogged_…` now expects one `StatusPushAccepted` for the push that started a check and none for the two deferred ones | same rule — section 7.1 |
| `tests/…/ControlPlane/Localization/PushAddressTranslationTests.cs` | `DetailPageInEnglish_…` switches the Owner account to English and signs in again instead of sending `Accept-Language` | the UI language comes only from the Owner's account (US-001 FR-019) — section 7.1 |

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | success — **0 errors, 0 warnings** (`TreatWarningsAsErrors`) |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no formatting change |
| `dotnet ef migrations add AddInstallationPushAddress …` | 0 | fifth Control Plane migration generated |
| `ClassroomAgent.Tests.exe -result-trx` (whole suite) | — | **Total 1085, Failed 0, Passed 1085, Skipped 0**, 48 s |

Environment: Docker Desktop 29.8.0, Testcontainers with PostgreSQL (TC-2); no
test called a live Google API or a live Control Plane (TC-4).

Comparison with the verified red-phase baseline of
`US-006-test-generation-report.md` v2: **176 failing cases → 0**. The 909 cases
that passed before still pass; no test is skipped, ignored or commented out.

## 6. Configuration Changes

| Host | Key | Change | Required by |
|---|---|---|---|
| Installation | `Hosting:PrivateAddress` | new, mandatory: an IPv4 or IPv6 literal (brackets optional) or exactly `*`; no default. An invalid or missing value refuses startup, naming the key and the rule, never the value. | spec FR-001, VR-003; api-design §8; `trebovaniya.md` v75, v76 |

No Control Plane setting was added: the timeout and the retry pauses are the
constants in `StatusPushDelivery` (api-design §8). No secret was added anywhere
(S-16).

## 7. Deviations and Discovered Problems

### 7.1 Four story-level cases corrected on the human's decision (2026-09-19)

v1 of this report held `IMPLEMENTATION` at `BLOCKED` because four cases
contradicted an approved artifact. The human decided to correct the cases; the
rule itself is unchanged, so `trebovaniya.md` stays at v77 and neither the Story
nor the Specification moved. `docs/tests/US-006-ac-test-matrix.md` is v2 with the
four rows rewritten.

**The one-minute window — three cases.** `trebovaniya.md` v77 §9 counts the
minute from the start of the previous check **started by a push**:

> проверка по push запускается не чаще раза в минуту … не раньше чем через минуту
> после **начала предыдущей проверки по push**

Story AC-011 says the same and specification I-11 spells it out — *"a scheduled
check does not start the minute"*. `PushCheckCoordinator.EligibleAt()` implements
exactly that and was not touched. The three cases assumed the startup (scheduled)
check starts the minute; each now lets a push start a check first, and only then
pushes inside that minute:

| Case | Correction |
|---|---|
| `PushCheckCoordinationTests.PushWithinAMinuteOfTheStartupCheck_LeavesOnePendingCheck` → `…PushWithinAMinuteOfThePreviousPushCheck_LeavesOnePendingCheck` | renamed to the rule it tests; a first push at +60 s starts a check, a second 10 s later is deferred and runs 60 s after the first push check started |
| `PushCheckCoordinationTests.ManyPushesWithinAMinute_LeaveOnlyOnePendingCheck` | the first push starts a check and the minute; the four that follow fall inside it and leave exactly one pending check — three calls in total |
| `Web.Logging.StatusPushLoggingTests.DeferredPush_IsNotLogged_AndThePendingCheckIsLoggedWhenItStarts` | one `StatusPushAccepted` for the push that started a check, none for the two deferred ones, one `PendingPushCheckStarted` |

As a side effect the three cases are now deterministic: under the old
arrangement their outcome also depended on whether the startup check happened to
be still running when the push arrived.

**The page language — one case.**
`ControlPlane.Localization.PushAddressTranslationTests.DetailPageInEnglish_UsesTheEnglishWarning_AndShowsTheAddressAsStored`
drove the language with `Accept-Language`, which the Control Plane ignores by
design: the UI language comes only from the signed-in Owner's account
(`OwnerAccountCultureProvider`, US-001 FR-019, NFR-073), and the pre-existing
green cases `AllowedAdminPages_AreUkrainianByDefault_EmailsUntranslated` and
`PageLanguageTests` assert precisely that. The case now sets
`owner.ui_language = 'en'` and opens the session again — the pattern
`PageLanguageTests` already uses — and additionally asserts `lang="en"` and the
absence of the Ukrainian warning.

### 7.2 `InstallationSchemaTests` extended

See section 4: the US-002 guard that enumerates the `installation` table exactly
had to gain the new column and the new check constraint. `TEST_WRITING` updated
the four other enumeration guards but missed this one. Nothing was weakened —
both entries are asserted with their exact type, length and nullability.

### 7.3 Race-prone case, and the same race removed from the implementation

- `ControlPlane.Push.StatusPushLoggingTests.ConnectionFailure_IsLoggedWithItsCategory_AndNoStatusCode`
  stops the host (`ReadLogEventsAsync`) as soon as the stub has *recorded* the
  attempt, which is before the sender has classified and logged it. It failed
  once under parallel load and has passed in every run since, including the two
  full-suite runs. Left as it is; a wait on the next attempt would make it
  deterministic.
- The retry schedule had the same shape of race in the implementation and it was
  removed there: `StatusPushDispatcher` derives each retry instant from the clock
  read **before** the attempt plus the attempt's known duration (zero, or exactly
  the 10-second timeout), so a clock advanced while an attempt is being
  classified can no longer postpone a retry.

### 7.4 The HTTP client factory logged the push address

`IHttpClientFactory` logs `Start processing HTTP request POST {Uri}` at
`Information`, which put the school's push address into the Control Plane log and
broke SC-10 / S-13. The named client is now registered with `RemoveAllLoggers()`,
which removes only that client's own logging; the sender's events 5011 … 5017
carry internal identifiers, attempt numbers, categories and status codes only.

## 8. Open Decisions

None open. OD-001 of `docs/decisions/US-006-open-decisions.md` stays RESOLVED and
its option 2 (one pending check) is what `PushCheckCoordinator` implements.

The two items v1 of this report raised were decided by the human on 2026-09-19:
both are test corrections, recorded in section 7.1, and neither changes
`trebovaniya.md`, the Story or the Specification.
