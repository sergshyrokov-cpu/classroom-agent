---
artifact_type: security_review
story: US-011
version: 1
status: APPROVED
created_at: 2026-09-26T13:23:59Z
updated_at: 2026-09-26T13:23:59Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-011-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 2
informational_findings: 6
security_sensitive: true
runtime_checks: PARTIAL
---

# US-011 Security Review — Check access diagnostic

## 1. Executive Summary

**Result: PASS.** No Critical and no Major finding. Two Minor findings, both about
test coverage rather than about insecure behaviour, and six Informational
observations.

This is the first Story in the system that actually calls Google, so the review
concentrated on the four properties that could not be structurally true before:
the service-account key never leaving the process (SC-7, a Hard Stop), only the
six read-only scopes of `trebovaniya.md` §6 ever being requested (SC-8,
NFR-021), read-only mode stopping the check before any call leaves the process
(SC-5, BR-026), and nothing Google returned reaching a response, a log line, an
audit row or the database (SC-10, SC-12).

All four were independently verified in the code and are covered by passing
tests. The key is resolved per check from `ISecretStore` through a reference in
configuration, is never persisted, rendered, logged or audited, and any
unusable content becomes the outcome `KeyUnavailable` with **no request sent**.
`DelegatedToken.ToString()` returns a placeholder, so an accidental
interpolation cannot leak a token. The six scopes come from the single
`Domain/Rules/GoogleDelegationScopes` constant, one per token request; the two
scopes the prototype used and the identity scopes are absent. The read-only
guard is the first statement of `RunAccessCheckUseCase.ExecuteAsync` and of the
startup self-check, before any repository, port or secret is touched.

**This Story closes the carried US-007 finding F-5**: the first real
`IGoogleDataPort` implementation takes `IReadOnlyModeGuard`, calls it first, and
the substituted port records zero calls for all three BR-025 causes — for the
interactive run and for the self-check alike.

Principal residual risk, and it is not a security weakness: the composition-root
wiring of the real Google adapter is exercised by no test (F-2). Every test
substitutes the port or constructs the adapter directly, so a DI defect would
surface only in production — and in the self-check path it would be swallowed and
logged as `AccessSelfCheckFailed` with nothing but the exception type name (F-3),
which is the very line the Owner relies on during a key rotation (DC-5). I read
the wiring and it is correct — `ISecretStore` is registered at `Program.cs:62`
and the adapter's three dependencies resolve — but the evidence is static, not
runtime.

Recommended next action: proceed to `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-011-check-access.md` | — (human-authored) |
| Specification | `docs/specifications/US-011-spec.md` | 1 (APPROVED; gate recorded 2026-09-21T07:54:55Z) |
| Open Decisions | `docs/decisions/US-011-open-decisions.md` | 2 (OD-001…OD-006 RESOLVED) |
| API design | `docs/designs/api/US-011-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-011-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-011-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-011-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-011-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-011-ac-test-matrix.md` | 1 |
| Implementation report | `docs/evidence/US-011-implementation-report.md` | 1 (verdict PASS) |
| Requirements | `trebovaniya.md` | 79 |
| Conventions | `docs/architecture/security-conventions.md` and the AD/API/PC/TC/DC files | current |

No mandatory input is `SUPERSEDED`. `api_design` and `database_design` record
`open_decisions` v1 while it is now v2; v2 adds OD-006 (the TEST_WRITING
skeleton) only, which touches no security requirement either design consumed.
This is not a staleness blocker.

## 3. Security-Relevant Scope

**Exposed functionality — installation public port only.** Two server-rendered
operations on `/settings/access-check`: `GET` (the page) and `POST` (run the
check). Both declare `InstallationPolicies.RunAccessCheck`. No `/api/v1` path,
no endpoint on the private port, no Control Plane endpoint, no
`ClassroomAgent.Contracts` type; `ContractVersion` stays 1. The startup
self-check has no HTTP surface at all.

**Protected assets touched:** the service-account key and its secret-store
reference; delegated access tokens; the `WorkspaceConnection` (technical account
address, domain); audit rows; the school's Google data (one course, one Meet
event — read, judged, discarded).

**Trust boundaries crossed:** browser → installation public host; controller →
Application use case; Application → the new Google port; Infrastructure → Google
token endpoint, Classroom API and Admin Reports API; Infrastructure → the secret
store (process environment); application → PostgreSQL. The Control Plane channel
is untouched.

**Affected security components:** `InstallationPolicies`,
`InstallationSecurityServices` (one new policy), `GlobalAntiforgeryFilter`
(unchanged, now covering one more POST), `IReadOnlyModeGuard` (one new caller
plus one background caller), `ServiceWriteScope` / `PermittedServiceWrite`
(unchanged), `AuditEvent` (two new factories), the audit table's two check
constraints, `ISecretStore` (one new consumer), installation configuration (one
new optional setting).

## 4. Environment and Tools

- .NET SDK **10.0.401**.
- Docker Desktop **29.8.0** running; Testcontainers PostgreSQL available, so the
  integration and schema tests ran for real (TC-2).
