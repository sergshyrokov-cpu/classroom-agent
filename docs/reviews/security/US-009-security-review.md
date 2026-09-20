---
artifact_type: security_review
story: US-009
version: 1
status: APPROVED
created_at: 2026-09-20T18:35:33Z
updated_at: 2026-09-20T18:35:33Z
produced_by: security-reviewer
inputs:
  - path: docs/evidence/US-009-implementation-report.md
    version: 1
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
  - path: docs/designs/api/US-009-api-design.md
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/designs/database/US-009-db-design.md
    version: 1
  - path: docs/designs/database/US-009-entity-model.md
    version: 1
  - path: docs/tests/US-009-test-strategy.md
    version: 1
  - path: docs/tests/US-009-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 4
security_sensitive: true
runtime_checks: PARTIAL
---

# US-009 Security Review — Configure WorkspaceConnection

## 1. Executive Summary

**PASS.** 0 Critical, 0 Major, 1 Minor, 4 Informational.

The control this Story exists to build — the Owner's second point of control,
BR-020 — is implemented where it cannot be routed around, and that was verified
rather than taken on trust: a request that carries a domain of its own is
ignored, and the row is still bound to the `Installation` domain that
`LegitimacyState` holds. Everything the requirements forbid this table to hold
is absent, at the column level: no key, no secret, no reference to either.

The Story adds no Google port, no scope, no outbound destination and no NuGet
package. The BR-026 closed list of service writes keeps exactly its four
members, and the SC-4 list of anonymous endpoints gains nothing.

One finding of the implementation deserves emphasis because it **improves** a
control rather than merely satisfying one: the stage discovered that the
translated error page was rendered in the server's culture rather than the
user's (NFR-073), a latent defect of the US-008 baseline that US-008's own
tests could not have caught, and fixed it. The fix is sound and now covered by
tests in all three BR-025 causes.

The single Minor finding is a test-shape limitation, not a weakness in the
implementation: the forbidden-role half of TC-5 is proven against the real
policy object rather than over HTTP, because no Dean can sign in until US-012.

