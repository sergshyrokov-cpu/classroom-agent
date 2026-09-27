---
artifact_type: security_review
story: US-013
version: 2
status: APPROVED
created_at: 2026-09-27T15:47:37Z
updated_at: 2026-09-27T15:58:06Z
produced_by: security-reviewer
inputs:
  - path: docs/evidence/US-013-implementation-report.md
    version: 2
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/designs/api/US-013-api-design.md
    version: 1
  - path: docs/designs/database/US-013-db-design.md
    version: 1
  - path: docs/designs/database/US-013-entity-model.md
    version: 1
  - path: docs/tests/US-013-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-013-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 5
security_sensitive: true
runtime_checks: PARTIAL
---

# US-013 Security Review — Background synchronization service

## 0. Second pass — what changed and what was re-checked

**Verdict of this version: PASS.** 0 Critical, 0 Major, 0 Minor, 5 Informational.

This document keeps the record of both passes: version 1 found F-1 (Major, SC-7)
and F-2 (Minor); version 2 closes both. Sections 3 to 17 still describe the Story
accurately and were re-read rather than rewritten.

**F-1 CLOSED.** `services.AddSingleton(settings)` is gone. A narrow record
`Web/Configuration/SyncScheduleSettings.cs` — `record SyncScheduleSettings(TimeSpan
Interval)` — is registered in its place, built in `AddInstallation` the way
`SchoolDefaults` and `GoogleServiceAccountSettings` are, and
`SynchronizationBackgroundService` takes it. Verified independently across the
**working tree**: `InstallationSettings` now appears only as an explicitly passed
parameter (`Program.cs:39`, `InstallationSecurityServices.cs:26`,
`AddInstallation`'s own signature) and in the reader that builds it — it is
registered nowhere and resolved nowhere, so the resolved OAuth client secret and
the connection string are once again unreachable from the container. The narrow
record carries one `TimeSpan` and nothing else.
`SyncIntervalConfigurationTests` resolves `SyncScheduleSettings` and still asserts
the effective interval: 11 of 11 green, no assertion weakened.

**F-2 CLOSED.** `DefaultSyncInterval`, `MinimumSyncIntervalMinutes` and
`MaximumSyncIntervalMinutes` are named constants beside `SyncIntervalKey`, and the
parsing method uses them.

**A verification defect of version 1, corrected here.** Version 1 supported two
claims — that no `CS9113` pragma and no `NotImplementedException` remained — with
`git grep`, which searches **tracked files only**. Nearly every file of this Story
is still untracked, so those greps were vacuous. Re-run against the working tree
with ripgrep over `src/`: **no match for `CS9113`, `AddSingleton(settings)` or
`NotImplementedException`**. The conclusions hold; the evidence behind them did
not, and that is recorded rather than quietly replaced.

**Re-checked because the fix touched wiring:** the host still starts and serves
(readiness tests 10 of 10 green, so nothing else depended on resolving
`InstallationSettings` from the container); the narrow record is in `Web`, so no
layering rule moved (AD-3); no secret reaches a log line; and the whole suite was
green after the fix — 2253 passed, 0 failed, 0 skipped, build 0 warnings,
`dotnet format` clean.

## 1. Executive Summary (version 1, kept for the record)

**Verdict of version 1: CHANGES_REQUIRED, loop back to IMPLEMENTATION.** One Major
finding.

The Story's own security properties hold and are proved by tests: in read-only
mode no run starts, nothing is written and no Google call is possible; a skipped
run leaves no row; no audit member was added because a scheduled run writes none;
the new table holds no personal data; no endpoint, page or policy was added; and
the log lines carry categories and identifiers only. The service-account key and
the Google scopes are untouched.

The Major finding is a **supporting change, not the Story's own behaviour**:
registering the whole `InstallationSettings` record in the container makes the
**resolved OAuth client secret and the database connection string injectable
anywhere in the Web host**, including presentation code. SC-7 requires the client
secret to stay out of the UI, and the codebase's own pattern — `SchoolDefaults`,
`GoogleServiceAccountSettings` — is a narrow settings record per consumer. The
Story needed one `TimeSpan`, and got a container entry that carries two secrets.

No Critical finding. No suspected credential exposure. No blocking Open Decision.

Recommended next action: replace the broad registration with a narrow record, then
re-run this review.

## 2. Reviewed Artifacts

As recorded in `inputs`: implementation report v1, specification v1 (APPROVED),
api design v1 (NOT_APPLICABLE), db design v1, entity model v1, ac test matrix v1,
open decisions v2, `trebovaniya.md` v79. None is `SUPERSEDED`;
`HUMAN_SPEC_APPROVAL` is recorded in `workflow-state.yaml`.

## 3. Security-Relevant Scope

**Exposed functionality: none added.** No endpoint, no Razor page, no
authorization policy, no route (`InstallationServices.cs`, no `Map*` call;
confirmed against `PrivateEndpoints.cs`, unchanged). The one externally observable
change is *which state* the existing private-port readiness endpoint reports.

Assets touched: the `sync_state` row (no personal data), the installation's
configuration (including two secrets — see F-1), the log file, and the existing
readiness signal. Assets deliberately **not** touched: password hashes, the
service-account key, `AllowedAdmin`, session cookies, `WorkspaceConnection`
contents, audit rows.

Trust boundaries crossed: background service → Application use case → PostgreSQL.
No boundary to Google (the pipeline is empty), none to the Control Plane, none
from a browser.

## 4. Environment and Tools

- .NET 10 solution; Docker running, so Testcontainers integration tests execute
  (TC-2).
- Commands run for this review: `dotnet build ClassroomAgent.sln`
  (0 errors, 0 warnings), `dotnet test ClassroomAgent.sln`
  (2253 passed, 0 failed, 0 skipped), `dotnet list package --vulnerable`
  (**no vulnerable packages in any of the six production projects or the test
  project**), `git diff --stat -- *.csproj` (**no dependency change**),
  targeted `git grep` for audit writes, Google ports, pragmas and secrets.
- Not performed: penetration testing, a live host driven by hand, and any call to
  a real Google API or Control Plane (prohibited).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or permission cell changed |
| SC-2 Authentication | NOT_APPLICABLE | no credential, sign-in path or cookie touched |
| SC-3 AllowedAdmin | NOT_APPLICABLE | no Admin login path touched |
| SC-4 Authorization | PASS | no endpoint or page added; `InstallationEndpointTests` and the anonymous-endpoint enumeration are green unchanged; readiness stays behind the private-port filter |
| SC-5 Read-only mode | PASS | `RunSynchronizationUseCase.ExecuteAsync` calls `IReadOnlyModeGuard.EnsureAllowedAsync` as its **first** statement; `SynchronizationRefusalTests.InReadOnlyMode_NothingIsWritten` asserts zero rows, zero commits, zero transactions; `…_TheGuardIsAskedFirst_AndNothingElseIsRead` asserts zero reads of the row and the connection; `SyncScheduleTests.InReadOnlyMode_…` asserts `sync_state` stays empty in a real host for all three BR-025 causes |
| SC-6 No DB UI | PASS | nothing added; the new table is reachable only through `ISyncStateRepository` |
| SC-7 Key / secrets | **PASS in v2** (FINDING F-1 in v1, now closed — §0) | the service-account key is untouched (`GoogleServiceAccountSettings` unchanged, no key read on this path); the whole-settings registration that widened access to the resolved OAuth client secret and the connection string was replaced by a narrow `SyncScheduleSettings` record, verified across the working tree |
| SC-8 Google | PASS | `git grep` finds no Google port in the run path; the pipeline is empty (OD-001); no scope, token or impersonation code changed |
| SC-9 Channel | PASS | no `Contracts` change, no service-channel change; `ContractVersion` untouched; the private port and its filter unchanged |
| SC-10 Hygiene | PASS | the stored failure is `"RunFailed:" + exception.GetType().Name` — a type name, never a message or a Google object; log messages carry a `Guid`, an `int`, and a category string built from an enum; `SyncLoggingTests.TheLog_CarriesNoPersonalDataAndNoConnectionString` asserts the technical account, the school domain and the connection string are absent from the whole log directory |
| SC-11 Audit | PASS | a scheduled run writes no audit row — `SyncLoggingTests.AScheduledRun_WritesNoAuditRow`; `git grep` finds no `AuditEvent` in the new code; the audit enums and their check constraints are unchanged, as the design intended (US-019 owns the manual start) |
| SC-12 Owner | NOT_APPLICABLE | no Control Plane or `Contracts` change; no school data leaves the installation |
| SC-13 Outbound | PASS | the only destinations remain PostgreSQL and, through the unchanged legitimacy service, the Control Plane |

## 6. Authentication and Authorization

Nothing to authenticate: a run has no principal, which is why
`trebovaniya.md` §5 gives background actions the `system` actor and why this Story
writes no audit row at all. No policy was added or changed; the deny-by-default
fallback and the SC-4 anonymous list are untouched, and the existing endpoint
enumeration test proves it by still passing.

The readiness endpoint's access rule is unchanged — private port only, enforced by
the existing filter. `SyncReadinessTests.Readiness_IsNotReachableOnThePublicPort`
independently confirms it (this test passed **before** the implementation, which is
why it is a guard rather than new coverage).

## 7. Credentials, Key and Google Access

- **No password handling.** Nothing in the change set reads or writes a hash.
- **The service-account key is not touched.** The run path holds no Google port
  (verified by `git grep` against `RunSynchronizationUseCase`), so no credential is
  requested, and `GoogleServiceAccountSettings` is unchanged.
- **Scopes and impersonation unchanged**; US-011 remains their only owner.
- **But the OAuth client secret's reachability changed** — F-1. It is still
  resolved once at start-up from the secret store, still absent from the database,
  the repository and every log line; what changed is that any component in the Web
  host can now ask the container for the record that holds it.

## 8. Sensitive Data Exposure

- **Responses and views:** none added; no DTO, no view model, no serialization of
  the new entity (AD-8 satisfied trivially).
- **The new table:** `sync_state` holds a status code, a `Guid`, three instants, an
  `int` and a bounded diagnosis string. No email, no name, no grade, no Google
  identifier. `ck_sync_state_error_length` bounds the diagnosis at 512 characters,
  and the domain truncates rather than throws, so a verbose diagnosis cannot be
  stored whole.
- **Logs:** reviewed line by line (five `LoggerMessage` methods). The most
  exposure-prone is `SyncRunFailed`, whose `{Diagnosis}` is
  `"RunFailed:" + type name`. An exception **message** never reaches it. Verified
  by test across the whole log directory.
- **Audit rows:** none written.
- **Exports, telemetry:** untouched.
- **Test fixtures:** synthetic throughout; no fixture reaches a live service.

## 9. Input Validation

The Story accepts **no external request input** — it has no endpoint (spec
VR-005). Its only external input is the configuration value
`Sync:IntervalMinutes`, validated at start-up: `int.TryParse` with
`NumberStyles.None` and the invariant culture, bounds 1…1440, and
`InstallationSettingException.Invalid(SyncIntervalKey, "expected an integer from
1 to 1440")` otherwise. **The rejected value is not in the message**, which SC-10
requires; `SyncIntervalConfigurationTests.InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue`
asserts the key is named and the value is not, over seven rejected inputs,
including `" 5 "`, `"+5"` and `"1.5"`.

Data returned by Google is not validated here because none is read (OD-001); the
obligation passes to US-014 with its first step.

## 10. API Security

No endpoint exists to review. The api-design recorded `NOT_APPLICABLE` with four
citations from the approved Specification, and the implementation matches: no
route was mapped, no policy declared, no contract version changed. The one
behavioural change inside an unchanged contract — readiness reporting `Unhealthy`
when the service is down — is required by DC-11 and was unimplementable before
this Story.

`SyncReadinessTests.Readiness_AnswersTheStateOnly` asserts the body is one of the
three state words and, specifically, that it does not name the synchronization
service: the endpoint reveals *that* the installation cannot serve users, not
*which* component failed.

## 11. Persistence and Configuration

- One migration, `20260927143648_AddSyncState`, creating the table with
  `pk_sync_state`, `uq_sync_state_singleton` and all six check constraints of
  db-design §3.2; `Down` drops it. No `EnsureCreated()` anywhere (PC-2).
- No existing table altered; the Control Plane schema untouched
  (`AppUserMigrationTests.TheControlPlaneSchema_IsUnchangedByThisStory` still
  green).
- `SyncStateSchemaTests` (11 tests) proves each constraint **at the database**,
  including all three disagreements `ck_sync_state_terminal_fields` forbids — the
  defence in depth the design asked for.
- Database separation (AD-1) unchanged; no connection string committed.
- Generated database files and `.xlsx` exports remain git-ignored
  (`.gitignore` lines 12–15, including the deliberate exception for the versioned
  report templates).
- Configuration: one new optional key. See §9 for its validation and F-1 for the
  registration problem.

## 12. Logging, Audit and Telemetry

Five log events, all with distinct `EventId`s in a block no other service uses
(5121–5125). Levels match DC-10 and spec I-4 / I-7: start, completion and skip at
`Information`, failure and unexpected exception at `Error`. Every line of a run
carries the run identifier, through both the message template and a log scope —
`SyncLoggingTests.EveryLineOfARun_CarriesTheRunIdentifier` asserts it on both
lines of a real run.

No audit row is written, and that is correct rather than missing: SC-11 and
`trebovaniya.md` §5 audit the **manual** start of a synchronization, which is
US-019. Hook telemetry unchanged and still git-ignored.

## 13. Dependencies

**No package was added** — `git diff -- *.csproj` is empty, confirming the
Implementation Report's claim independently. No project reference changed, so
`package-map.md` still holds (`Application` does not see `Infrastructure`; the new
marker lives in `Application` precisely because the readiness query may not depend
on `Web`).

`dotnet list package --vulnerable`: no vulnerable package in any of the seven
projects, against the current sources. This is a vulnerability-database check at
review time, not a guarantee of future disclosures.

## 14. Security Test Coverage

| Security property | Test | Status |
|---|---|---|
| Read-only writes nothing, calls nothing (SC-5, TC-5) | `SynchronizationRefusalTests` ×5, `SyncScheduleTests.InReadOnlyMode_…` ×3 | PASS |
| Guard evaluated first (AD-6) | `…TheGuardIsAskedFirst_AndNothingElseIsRead`, `TheGuard_IsConsultedBeforeAnythingIsRead` | PASS |
| The BR-026 closed list did not grow | `PermittedServiceWriteTests`, `ReadOnlyEnforcementTests` (existing, unchanged, green) | PASS |
| No personal data or connection string in logs (SC-10) | `TheLog_CarriesNoPersonalDataAndNoConnectionString` | PASS |
| No audit row for a scheduled run (SC-11) | `AScheduledRun_WritesNoAuditRow` | PASS |
| Readiness stays off the public port (SC-9) | `Readiness_IsNotReachableOnThePublicPort` | PASS (guard) |
| Readiness reveals no component detail | `Readiness_AnswersTheStateOnly` | PASS |
| Read-only stays `Degraded`, not an outage (DC-7) | `InReadOnlyMode_ReadinessStaysDegraded200` ×3 | PASS |
| Configuration rejection does not echo the value (SC-10) | `InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue` ×7 | PASS |
| Database enforces the row's invariants | `SyncStateSchemaTests` ×11 | PASS |
| No test reaches Google or a live Control Plane (TC-4) | whole suite; fixtures are the fake client and the manual clock | PASS |

**Not covered, and correctly so:** there is no protected endpoint in this Story, so
the TC-5 allowed-role / forbidden-role pair has nothing to apply to. That absence
is held in place by the existing endpoint-enumeration test.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A suspended school keeps synchronizing | the guard refuses and nothing is written | `InReadOnlyMode_…` in two layers | PASS |
| A school past its grace period synchronizes | same | theory over all three BR-025 causes | PASS |
| A never-legitimated installation synchronizes at start | the first run waits for the determination, then the guard refuses | `TheFirstRun_WaitsForTheFirstLegitimacyDetermination`, read-only theories | PASS |
| Synchronization runs without an approved domain | a connection for another domain is not usable | `WithAConnectionForAnotherDomain_TheRunIsSkipped` | PASS |
| Two runs write the row at once | the coordinator starts one; the row holds one run | `SyncRunCoordinationTests` ×6, `TheRow_NeverHoldsTwoRunsAtOnce` | PASS |
| A wedged interval stops synchronization forever | the wait for the determination is bounded by one interval | `WaitForFirstLegitimacyDeterminationAsync` (spec I-5) — code-reviewed, not test-driven (§21) | PARTIAL |
| An operator learns internals from readiness | the state word only | `Readiness_AnswersTheStateOnly` | PASS |
| A verbose failure fills the database or the log | the domain truncates at 512; the log carries a type name | `ALongMessage_IsTruncatedToTheStoredLength`, `ck_sync_state_error_length` | PASS |
| **Presentation code reads the OAuth client secret** | the secret is reachable only where it is needed | the record is registered nowhere and resolved nowhere; only a narrow `TimeSpan` record is in the container (§0) | **PASS in v2** (FAIL in v1 — F-1) |

Denial-of-service and rate limiting are not invented as requirements: a background
service has no external caller in this Story.

## 16. Repository Hygiene

The change set is 15 modified and 27 untracked files, every one of them source,
test, migration or documentation. No `.env`, no key, no token, no connection
string, no generated database file, no `.xlsx`. `google_credentials.json` and
`dac-classroom-agent-*.json` remain git-ignored and were not opened. The generated
migration and model snapshot are source and belong in the commit (PC-2).

## 17. Deviations

The Implementation Report's five deviations were judged independently:

- **D-1 (`LegitimacyCheckMemory.Changed`)** — **accepted, and it is the safer
  design.** A poll on an injected clock is not merely untestable; it would make the
  first run's timing depend on whatever else happens to move the clock. The signal
  is the pattern two existing coordinators already use. It adds a member to a
  US-005 type and changes no legitimacy behaviour: `LegitimacyCheckScheduleTests`
  and `LegitimacyLoggingTests` are green unchanged. No security impact.
- **D-2 (three table-set guards)** — accepted. Each kept its own assertion; only
  the sample set grew by the table this Story adds. The guards still fail if a
  future Story adds a table silently, which is their point.
- **D-3 (two fixture corrections)** — accepted, and the second one is
  security-relevant in a good way: answering the Control Plane with a **failure**
  rather than leaving it silent is what keeps the seeded read-only state intact, so
  the read-only tests now prove the refusal instead of timing out.
- **D-4 (coordinator tests as unit tests)** — accepted. Asserting a shared
  singleton while the production service competes for it proves nothing; the
  integrated guarantee is still asserted at host level. Coverage grew by three
  tests.
- **D-5 (`TheIntervalCountsFromCompletion` restated)** — accepted with the
  limitation recorded: with a zero-duration run the two readings coincide, so the
  test asserts what is observable and the comment says where it becomes
  distinguishable. No assertion was dropped.

**Not disclosed by the Implementation Report:** that `services.AddSingleton(settings)`
registers a record carrying two secrets. §4 of that report lists the line as
"DI registration … and the settings", which is accurate but does not surface the
consequence — hence F-1 rather than an informational note.

## 18. Findings

### F-1 — MAJOR — SECRET_MANAGEMENT — SC-7 — **CLOSED in v2 (§0)**

**File:** `src/ClassroomAgent.Web/Configuration/InstallationServices.cs:34`
(`services.AddSingleton(settings);`)

**Observed:** the whole `InstallationSettings` record is registered as a singleton
so that `SynchronizationBackgroundService` can read one `TimeSpan`
(`settings.SyncInterval`). That record carries `OAuthClientSecret` — the
**resolved** OAuth client secret, read from the secret store at start-up
(`InstallationSettingsReader.OAuthClientSecret`, and the record's own remarks say
so) — and `ConnectionString`, which holds the database credentials. Every
component in the Web host, including any controller, Razor page model or view
component, can now obtain both by constructor injection. Before this Story the
record was passed explicitly to the three places that needed parts of it
(`Program.cs`, `InstallationSecurityServices`, `AddInstallation`) and was not
resolvable from the container at all.

**Expected:** SC-7 requires the client secret to live in the secret store and to
be "never in the database, never in the repository, never in a log, and never in
the UI". The repository's own pattern for giving a component a setting is a
**narrow record**: `SchoolDefaults(settings.DefaultUiLanguage)` at line 50 and
`GoogleServiceAccountSettings(settings.ServiceAccountKeyReference)` at line 93 —
each carrying exactly one value, deliberately not the whole settings object.

**Risk:** no code reads the secret through this route today, so nothing leaks
right now. The regression is in defence in depth: the control that kept the secret
out of presentation code was that presentation code could not obtain it. A future
Story — or a diagnostic page written in a hurry — now needs no new registration to
render it. Personal-data-adjacent credentials of a school's own OAuth client, and
the database credentials, are exactly what SC-7 means to keep narrow.

**Required correction:** register a narrow record for the schedule — for example
`SyncScheduleSettings(TimeSpan Interval)`, built in `AddInstallation` the way
`SchoolDefaults` is — and remove `services.AddSingleton(settings)`. The background
service takes that record instead of `InstallationSettings`.
`SyncIntervalConfigurationTests` resolves `InstallationSettings` from the
container today and must be pointed at the narrow record; that is a test change
belonging to the same correction, and no assertion needs weakening — the tests
assert the effective interval, which the narrow record carries.

**Loop-back:** `IMPLEMENTATION` (the approved artifacts are correct; spec FR-013
and FR-018 do not ask for a broad registration).

**Verification after correction:** `git grep "AddSingleton(settings)"` finds
nothing; the narrow record carries only the interval; the configuration tests and
the whole suite stay green; the build stays at 0 warnings.

### F-2 — MINOR — CONFIGURATION — SC-10 — **CLOSED in v2 (§0)**

**File:** `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs`
(`SyncInterval`)

**Observed:** the default of 60 minutes is a literal inside the reader method,
while spec I-6 fixes it as a decision the Owner made at the gate and DC-3 now
documents it. A reader of the code sees a number with no name.

**Risk:** none to security; it is a maintenance hazard — the documented default and
the code default can drift apart silently, and the interval is the value that
bounds both the quota exposure of §6 and the first-run wait of spec I-5.

**Required correction:** none required before approval. Recommended: a named
constant next to the key constant, so the key, the bounds and the default read as
one unit. Recorded so IMPLEMENTATION may fold it into the F-1 correction.

### F-3 … F-6 — INFORMATIONAL

- **I-1 A stale `Running` row is tolerated by design** (spec I-3). A crashed
  process leaves the row claiming a run is in progress until the next run
  overwrites it. Readiness reports the outage, so the operational signal is
  correct; no correction is wanted.
- **I-2 The failure diagnosis is coarse.** `"RunFailed:" + type name` is exactly
  what SC-10 wants and exactly what makes a diagnosis thin: the Owner learns
  `RunFailed:NpgsqlException`, not which statement failed. US-017, which classifies
  failures, is where this becomes actionable.
- **I-3 The run identifier is a `Guid` in the log and the row**, so correlating a
  run with the database needs both; there is no human-readable run number. No rule
  asks for one.
- **I-5 The rejection message still spells its bounds in prose** — "expected an
  integer from 1 to 1440" sits next to the constants that now define them, so a
  future change to the bounds must edit two places. No security impact; the message
  reveals nothing about the rejected value, which is what SC-10 cares about.
- **I-4 The interval is unbounded upward at 1440 minutes** — a school configured at
  the maximum synchronizes once a day, which is a functional choice the Owner owns
  (OD-002), not a security limit. Noted only so it is not mistaken for a control.

## 19. Positive Controls (independently verified)

- The read-only guard is the **first statement** of the run use case, and the
  refusal path writes nothing — verified in the code and by four tests in two
  layers, for all three BR-025 causes.
- `PermittedServiceWrites` did **not** grow, and the two architecture guards that
  would have caught a silent service write are green without a new declaration.
- The new table is reachable only through its repository; no `DbContext` appears in
  `Web` (AD-3 intact).
- No Google port on the run path; no scope, key or impersonation change.
- No endpoint, page, policy or anonymous entry added; the endpoint-enumeration
  guard is green unchanged.
- No audit member, no audit constraint change, no audit row.
- The database enforces the row's invariants, including the three terminal-field
  disagreements — defence in depth beyond the domain.
- The rejected configuration value is never logged.
- No dependency added; no vulnerable package reported.
- Build 0 warnings; suite 2253 passed, 0 failed, 0 skipped; `dotnet format` clean;
  every OD-008 pragma removed.

## 20. Open Decisions

No blocking security Open Decision was identified. OD-001 … OD-008 are all
resolved, and F-1 needs no decision — it needs the narrow registration the
codebase already uses elsewhere.

## 21. Review Limitations

- `runtime_checks: PARTIAL` — this review read code, configuration and tests, and
  re-ran the suite; it did not drive a live host by hand and performed no
  penetration testing.
- The bound on the wait for the first legitimacy determination (spec I-5) is
  **code-reviewed only**: no test proves that a check which never completes still
  lets a run start after one interval, because the fixture always answers.
  Recorded as a coverage gap, not a finding — the Specification's wording is
  implemented as written.
- Vulnerability scanning reflects the package database at review time.
- The concurrency of `SyncRunCoordinator` was reviewed by reading its locking, not
  by a stress test; its guarantee is asserted functionally.

## 22. Verdict Rationale

**Version 2: PASS.** Both findings of version 1 are closed and were re-verified
against the working tree rather than the index; the corrections introduced nothing
new — the narrow record carries one `TimeSpan`, the wiring still starts, and the
suite is green. Every touched SC item is `PASS`; SC-1, SC-2, SC-3 and SC-12 remain
`NOT_APPLICABLE`. Five Informational observations stand and need no correction. No
blocking security Open Decision, no risk to accept, nothing suggesting credential
compromise. The Story is ready for `HUMAN_PR_APPROVAL`.

### Version 1 rationale, kept for the record

`CHANGES_REQUIRED` with `loop_back_stage: IMPLEMENTATION`. The Story's own
security behaviour is sound and well proved; the reason for not passing is a
supporting change that widened access to two secrets beyond what the Story needs,
against SC-7 and against the codebase's own narrow-settings pattern. It is a
one-line registration and a one-type addition to correct, with a test pointed at
the new type. No human security decision is required, no risk needs accepting, and
nothing suggests credential compromise.