- Commands run during this review: `git status --porcelain`, `git diff`,
  `git ls-files`, `git show HEAD:…`, targeted `grep` over `src/` and `tests/`,
  `dotnet --version`.
- Commands whose results I took from the implementation stage of this same
  session, having watched them run: `dotnet build ClassroomAgent.sln` (0 errors,
  0 warnings), `dotnet test ClassroomAgent.sln` (**1976 total, 1976 passed, 0
  failed, 0 skipped**), `dotnet format --verify-no-changes` (clean),
  `dotnet list package --vulnerable --include-transitive` (clean for all seven
  projects).
- Not performed: penetration testing; any live Google call; running the
  installation host; resolving the real adapter from a live container (F-2).
- No secret value, no personal data and no content of
  `google_credentials.json` / `dac-classroom-agent-*.json` was opened, printed or
  copied.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | **PASS** | No role added. The policy covers exactly the §2 matrix row "Проверить доступ" (✔ Admin, ✘ Dean); no Teacher, Student or new matrix cell appears. `InstallationPolicies.RunAccessCheck`; `InstallationSecurityServices` requires `AppRole.Admin`. |
| SC-2 Authentication | **NOT_APPLICABLE** | The Story touches no password, no login flow, no cookie or HSTS setting, no Identity option. |
| SC-3 AllowedAdmin | **NOT_APPLICABLE** | No login path changed. |
| SC-4 Authorization | **PASS** | Class-level `[Authorize(Policy = RunAccessCheck)]` on `AccessCheckController`; no `[AllowAnonymous]` anywhere in the new code (verified by grep over the controller and the view folder); the SC-4 anonymous closed list gains nothing; the fallback policy is untouched. Antiforgery: `GlobalAntiforgeryFilter` is registered globally (`Program.cs:69`, `AddControllersWithViews`), the new `POST` carries no `IgnoreAntiforgeryTokenAttribute`, so it is validated, and the installation's public-port exemption list stays empty. Tests: `TheEndpoints_AreGetAndPost_AndNeitherIsAnonymous`, `TheAnonymousList_GainsNothing`, `ThePost_IsNotExemptFromAntiforgery`, `ARunWithoutTheAntiforgeryToken_IsRefused_AndNothingRuns`. The `GET` writes nothing and calls nothing (API-4), proven by `OpeningThePage_RunsNothing_AndRendersNoResult` and `OpeningThePage_WritesNoAuditRow`. |
| SC-5 Read-only mode | **PASS** | `RunAccessCheckUseCase.ExecuteAsync` step 1 is `readOnlyMode.EnsureAllowedAsync(Operation, …)`, before the port, the connection query, the repository or the secret store is touched; the refusal is the existing `ReadOnlyModeException`, mapped to `409` by the host handler, and this Story adds no second mapping. `RunStartupSelfCheckUseCase` calls the same guard first and turns the refusal into a skip. The only write under read-only mode is the audit row, declared `PermittedServiceWrite.AuditEvent` — on the BR-026 list — and `PermittedServiceWrite` was not modified, so it keeps exactly its approved members. Tests: `InReadOnlyMode_TheRunIsRefusedWith409_AndNoCallReachesGoogle` (×3 BR-025 causes), `ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason`, `InReadOnlyMode_TheSelfCheckIsSkipped_WithAWarning_AndCallsNothing` (×3), `InReadOnlyMode_ThePageIsServed_WithTheButton_AndCallsNothing` (×3). **Carried US-007 F-5 is closed.** |
| SC-6 No DB UI | **PASS** | No diagnostic or database endpoint added. The check reports outcome categories, never a query, a table or a row. |
| SC-7 Key | **PASS** | The key lives only in the secret store; configuration holds the **reference** (`Google:ServiceAccountKeyReference`), resolved per check in `GoogleAccessProbe.LoadKey` and dropped with the credential when the check ends — no static field, no cache. Nothing about it enters a DTO, the view model, the page, a log line, an audit row or the database: `AccessCheckPageModel` has no such member, `AuditEvent.AccessCheckRun/AccessCheckRefused` carry only ids and codes, and `LoadKey` returns `null` for every defect without echoing even the kind of defect. `DelegatedToken.ToString()` returns `"[delegated token]"`. Tests: `AnOwnerSideCause_RevealsNothingAboutTheKey`, `WithoutAReference_TheKeyIsUnavailable_AndNothingIsSent`, `AReferenceTheStoreDoesNotHold_IsKeyUnavailable_AndNothingIsSent`, `ContentThatIsNotAServiceAccountKey_IsKeyUnavailable_AndNothingIsSent`, `TheIssuedToken_NeverReachesThePage`. |
| SC-8 Google | **PASS** | Only `GoogleDelegationScopes.All` is ever passed to the port — the six read-only scopes of §6 — one scope per token request (`Scopes = [scope]`), with the **stored** technical account as the impersonated subject (`User = technicalAccount`), never the Admin's session and never a super-admin. `drive.file`, `classroom.profile.photos` and the identity scopes are absent from the constant. Nothing is written to Google: the two reads are `GET`s of page size 1, and the only non-GET request in a run is the token `POST` to Google's token endpoint. Retries are off (`ExponentialBackOffPolicy.None` on both the credential and the read initializer), and no permission or configuration outcome is ever retried. Tests: `TheRun_RequestsExactlyTheSixScopes_OnePerRequest_InOrder`, `EveryDelegation_ImpersonatesTheStoredTechnicalAccount_NotTheAdmin`, `AFullSequence_OnlyReachesGoogle_AndWritesNothing`, `APermissionFailure_IsNotRetried`, `GoogleUnavailable_IsNotRetried`, `AServerErrorOrThrottling_OnARead_IsGoogleUnavailable_AndNotRetried`. |
| SC-9 Channel | **NOT_APPLICABLE** | No service-channel, private-port or `WorkspaceConnection`-writing behaviour changed. The Story only *reads* the connection state through the US-009 query and does not re-derive the domain rule. |
| SC-10 Hygiene | **PASS** | Responses carry translation keys, outcome names, scope URIs, the stored technical account and the domain — no internals, no Google text. `AccessCheckLog` lines carry an account id, a request id, a step kind, a scope URI, an outcome category and the verdict; `SelfCheckFailed` logs only `failure.GetType().Name`; `LogUnclassified` logs a step kind and an HTTP status. No log line carries the key, the reference, a token, the technical account, the domain, Google's error text or anything from a response. The rejected payload cannot be logged because the `POST` binds no field at all (VR-001). Tests: `TheLog_CarriesNoAddressDomainOrToken`, `TheAuditRows_CarryNoPersonalDataAndNoFindings`. |
| SC-11 Audit | **PASS** | Every run writes one row (`AuditAction.AccessCheckRun`): `Succeeded` when the check was carried out whatever it found (spec I-4), `Refused` with `ReadOnlyMode` or the new `ConnectionNotUsable` otherwise. Fields: actor type/id/role, action, target type/id, outcome, refusal category, request id — no email, no domain, no scope, no finding. `AuditEvent.AccessCheckRefused` rejects any category outside the two it may carry. No update or delete path is added; the `created_at = updated_at` CHECK is untouched. The self-check writes no row, proven by `TheSelfCheck_WritesNothingToTheDatabase`. |
| SC-12 Owner | **PASS** | `Contracts` unchanged, `ContractVersion` still 1, no school statistic and no check result reaches the Control Plane. Nothing Google returned is copied anywhere: the reads inspect the answer for success only. |
| SC-13 Outbound | **PASS** | The one new destination is Google, already on the SC-13 list. The adapter's single transport is a `SocketsHttpHandler` with `AllowAutoRedirect = false` and `UseCookies = false`, so an answer cannot redirect a request carrying a bearer token to another host and no cookie is retained across calls. `AFullSequence_OnlyReachesGoogle_AndWritesNothing` asserts the host set. |