Recommended next action: proceed to `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As listed in `inputs` above. Every one is the current, non-superseded version.
`HUMAN_SPEC_APPROVAL` is recorded (2026-09-20T13:25:00Z), and the
`implementation_report` records verdict `PASS` with concrete build and test
output, which this review re-ran independently (§4).

## 3. Security-Relevant Scope

**Exposed functionality** — installation public port, two operations, both
server-rendered and both Admin-only: `GET /settings/workspace-connection` and
`POST /settings/workspace-connection`. The Control Plane gains nothing. The
installation's private port gains nothing.

**Protected assets** touched: the `WorkspaceConnection` record (the school's
domain and its technical account), the audit trail, `LegitimacyState` (read
only), and the Admin's session.

**Trust boundaries** crossed: browser → installation public port (HTTPS,
session cookie, antiforgery); controller → `Application` use case; `Application`
→ PostgreSQL. **No boundary to Google and none to the Control Plane is crossed
by this Story's code** — verified in §7.

## 4. Environment and Tools

- .NET 10 SDK; solution `ClassroomAgent.sln`.
- Docker Desktop available; integration tests ran against PostgreSQL in
  Testcontainers (TC-2).
- Commands re-run by this review, with observed results:
  - `dotnet build ClassroomAgent.sln` → 0 errors, 0 warnings;
  - `dotnet test ClassroomAgent.sln` → **1651 passed, 0 failed, 0 skipped**;
  - `dotnet list package --vulnerable` → no vulnerable packages in any of the
    seven projects (point-in-time nuget.org result);
  - `git diff --stat -- "*.csproj"` → empty: no dependency changed.
- Not performed: penetration testing, fuzzing, live Google or Control Plane
  calls (all forbidden by this Skill and by TC-4).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | `InstallationPolicies.ConfigureWorkspaceConnection` implements the one §2 matrix cell this Story needs — Admin ✔, Dean ✘ — and no other cell was added. No Teacher or Student appears anywhere. |
| SC-2 Authentication | PASS (unchanged) | The Story adds no authentication path, no password and no cookie option. The session is the one US-008 issues. |
| SC-3 AllowedAdmin | PASS (unchanged) | No change to the login check. The settings reuse the existing session; `SaveWorkspaceConnectionTests.TheSave_CallsNothingOutsideTheInstallation` shows the save performs no Control Plane call of its own. |
| SC-4 Authorization | PASS | Both endpoints declare the policy (`[Authorize(Policy = …)]` on `WorkspaceConnectionController`); `TheAnonymousList_GainsNothing` and the US-008 endpoint enumeration both pass; `TheSaveEndpoint_IsNotExemptFromAntiforgery` and `TheSaveWithoutAnAntiforgeryToken_IsRefused` (400) confirm the global antiforgery rule covers the new POST; `AGetWithTheFieldInTheQuery_SavesNothing` confirms no state change on GET. |
| SC-5 Read-only mode | PASS | `SaveWorkspaceConnectionUseCase` takes `IReadOnlyModeGuard` and calls it as its first statement; it is **not** registered in `PermittedServiceWrites`; `ThePermittedServiceWriteList_IsUnchanged` and `NoNewUseCase_IsRegisteredAsAPermittedServiceWrite` pass; the three BR-025 causes each answer `409` with the table untouched. |
| SC-6 No DB UI | PASS | No diagnostic endpoint, SQL console or entity explorer added. |
| SC-7 Key | PASS | `TheTable_HoldsNoCredentialColumn` enumerates `information_schema.columns` and finds no column whose name carries key, secret, credential, password, token or client id. No form field, DTO property or configuration binding accepts one. |
| SC-8 Google | PASS | No Google port, no scope, no call. `grep` over the change set finds no Google type; the only outbound transport recorded no request during a save. |
| SC-9 Channel | PASS | The BR-020 refusal is the subject of the Story and is enforced in `Application` (§6). Nothing was added to the service channel or the private port. |
| SC-10 Hygiene | PASS | `WorkspaceConnectionLog` writes the account id, the request id and a category only; three tests assert the typed address never appears in the log — for a malformed value, a refused save and a successful one. The error page carries no internals. |
| SC-11 Audit | PASS | One row per attempt, success or refusal, with actor id and role, target type, request id and a refusal category; `NoRow_CarriesTheAddressOrTheDomain` asserts no `@` and no domain in any row's JSON; `AnAuditRow_CannotBeUpdated` confirms immutability still holds. |
| SC-12 Owner | PASS | `Contracts` unchanged; the Control Plane learns nothing about a school's connection — no endpoint, no wire type, no migration. |
| SC-13 Outbound | PASS | The only destination reachable from this Story's code is the installation's own database. |

## 6. Authentication and Authorization

The permission-matrix row "Настройка `WorkspaceConnection` (домен,
impersonation)" is ✔ for the Admin and ✘ for the Dean. The implementation
expresses exactly that: a policy requiring an authenticated user in role
`Admin`, declared on the controller and therefore on both endpoints.

Independently verified:

- an Admin principal is authorised and a Dean principal is refused **by the
  host's own `IAuthorizationService`**, against the real registered policy;
- an anonymous principal is refused, and an anonymous request is redirected to
  `/sign-in` without ever rendering the page;
- neither endpoint allows anonymous access, and the SC-4 closed list is
  unchanged;
- the save requires the antiforgery token and is not exempt.

No service method or background path reaches the save: the use case is resolved
per request and is called only by the controller.

## 7. Credentials, Key and Google Access

The Story handles no password and no credential. The service-account key and its
reference remain where DC-3 and SC-7 put them — the secret store and
configuration — and the new table's column list makes a key column impossible
rather than merely absent.

No Google scope is requested and no Google API is reached. The technical account
is **recorded, not verified**, exactly as the Specification and the openapi
state; proving it works is US-011. Consequently the US-007 security-review
finding F-5 (the first real Google data port must prove its own read-only
refusal) still has no owner here and moves on to US-011.

## 8. Sensitive Data Exposure

| Surface | Result |
|---|---|
| Responses and views | The page renders the school's domain and the stored technical account to an Admin only. No `Html.Raw` anywhere in the views; Razor escaping applies to the echoed value after a refusal. |
| DTOs | `WorkspaceConnectionView` and `WorkspaceConnectionPageModel` carry data plus translation **keys**, never rendered sentences and never an entity (AD-8). |
| Logs | Account id, request id and categories only; three tests assert the address is absent. |
| Audit rows | Internal identifiers and codes only; asserted by JSON inspection of every row. |
| Exceptions | The read-only refusal renders the translated error page with a reason; every other failure is the US-008 error page with no internals. |
| Exports, telemetry | Untouched by this Story. |

## 9. Input Validation

The save request has exactly one property, so a domain, a role or an account
state sent in the body has nothing to bind to — verified at runtime by
`ADomainSentInTheRequest_IsIgnored`, which posts `domain` and `Domain` alongside
the address and finds the stored row still bound to the installation's own
domain.

Validation is server-side, explicit and runs before the use case: `[Required]`,
`[StringLength(254)]` and `ServiceAccountEmailAttribute` (one `@`, non-empty
parts, no whitespace, a domain part satisfying `WorkspaceDomainAttribute`).
Eleven malformed shapes are rejected with `400` and no write; the 254-character
boundary is tested on both sides.

**Positive control worth naming:** every length check precedes its regular
expression — `ServiceAccountEmailAttribute` rejects anything over 254 characters
before splitting, and `WorkspaceDomainAttribute.IsDomain` checks 3…253 before
matching — so the domain pattern always runs on bounded input and the nested
quantifiers cannot be driven into pathological backtracking.

The rejected value is never logged (SC-10) and is echoed back only into its own
form field.

## 10. API Security

Only the two approved operations exist. Status codes follow api-design §2.4 and
§5: `400` for a malformed request or a missing antiforgery token, `409` for
every refusal by installation state (domain mismatch, impersonation domain
mismatch, domain not confirmed, read-only mode), `403` for the wrong role,
`302` on success. No `/api/v1` path was added, so no API-6 body is produced
here.

The success path is Post-Redirect-Get with the confirmation in TempData rather
than a query parameter — the US-008 rule that stops a crafted link from
rendering arbitrary text on the school's own page.

## 11. Persistence and Configuration

The migration `AddWorkspaceConnection` creates the table with a primary key, a
unique singleton index and eight check constraints, and amends four audit
constraints. Schema changes come only from the migration; no `EnsureCreated()`
exists. The Control Plane schema and model snapshot are untouched, so AD-1 holds.

Two design points were reviewed closely:

- **`ck_workspace_connection_email_domain`** makes a stored row agree with
  itself — the technical account always belongs to the domain the row is bound
  to. It is correctly documented as a last line of defence, not as BR-020: the
  Owner's rule compares against another table and stays in `Application`. Both
  layers exist, which is the defence-in-depth this review wants to see.
- **The relaxation of `ck_audit_event_target`** (db-design §4.1) from
  "both or neither" to "an id requires a type" is sound. It still forbids an
  identifier belonging to nothing, and it is what lets a refused action name
  *what* was refused without inventing a row. `AnAuditRowWithATargetIdButNoType_IsRejected`
  proves the remaining half of the invariant at runtime, and
  `AnUnknownTargetType_IsRejected` proves the new value constraint by
  **constraint name**, so neither can pass on an unrelated rule.

Configuration: nothing added. No connection string, secret or school-specific
value entered the repository.

## 12. Logging, Audit and Telemetry

Every audited action the Story introduces writes a row and is proven by a test,
including the four refusal categories. Rows are never updated and are deleted
only by the retention purge, which this Story does not touch.

Hook telemetry is unchanged and remains git-ignored.

## 13. Dependencies

No package was added, removed or upgraded: `git diff` over the `.csproj` files
is empty. `dotnet list package --vulnerable` reports no vulnerable package in
any project — a point-in-time result against nuget.org, not a guarantee.
Project references are unchanged, and the architecture tests that lock them —
`ProjectReferences_AreExactlyThoseAllowed`,
`FrameworkFreeProjects_ReferenceNoPackage`,
`ApplicationAssembly_ReferencesNoInfrastructureContractsOrEfCore` — all pass, so
`Application` still references no NuGet package and `Domain` still has none.

## 14. Security Test Coverage

| Requirement | Test | Status |
|---|---|---|
| S-01 policy, both roles | `WorkspaceConnectionAuthorizationTests.ThePolicy_AdmitsAnAdmin` / `…RefusesADean` / `…RefusesAnAnonymousPrincipal` | PASS (see F-1 on the level) |
| S-02 BR-020 in `Application` | `ADomainSentInTheRequest_IsIgnored`, `AnAddressOutsideTheDomain_IsRefused` | PASS |
| S-03 the domain comes from `LegitimacyState` | `WithNoKnownDomain_TheSaveIsRefused`, `WithADomainButNoSuccessfulCheck_TheSaveIsStillRefused`, `SaveWorkspaceConnectionBranchTests` | PASS |
| S-04 no credential column | `TheTable_HoldsNoCredentialColumn` | PASS |
| S-05 read-only | `WorkspaceConnectionReadOnlyTests` (8 methods, three causes each where applicable) | PASS |
| S-06 audit without personal data | `NoRow_CarriesTheAddressOrTheDomain` | PASS |
| S-08 antiforgery | `TheSaveWithoutAnAntiforgeryToken_IsRefused`, `TheSaveEndpoint_IsNotExemptFromAntiforgery` | PASS |
| S-09 log hygiene | `TheRejectedValue_IsNotLogged`, `ARefusedSave_LogsNoAddress`, `ASuccessfulSave_LogsNoAddress` | PASS |
| S-10 / S-11 nothing leaves the installation | `TheSave_CallsNothingOutsideTheInstallation` | PASS |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A school's Admin points the program at another school's domain | refused, nothing written, audited | `AnAddressOutsideTheDomain_IsRefused`, `AMismatchedStoredConnection_IsNotUsedToAcceptAForeignAddress` | PROTECTED |
| A crafted request carries its own domain | ignored; the row stays bound to the approved domain | `ADomainSentInTheRequest_IsIgnored` | PROTECTED |
| A subdomain is passed off as the domain | refused | `ASubdomainOfTheSchoolDomain_IsNotTheDomain` | PROTECTED |
| A save while the school is suspended or past its grace period | `409`, nothing written, audited | `WorkspaceConnectionReadOnlyTests` | PROTECTED |
| A Dean opens the settings | refused by the policy | `ThePolicy_RefusesADean` | PROTECTED (see F-1) |
| An anonymous visitor reaches the page or posts | redirected; antiforgery refuses the post | `AnAnonymousVisitor_IsSentToSignIn`, `TheSaveWithoutAnAntiforgeryToken_IsRefused` | PROTECTED |
| A restored wrong database leaves a connection for a foreign domain | reported, never used, never corrected silently | `WhenTheSavedDomainNoLongerMatches_ThePageSaysSoAndChangesNothing` | PROTECTED |
| Oversized or malformed input | `400` before any business logic, bounded before the regex | `WorkspaceConnectionValidationTests` | PROTECTED |

## 16. Repository Hygiene

`.gitignore` still covers `google_credentials.json`,
`dac-classroom-agent-*.json`, `classroom_cache.db` and `*.xlsx` (with the
versioned report templates excepted). No credential file, generated database or
export is tracked or staged. The change set contains source, tests, resources,
one migration and documentation only. No secret value was opened, printed or
quoted by this review.

## 17. Deviations

The four deviations the implementation reported were each assessed:

- **D-1 (the culture fix)** — accepted, and **security-positive**. See F-3.
- **D-2 (the added unit test)** — accepted: it closes the coverage gap
  TEST_WRITING recorded, and adds no production behaviour.
- **D-4 (the singleton column set explicitly)** — accepted: it reuses the
  US-005 line verbatim rather than inventing a second mechanism (AD-11), and the
  constraint it satisfies is the one that keeps the table at a single row.
- **D-5 (the fourth check as a shape check)** — accepted. Comparing a value with
  itself would have been dead code; spec VR-004 describes exactly what was
  implemented, and the branch is reachable from a hand-edited database, which is
  the case it exists for.

No undocumented security behaviour was found.

## 18. Findings

### F-1 — Minor — TEST_COVERAGE — SC-4 / TC-5

**Where:** `tests/…/Web/Security/WorkspaceConnectionAuthorizationTests.cs`.

**Observed:** the forbidden-role half of TC-5 is proven by evaluating the host's
real policy against a synthetic Dean principal, not by a Dean's HTTP request.

**Expected:** TC-5 asks for an allowed-role and a forbidden-role test per
protected endpoint.

**Risk:** low. The policy object under test is the production one, and the
endpoints are independently shown to declare that policy and to allow no
anonymous access, so the HTTP path is covered by construction. What is not
exercised is the end-to-end refusal of a real Dean session — which cannot exist
until US-012 creates Dean accounts.

**Required correction:** none in this Story. **US-012 must add the HTTP-level
forbidden-role test for this endpoint** when a Dean can first sign in.

**Loop-back:** none.

### F-2 — Informational — READ_ONLY_MODE — SC-5

A declared service write commits the **whole** `DbContext`, not one row: the
declaration is scope-wide by design in the US-007 mechanism. In this Story's
paths nothing else is ever staged when the audit row is written — the guard is
the first statement, and the reads before a refusal are untracked — and the
tests assert the connection table is byte-for-byte unchanged after every
refusal. Recorded so a later Story that stages a write before auditing a refusal
does not inherit the assumption.

### F-3 — Informational — ERROR_HANDLING — NFR-073

The implementation found and fixed a latent defect of the US-008 baseline: the
translated error page was rendered in the server's culture, because the culture
set by the localization middleware does not survive the unwinding of an
exception to the outermost middleware. `RequestCultureScope` restores it from
`IRequestCultureFeature` and puts the previous culture back on dispose; the
`AccountCultureProvider` decision is reused rather than made twice.

This review treats it as **security-positive**: a refusal a user cannot read is
a refusal they will work around. Verified in all three BR-025 causes. Nothing
else in the host relied on the previous behaviour.

### F-4 — Informational — AUDIT — SC-11

A signed-in Admin can write an unbounded number of refused-save audit rows by
repeatedly posting a rejected address. This is the intended trail — the control
exists precisely to record such attempts — and the rows carry no personal data
and are purged on their own timestamps (PC-11). No action; noted because it is
the only unbounded write this Story adds.

### F-5 — Informational — GOOGLE_ACCESS — SC-8

US-007 finding F-5 remains open: US-009 adds no real Google data port, so the
first Story that does (US-011) must prove its own read-only refusal there.

## 19. Positive Controls

Independently observed and verified:

- BR-020 enforced in `Application` and **proven against a crafted request**, not
  only against the form;
- the connection table physically cannot hold a key, a secret or a reference to
  one;
- `PermittedServiceWrite` still has exactly four members and gained no
  registration — the guarded write did not become a service write;
- the SC-4 anonymous list gained nothing, and the US-008 enumeration test still
  passes unchanged;
- no Google port, scope or call; no Control Plane call; no new outbound
  destination;
- no package, project reference or configuration setting was added;
- every audit row of this Story carries codes and identifiers only;
- validation runs on length-bounded input before any regular expression;
- the audit pairing invariant was relaxed in exactly one direction and the other
  half is still enforced, by constraint name.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 and OD-002 were
resolved by the Owner before SPECIFICATION and the implementation follows both.

## 21. Review Limitations

- The review did not re-execute the suite class by class; it observed the
  reported totals and re-ran the full suite once (1651 passed).
- No penetration testing and no fuzzing were performed.
- The vulnerability scan is a point-in-time nuget.org result.
- Concurrency was reasoned about, not stress-tested: the double first-save path
  is proven at the database level only, as TC-6 requires of timing-dependent
  behaviour.
- `runtime_checks: PARTIAL` accordingly.

## 22. Verdict Rationale

`PASS`. The build is clean, the suite is green with nothing skipped, every SC
item the scope touches is `PASS` with evidence, and no Critical or Major finding
exists. The one Minor finding is a limitation of what can be tested before
US-012 exists and carries a named owner. The Story is safe to put before
`HUMAN_PR_APPROVAL`.