## 6. Authentication and Authorization

`trebovaniya.md` §2 gives "Проверить доступ" to the Admin and denies it to the
Dean. The implementation matches:

| Operation | Anonymous | Dean | Admin |
|---|---|---|---|
| `GET /settings/access-check` | `302 /sign-in` | `403` | `200` |
| `POST /settings/access-check` | `302 /sign-in` | `403` | `200` / `409` |

Both come from one class-level `[Authorize(Policy = RunAccessCheck)]`, and the
policy is its own — distinct from `ConfigureWorkspaceConnection` and
`ViewConnectionInstruction` (spec I-9), which matters because read-only mode
treats the three rows differently. Evidence:
`ThePolicy_IsSeparateFromTheOtherSettingsPolicies`, `ThePolicy_AdmitsAnAdmin`,
`ThePolicy_RefusesADean`, `ThePolicy_RefusesAnAnonymousPrincipal`,
`AnAnonymousVisitor_RunningTheCheck_IsSentToSignIn_AndNothingRuns`.

No identifier is accepted from the request, so there is no path by which one
school's Admin could aim the check at another school's data: the technical
account and the domain are read server-side from the stored connection
(VR-003). The `POST` has no request model at all.

Finding: **F-1 (Minor)** — the forbidden-role case is proven against the real
policy object with a synthetic Dean principal, not over HTTP, because no Dean can
sign in until US-012. This is the carried US-010 F-1 limitation, inherited rather
than introduced.

## 7. Credentials, Key and Google Access

No password handling in this Story.

**The key.** `Google:ServiceAccountKeyReference` is an optional installation
setting holding the *name* of the secret, trimmed, blank counted as absent
(VR-004). `InstallationSettingsReader` never logs it and the settings record
carries only the reference. `GoogleAccessProbe.LoadKey` resolves it through
`ISecretStore` on **every** request, parses it as a service-account key, and
returns `null` — becoming `KeyUnavailable`, with no request sent — when the
reference is absent, the store holds nothing, or the content is not a usable key.
The `catch` list is explicit and the comment states the rule the code follows:
the content is not echoed, not even its kind of defect. `EnvironmentSecretStore`
reads the process environment each time and caches nothing.

Spec I-1 is honoured: a missing key does not stop the installation. The school's
viewing and export keep working and the defect surfaces as a diagnosis.

**Scopes and impersonation.** Verified against `GoogleDelegationScopes` (six
read-only URIs, `drive.file` and `classroom.profile.photos` deliberately absent)
and against the adapter: one scope per credential, the technical account as
`User`, one `GET` per read with `PageSize = 1` / `MaxResults = 1`. The Admin's
own OAuth session is never used for a data call — the adapter has no access to
it.

**Classification.** An answer the adapter cannot classify becomes
`GoogleUnavailable` and is logged at `Error` with the step and the HTTP status
only, so an unrecognised answer is never passed off as a configuration
diagnosis. `5xx` and `429` are turned into unavailability in one place, inside the
borrowed handler, before the client library can parse an error body.

## 8. Sensitive Data Exposure

- **Responses and views.** `AccessCheckPageModel` carries the connection state,
  the technical account, the domain, the read-only flag and reason, a message key
  and the result. All are values the Admin saved or may already see on the US-009
  page. Razor encodes every one of them (VR-006); nothing is rendered as markup.
  No domain entity appears in a controller signature or a view model (AD-8).
- **Nothing from Google.** `AccessCheckResult` and `AccessCheckStep` can hold only
  a step kind, a scope URI, an outcome and a "not attempted because" outcome.
  There is no member that could carry a course, an event, an identifier or an
  error string.
- **Logs and audit.** Reviewed line by line in §5 (SC-10, SC-11).
- **Exceptions.** Step failures are outcomes, not exceptions; only the read-only
  refusal throws, into the existing handler. An unexpected failure reaches the
  single `IExceptionHandler` and the error page with no detail (AD-9, API-10).
- **Exports and telemetry.** Untouched. `docs/hooks/tool-usage.jsonl` remains
  git-ignored.
- **Test fixtures.** Synthetic throughout: a fresh RSA key per test
  (`SyntheticServiceAccountKey`), a dictionary secret store that never touches
  the process environment, and scripted answers in Google's documented formats
  (TC-4).

## 9. Input Validation

The `POST` accepts **no input** beyond the antiforgery token — there is no
request model, so a submitted account, scope or identifier has nothing to bind to
and is ignored (VR-001, proven by `APostedAccountOrScope_IsIgnored`). That
removes the whole class of body-tampering abuse rather than validating against it.

External input that *is* validated: the secret-store content (VR-004, must parse
as a service-account key with a private key and a client email) and the data
Google returns (VR-005, inspected for success only — nothing from it is bound to
anything the program keeps). The `GET` takes no parameter.

`[ApiController]` and `fieldErrors` do not apply: there is no JSON endpoint in
this Story (api-design §5), and the antiforgery refusal is the translated error
page, per SC-4 and API-7.

## 10. API Security

Exactly the two operations of the approved OpenAPI exist; nothing undocumented
was added, and `POST /api/v1/workspace-connection/test` was deliberately **not**
shipped (api-design §2.1), so no endpoint exists without a caller. Status codes
match the contract: `302`, `400`, `403`, `409` (two causes, one status), `200`
for any finding including a failed step. No `429` — nothing is rate-limited
(OD-005) and Google's own `429` becomes a `GoogleUnavailable` step, never
forwarded.

The evaluation order of the `POST` is fixed and observable:
authentication → authorization → antiforgery → read-only guard → connection
state → the eight steps → audit row → `200`. The guard-before-connection order is
tested explicitly, so a school that is both read-only and unconfigured answers
with the read-only reason and records zero port calls.

No Post-Redirect-Get, therefore no result in TempData and no result travelling
through a cookie — a deliberate choice that keeps the (unstored) result out of
the browser's storage entirely.

## 11. Persistence and Configuration

- **No table, no column, no index.** Nothing about a check is stored (OD-003).
  Verified against the change set and by the schema tests `NoTableIsAdded`,
  `TheAuditTable_GainsNoColumn`.
- **The only schema change** is the two amended CHECK constraints on
  `audit_event`, shipped as migration `20260921091106_AddAccessCheckAudit` with a
  correct `Down`. `AnUnknownAction_IsStillRejected` and
  `AnUnknownRefusalCategory_IsStillRejected` prove the lists stay closed.
- **No `EnsureCreated()` / `EnsureDeleted()`** anywhere in `src/` (grep
  verified); every schema change comes from a migration (PC-2).
- **No column that could hold the key, a reference, a token or a Google
  response** was added (SC-7, PC-9).
- **Database separation** untouched (AD-1); no connection string changed.
- **Configuration.** One new optional setting, holding a reference and never a
  secret. The installation host has no `appsettings.json`, so no committed
  configuration file changed and no school-specific value is hard-coded (AD-10,
  SC-7). Development settings cannot become runtime defaults through this change.
- `DbContext` appears in neither `Application` nor `Web` (AD-3); the use case
  reaches the database only through repositories and the decorated
  `IUnitOfWork`.

## 12. Logging, Audit and Telemetry

Reviewed in §5 (SC-10, SC-11) and §8. Two additional notes:

- The audit commit of a **refusal** stages nothing but the audit row, so the
  carried US-009 F-2 property — a declared service write commits the whole
  `DbContext` — holds: in the read-only path the only preceding operation is a
  read, and in the unusable-connection path only the row is added.
- `ServiceWriteScope.Declare` is used in both refusal paths, including when the
  installation is **not** read-only, where it has no effect: the declaration is
  read only by the read-only backstop, is limited to one at a time, and is
  disposed. It grants nothing.

Finding: **F-3 (Informational)** — `SelfCheckFailed` deliberately logs only the
exception type name, which is right for SC-10 but leaves an Owner with little to
act on if the self-check itself cannot run.

## 13. Dependencies

Three packages added, to `ClassroomAgent.Infrastructure` **only**, exactly as
OD-001 approved and spec FR-015 pins:

| Package | Version |
|---|---|
| `Google.Apis.Auth` | 1.76.0 |
| `Google.Apis.Classroom.v1` | 1.76.0.4254 |
| `Google.Apis.Admin.Reports.reports_v1` | 1.76.0.4252 |

`ClassroomAgent.Application.csproj` and `ClassroomAgent.Domain.csproj` are
unchanged (verified with `git diff --stat`), so `Application` and `Domain` keep
zero Google references and no Google SDK type crosses the port (AD-4). No project
reference changed, so `package-map.md` holds. No test package leaked into a
production project.

`dotnet list package --vulnerable --include-transitive` reported no vulnerable
package in any of the seven projects (spec S-13). This is a vulnerability
*database* check against nuget.org at review time, not an assertion that the
packages are free of undisclosed defects.

## 14. Security Test Coverage

| Requirement | Test | Status |
|---|---|---|
| S-01 own Admin-only policy, SC-4 list unchanged | `AccessCheckAuthorizationTests.ThePolicy_*`, `TheAnonymousList_GainsNothing` | PASS |
| S-02 allowed and forbidden role (TC-5) | `ThePolicy_AdmitsAnAdmin`, `ThePolicy_RefusesADean` | PASS with F-1 |
| S-03 read-only: zero Google calls, all three causes, Application layer | `AccessCheckRefusalTests.InReadOnlyMode_*`, `StartupSelfCheckTests.InReadOnlyMode_*` | PASS |
| S-04 only the six read-only scopes, one per request | `TheRun_RequestsExactlyTheSixScopes_OnePerRequest_InOrder`, `AFullSequence_OnlyReachesGoogle_AndWritesNothing` | PASS |
| S-05 the stored technical account is the subject | `EveryDelegation_ImpersonatesTheStoredTechnicalAccount_NotTheAdmin`, `ATokenRequest_IsForThatOneScope_ImpersonatingTheTechnicalAccount` | PASS |
| S-06 nothing written to Google | `AFullSequence_OnlyReachesGoogle_AndWritesNothing` (GET reads, one token POST) | PASS |
| S-07 no key, reference or token anywhere | `AnOwnerSideCause_RevealsNothingAboutTheKey`, `TheIssuedToken_NeverReachesThePage`, the three `KeyUnavailable` groups | PASS |
| S-08 nothing Google returned escapes | `TheAuditRows_CarryNoPersonalDataAndNoFindings`, `TheLog_CarriesNoAddressDomainOrToken`, model shape | PASS |
| S-09 no retry of permission/configuration failures | `APermissionFailure_IsNotRetried`, `GoogleUnavailable_IsNotRetried`, `AServerErrorOrThrottling_OnARead_IsGoogleUnavailable_AndNotRetried` | PASS |
| S-10 POST with antiforgery, GET runs nothing | `ThePost_IsNotExemptFromAntiforgery`, `ARunWithoutTheAntiforgeryToken_IsRefused_AndNothingRuns`, `OpeningThePage_RunsNothing_AndRendersNoResult` | PASS |
| S-11 audit rows without personal data | `AccessCheckAuditTests.*`, `AccessCheckRefusalTests` audit cases, `AccessCheckAuditSchemaTests` | PASS |
| S-12 no new outbound destination | `AFullSequence_OnlyReachesGoogle_AndWritesNothing` | PASS |
| S-13 packages free of known vulnerabilities | `dotnet list package --vulnerable` | PASS |
| TC-4 no live Google call | property of the suite: the fake port and the scripted transport are the only seams | PASS with F-2 |
| AC-010 every message translated, no Workspace role named | `AccessCheckTranslationTests.*` (21 keys × 3 assertions) | PASS |

Test quality note: these are not call-count-only tests. The read-only cases
assert both the HTTP status and that the substituted port recorded **zero**
calls, which is the property SC-5 needs; the scope test compares the recorded
scope list for equality with the constant, so an extra or forbidden scope fails
it; the key cases assert that **no request was sent**, not merely that an outcome
was returned.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A Dean opens or posts to the check | `403`, nothing runs | policy tests | Protected (F-1 on the HTTP level) |
| An anonymous visitor posts to the check | `302`, zero port calls, zero rows | `AnAnonymousVisitor_RunningTheCheck_IsSentToSignIn_AndNothingRuns` | Protected |
| A cross-site form posts the check | `400` error page, nothing runs | global antiforgery filter + test | Protected |
| An Admin runs the check while the installation is read-only | `409`, **no token request**, audit row `refused/read_only_mode` | three-cause tests | Protected |
| An Admin submits a technical account or scope in the body | ignored; the stored values are used | no request model + `APostedAccountOrScope_IsIgnored` | Protected |
| An Admin uses the check to fish for the key or the Cloud project | messages name only that the Owner must act | `AnOwnerSideCause_RevealsNothingAboutTheKey` | Protected |
| An Admin uses the check to read school data | the answer is inspected for success only; no field is copied out | model shape, `TheAuditRows_CarryNoPersonalDataAndNoFindings` | Protected |
| An Admin re-runs the check repeatedly (8 Google calls each) | no limit by design; every run audited | OD-005 (resolved by the Owner), `EveryRun_IsItsOwnRow` | Accepted by decision — see F-6 |
| Google answers slowly or never | the run ends within 30 s as `Inconclusive` | `WhenGoogleNeverAnswers_TheRunEndsAtTheTimeLimit_AsInconclusive` | Protected |
| Google returns a redirect to another host with the bearer token | `AllowAutoRedirect = false` | adapter wiring | Protected |
| The key is deleted mid-rotation | `KeyRejected`, run-wide, one call | adapter mapping tests | Protected |
| A failing self-check takes the installation down | it cannot: the exception is caught and logged | `AFailingPort_DoesNotPreventTheStart` | Protected |

## 16. Repository Hygiene

`.gitignore` covers `google_credentials.json`, `dac-classroom-agent-*.json`,
`*.json.key`, `classroom_cache.db`, `*.xlsx` (with the single documented
exception for the blank templates under `docs/product/report-templates/`),
`bin/`, `obj/`, logs and the hook telemetry. `git ls-files` shows no credential
file, no `.env`, no generated database and no export other than the versioned
blank template. The 41 uncommitted files are all under `src/` and one test file
— no local configuration, no IDE file, no secret-like artifact. No finding.

## 17. Deviations

**D-1. Three existing US-007 tests were scoped to the operation under test.**
`tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyRefusalLoggingTests.cs`
counted `ReadOnlyWriteRefused` events across the whole host log; the startup
self-check now consults the same guard at every start, so a read-only host
legitimately holds one more refusal line.

I judged this independently and accept it. Spec FR-010 and I-7 require the
self-check to decide read-only mode through the same guard as every use case, so
the extra line is required behaviour, not a defect to hide; the alternative —
not calling the guard — would contradict the approved Specification and the
US-007 structural rule. The change adds a filter parameter and removes no
assertion: US-007 AC-010 (a refused write writes exactly one `Warning` line
naming its operation and the reason category) is asserted exactly as before, now
against the operation the test actually performs. The property that was lost —
that no *other* component logs a refusal in the same host — was never the test's
subject and is required by no artifact. It is a security-relevant test file, so
it is recorded here as a deviation rather than passed over: `test_strategy` §5
did not foresee it, unlike the anticipated `AppUserMigrationTests` change.

**D-2. `AccessCheckPageModel.Domain` is never rendered.** The openapi view model
includes the domain and the implementation carries it, but the view shows only
the technical account. Harmless, and it keeps the carried US-010 F-2 concern
(`legitimacy_state.domain` has only a length constraint) entirely theoretical for
this Story: the domain reaches no Google request, no log line and no audit row,
and if it were rendered Razor would encode it. Recorded as Informational (F-5).

No other deviation between the approved security requirements and the
implementation was found. No finding contradicts the Implementation Report; its
§7 disclosure matches what I observed.

## 18. Findings

### F-1 — Minor — TEST_COVERAGE — SC-1, TC-5

- **Affected:** `tests/ClassroomAgent.Tests/Web/Security/AccessCheckAuthorizationTests.cs`;
  the two new endpoints.
- **Observed:** the forbidden-role case is proven against the real policy object
  with a synthetic Dean principal. There is no HTTP-level test in which a
  signed-in Dean receives `403` from `/settings/access-check`.
- **Expected:** TC-5 asks for an allowed-role and a forbidden-role test per
  protected endpoint.
- **Risk:** low. The policy is the same object the endpoint declares, and the
  fallback policy denies by default; a wiring mistake would have to bypass a
  class-level attribute.
- **Required correction:** none in this Story. This is the carried US-010 F-1
  limitation, inherited because no Dean can sign in until **US-012**, which owns
  the fix for all three settings screens at once.
- **Loop-back:** none. **Re-verification:** in US-012, add the HTTP-level
  forbidden-role case for all three settings actions together.

### F-2 — Minor — TEST_COVERAGE — SC-7, SC-8, DC-5

- **Affected:** `src/ClassroomAgent.Web/Configuration/InstallationServices.cs`
  (the `IGoogleAccessProbe` singleton factory); the whole test suite.
- **Observed:** no test resolves the **real** `GoogleAccessProbe` from a
  container. HTTP and self-check tests substitute `FakeGoogleAccessProbe`; the
  adapter tests construct `GoogleAccessProbe` directly with a dictionary secret
  store and a scripted transport. The composition-root wiring — the singleton
  factory, `ISecretStore`, `GoogleServiceAccountSettings`, the transport — is
  therefore exercised by nothing.
- **Expected:** security-relevant wiring should have some evidence beyond
  reading it.
- **Risk:** low for confidentiality, moderate for availability of the diagnostic
  itself. I verified the wiring statically and it is correct: `ISecretStore` is
  registered at `Program.cs:62` and all three dependencies resolve. But if it
  were not, the interactive check would answer `500` and — worse — the startup
  self-check would fail inside the background service's catch-all and log
  `AccessSelfCheckFailed` with only an exception type name. That is precisely the
  line the Owner reads to confirm a rotated key before deleting the old one
  (DC-5), so a silent wiring defect would degrade the rotation procedure rather
  than announce itself.
- **Required correction:** none required for this Story — no approved artifact
  demands such a test, and inventing one here would be inventing a requirement.
  Recommended for a later Story or as a deliberate decision: one test that
  resolves `IGoogleAccessProbe` from the real installation container with no key
  reference configured and asserts the outcome `KeyUnavailable` with zero
  requests — which needs no network and no key.
- **Loop-back:** none. **Re-verification:** whenever synchronisation (US-013 and
  later) puts this port on a critical path.

### F-3 — Informational — LOGGING — SC-10, DC-10

`AccessCheckLog.SelfCheckFailed` logs only `failure.GetType().Name`. Correct
under SC-10, and the right trade-off, but it means an Owner facing a self-check
that cannot run at all sees a type name and no cause. Worth remembering when
DC-10 is next revisited; no change required now.

### F-4 — Informational — READ_ONLY_MODE — SC-5, TC-5, US-007 F-5

The read-only proof runs through the real host over HTTP with the port
substituted (`Web.UseCases.AccessCheckRefusalTests`), not as a bare Application
unit test. It nevertheless proves what TC-5 requires — enforcement in
`Application` and zero port calls, not a hidden button — because the refusal
happens before the port is reached, and the structural
`Application.UseCases.GoogleDataPortRuleTests` independently requires any use
case holding an `IGoogleDataPort` to take the guard. **The carried US-007 F-5 is
satisfied and can be closed.**

### F-5 — Informational — DATA_EXPOSURE — carried US-010 F-2

`AccessCheckPageModel` carries the school domain but the view never renders it,
and the domain reaches no Google request, log line or audit row. The carried
concern that `legitimacy_state.domain` has no character-set constraint therefore
does not bite in this Story; were the value rendered later, Razor would encode
it (VR-006).

### F-6 — Informational — API_SECURITY — OD-005

Nothing limits how often an Admin may run the check, so a signed-in Admin can
spend the school's Google quota eight calls at a time. This is the Owner's
resolved decision (OD-005, option 1), every run is audited, and the actor is a
trusted school administrator. Recorded so the decision stays visible, not as a
requirement to add.

### F-7 — Informational — TEST_COVERAGE — carried US-010 F-6

No host sends a `Content-Security-Policy` header. The new page adds no script,
no inline handler and no external script reference, so it does not widen the
gap.

### F-8 — Informational — GOOGLE_ACCESS — spec I-8

The check proves that the technical account *may* call each API, not that it sees
every course, and no check proves it holds nothing beyond read access. Both
belong to `trebovaniya.md` §7 item 10, which stays open. No message names a
Workspace admin role, asserted by `NoMessage_NamesAWorkspaceAdminRole`.

## 19. Positive Controls

Independently observed and verified in the code, not taken from the report:

1. The read-only guard is the **first** statement of both use cases, before any
   repository, port or secret access.
2. `PermittedServiceWrite` was not modified; the refusal path declares only
   `AuditEvent` and stages nothing else.
3. `GoogleDelegationScopes.All` holds exactly the six read-only scopes; the two
   prototype scopes and the identity scopes are absent.
4. One scope per credential, the stored technical account as the impersonated
   subject, retries disabled on both the credential and the reads.
5. The key is resolved per request, never cached in a static field, and every
   defect collapses to `KeyUnavailable` with no request sent and nothing echoed.
6. `DelegatedToken.ToString()` cannot print the token.
7. No `[AllowAnonymous]`, and the global antiforgery filter covers the new `POST`
   with no exemption.
8. `Application` and `Domain` project files are unchanged — no Google reference
   crosses the port.
9. No `EnsureCreated()` / `EnsureDeleted()` in `src/`; the only schema change is
   the migration, with a correct `Down`.
10. `AllowAutoRedirect = false`, `UseCookies = false` on the one transport the
    adapter uses.
11. `.gitignore` and `git ls-files` show no secret, no generated database and no
    export beyond the versioned blank template.
12. `ISecretStore` is genuinely registered (`Program.cs:62`) — checked because
    the adapter resolves it from the container.

## 20. Open Decisions

No blocking security Open Decisions were identified.

OD-001…OD-006 are all RESOLVED and were implemented as resolved. OD-001 is the
approval `AGENTS.md` requires for the three new packages; OD-003 keeps any check
result out of the database; OD-005 is the decision behind F-6.
`trebovaniya.md` §7 item 10 stays open and is touched, not closed (F-8) — that is
what spec I-8 approved, not an unresolved decision this Story needed.

## 21. Review Limitations

- **Static and test-based review only.** No penetration testing, no live Google
  call, no running installation host.
- **The real adapter was never resolved from a live container** (F-2); its wiring
  was verified by reading `Program.cs` and `InstallationServices`.
- **The adapter's mapping is proven on synthetic answers** in Google's documented
  formats. If Google phrases an error differently in production, the fallback is
  `GoogleUnavailable` — so a *wrong* diagnosis is ruled out, an unhelpful one is
  not (test strategy §7).
- **Build/test evidence** was produced in the implementation stage of this
  session and observed there, not re-run inside this review; the commands and
  exact counts are in §4.
- **Vulnerability scanning** reflects the nuget.org advisory database at review
  time.
- The change set is **uncommitted**; this review describes the working tree as of
  2026-09-26T13:23:59Z.

## 22. Verdict Rationale

**PASS.** The implementation report records a green build and a green suite, and
I saw both. Every SC item the scope touches is `PASS` with evidence. There is no
Critical and no Major finding: the two Minor findings are gaps in test coverage
whose corrections belong to US-012 (F-1, carried) or to no approved requirement
at all (F-2, a recommendation I deliberately did not turn into a demand), and the
six Informational items require no change. Every security-sensitive Acceptance
Criterion — AC-001, AC-002, AC-003, AC-006, AC-007, AC-008, AC-009 — is verified
by a passing test whose assertion actually proves the property. No security
Open Decision is unresolved, and no human security exception is being requested.

The one judgement call I had to make independently was D-1, the change to three
US-007 tests. I accept it: the extra log line it accommodates is behaviour the
approved Specification requires, and the change sharpened the assertions instead
of weakening them. It is recorded in §17 so that `HUMAN_PR_APPROVAL` decides
with it in view rather than discovering it later.

Recommended next stage: **HUMAN_PR_APPROVAL**.

---

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-011
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-011-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "F-1 (Minor, TEST_COVERAGE, SC-1/TC-5): the forbidden-role case for the two new endpoints is proven against the real policy with a synthetic Dean principal, not over HTTP, because no Dean can sign in until US-012. Carried US-010 F-1; owner US-012, which should add the HTTP-level case for all three settings actions at once."
    - "F-2 (Minor, TEST_COVERAGE, SC-7/SC-8/DC-5): no test resolves the real GoogleAccessProbe from a container - every test substitutes the port or constructs the adapter directly - so the composition-root wiring has no evidence beyond reading it. I verified it statically and it is correct (ISecretStore is registered at Program.cs:62). A wiring defect would answer 500 for the Admin and, in the self-check, be swallowed and logged as AccessSelfCheckFailed with only an exception type name, degrading the DC-5 rotation procedure. No approved artifact demands such a test; recommended for a later Story: resolve IGoogleAccessProbe from the real container with no key reference and assert KeyUnavailable with zero requests."
    - "F-3 (Informational, LOGGING, SC-10/DC-10): SelfCheckFailed logs only the exception type name - right under SC-10, but an Owner facing a self-check that cannot run at all gets no cause."
    - "F-4 (Informational, READ_ONLY_MODE): the read-only proof runs over HTTP with the port substituted rather than as a bare Application unit test; it still proves what TC-5 requires (enforcement in Application, zero port calls), and the structural GoogleDataPortRuleTests covers the constructor rule. THE CARRIED US-007 FINDING F-5 IS SATISFIED AND CAN BE CLOSED."
    - "F-5 (Informational, DATA_EXPOSURE, carried US-010 F-2): AccessCheckPageModel carries the school domain but the view never renders it, and the domain reaches no Google request, log line or audit row, so the unconstrained character set of legitimacy_state.domain does not bite here."
    - "F-6 (Informational, API_SECURITY, OD-005): nothing limits how often an Admin may run the check (eight Google calls per run). The Owner's resolved decision; every run is audited. Recorded to keep the decision visible."
    - "F-7 (Informational, carried US-010 F-6): no host sends a Content-Security-Policy header; the new page adds no script, inline handler or external script reference, so it does not widen the gap."
    - "F-8 (Informational, GOOGLE_ACCESS, spec I-8): the check proves the technical account may call each API, not that it sees every course nor that it holds nothing beyond read access - trebovaniya.md section 7 item 10 stays open."
    - "D-1 (deviation accepted, review section 17): three existing US-007 tests in ReadOnlyRefusalLoggingTests were scoped to the operation under test because spec FR-010/I-7 makes the startup self-check consult the same guard at every start. Judged independently: the extra log line is required behaviour, no assertion was removed or weakened, and US-007 AC-010 is asserted exactly as before. Recorded so HUMAN_PR_APPROVAL decides with it in view."
    - "Review limitations (section 21): static and test-based review only; no live Google call and no running host; the adapter's mapping is proven on synthetic answers, so a wrong diagnosis is ruled out but an unhelpful one is not; build and test evidence was produced and observed in this session's implementation stage, not re-run inside the review; vulnerability scanning reflects the nuget.org advisory database at review time."
```
