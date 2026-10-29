---
artifact_type: security_review
story: US-067
version: 1
status: APPROVED
created_at: 2026-10-29T10:49:56Z
updated_at: 2026-10-29T10:49:56Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-067-admin-changes-dean-login.md
    version: null
  - path: docs/specifications/US-067-spec.md
    version: 1
  - path: docs/decisions/US-067-open-decisions.md
    version: 1
  - path: docs/designs/api/US-067-api-design.md
    version: 1
  - path: docs/designs/api/US-067-openapi.yaml
    version: 1
  - path: docs/designs/database/US-067-db-design.md
    version: 1
  - path: docs/designs/database/US-067-entity-model.md
    version: 1
  - path: docs/tests/US-067-test-strategy.md
    version: 1
  - path: docs/tests/US-067-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-067-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-067-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-067 Security Review — An Admin changes a Dean's login

## 1. Executive Summary

**Result: PASS.** 0 Critical, 0 Major, 0 Minor, 2 Informational.

The Story adds one Admin-only write, `POST /settings/deans/{deanId:long}/login`, a domain method
`AppUser.ChangeLogin`, a closed-list audit action `dean_login_changed` and its CHECK migration. The
principal controls hold and were checked in code and by tests: the controller-level
`ManageDeanAccounts` policy (Admin only), global antiforgery, the read-only refusal first in
`Application`, the school domain read server-side, creation's own address rules (shared helper, not a
copy), a uniqueness pre-check backed by the unique index, security-stamp rotation ending open
sessions, an audit row without either address, and no logging of the entered value. The independent
build is clean and the full suite is green (6882/6882). No new package.

Delegation: build, `dotnet list package --vulnerable --include-transitive`, the `.csproj` diff and the
full test run went to a `cheap-worker` subagent (facts only). Reading code and every judgement here were
done by the reviewer.

## 2. Reviewed Artifacts

As in front matter `inputs`. Specification v1 `APPROVED` (HUMAN_SPEC_APPROVAL recorded,
`history.jsonl`); none of the inputs is `SUPERSEDED`. Open Decisions OD-001, OD-002 resolved a).

## 3. Security-Relevant Scope

- **Installation host (Web):** new `DeanAccountsController.ChangeLogin` action; new per-row form on
  `/settings/deans` (`Views/DeanAccounts/Index.cshtml`).
- **Application:** `ChangeDeanLoginUseCase` (new), `DeanLoginAddress` (new shared helper, also used by
  `CreateDeanAccountUseCase`), `DeanAccountRefusal.LoginUnchanged`.
- **Domain:** `AppUser.ChangeLogin`, `AuditAction.DeanLoginChanged`, `AuditEvent.DeanAccountManaged`
  accepting it.
- **Infrastructure:** action code mapping and CHECK constraint; migration
  `20261029092536_AddDeanLoginChangedAuditAction` (Down refuses while such rows exist).
- **Assets:** Dean login (staff personal data), Dean password hash (untouched), session cookie / security
  stamp, audit rows.
- **Trust boundaries:** browser → installation host; controller → use case; application → PostgreSQL.
  No Google port, no Control Plane channel, no `Contracts` change.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 running (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6882 total, 6882 passed, 0 failed, 0 skipped, 10 min 16 s.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no vulnerable package in
  any of the 7 projects (sources: nuget.org, local SDK feed).
- `git diff --stat HEAD -- '*.csproj' Directory.Build.props Directory.Packages.props` — empty.
- Not performed: penetration testing, live browser check.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added; the operation is Admin-only per §2 matrix (v104) — `[Authorize(Policy = ManageDeanAccounts)]` on the controller (`DeanAccountsController.cs:24`). |
| SC-2 Authentication | PASS | Password, hash, temporary flag, counter, lockout untouched (`AppUser.ChangeLogin` sets only `Email`, `NormalizedEmail`, stamps); stamp rotated → open session refused on next request (`AnOpenSessionOfTheDean_EndsOnItsNextRequest`); old address refused as unknown login (audit category `unknown_login`); domain read from `LegitimacyState`, subdomain refused; an Admin's id is "no such Dean" and `ChangeLogin` throws for a non-Dean role (defence in depth). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin sign-in not touched. |
| SC-4 Authorization | PASS | Controller policy; `PermissionMatrixTests` row `POST settings/deans/{deanid}/login`; Dean `403` for any id incl. own, anonymous → `/sign-in`, missing antiforgery token refused (page tests). No anonymous route added; POST only. |
| SC-5 Read-only mode | PASS | `readOnlyMode.EnsureAllowedAsync` is the first call (`ChangeDeanLoginUseCase.cs`), refusal audited through `PermittedServiceWrite.AuditEvent` only; tests `InReadOnlyMode_TheGuardRefusesBeforeAnyRepositoryCall`, `InReadOnlyMode_TheChangeIsRefused_AndOnlyTheRefusalIsAudited` (all causes). No Google port in the path. |
| SC-6 No DB UI | NOT_APPLICABLE | Nothing added. |
| SC-7 Key | NOT_APPLICABLE | Key not touched. |
| SC-8 Google | NOT_APPLICABLE | No Google call. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | No logger call in the changed code; entered value not echoed on refusal (`ARefusedAddress_…` asserts absence in body); log-absence tests for rejected and successful values (see INFO-2 of verification, kept open). Refusals carry translated messages only. |
| SC-11 Audit | PASS | One `dean_login_changed` row, actor Admin, target account id, no address (`TheChange_IsAuditedOnce_WithoutEitherAddress`, `TheAuditRow_HoldsNeitherAddress`); validation refusals write none; CHECK extended by migration; Down refuses rather than drop rows (`Down_IsRefused_WhileALoginChangeRowExists`). |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts`/Control Plane change. |
| SC-13 Outbound | NOT_APPLICABLE | No outbound flow. |

## 6. Authentication and Authorization

| Caller | Expected | Evidence |
|---|---|---|
| Admin | allowed | page tests, `PermissionMatrixTests` |
| Dean (other id / own id) | `403` | `ADean_IsForbidden_ForAnotherAccountAndForTheirOwn` |
| Anonymous | redirect `/sign-in` | `AnAnonymousRequest_IsSentToSignIn` |
| Admin, id of an Admin / unknown id | `404`, nothing written | `AnUnknownIdAndAnAdminsId_Answer404`, `AnAdminsId_IsNoSuchDeanAccount_AndTheAdminsLoginStays` |

The actor id comes from the authenticated principal (`ActorId()`), never from the form. No foreign
identifier other than the route `deanId` is accepted; it is checked against Dean accounts only.

## 7. Credentials, Key and Google Access

The password and its hash are neither read nor changed; no password is returned. FR-012 (existing
password not re-checked against the new local part) is an approved requirement, not a gap. No key, no
Google access.

## 8. Sensitive Data Exposure

- View: the per-row form shows no value; the list already shows Dean logins to the Admin (US-012).
- DTOs: unchanged `DeanAccountActionOutcome` (refusal, id) — no entity in a signature (AD-8).
- Logs: no new log statement; tests assert neither address appears.
- Audit: ids only.
- Exceptions: a database conflict other than the unique email index surfaces through the single
  `IExceptionHandler` as the generic error page (INFO-1).

## 9. Input Validation

`newEmail` is bound as `string?`, `null` → empty → malformed; trimmed; length ≤ 254 (matches the column
`HasMaxLength(254)`), no whitespace, one `@`, non-empty local part, domain shape, exact school domain.
Route `deanId` constrained to `long`; non-numeric → no route (`ANonNumericId_MatchesNoRoute`). No other
field is read, so a role or state in the body has no effect.

## 10. API Security

Only the approved endpoint exists (api-design D-1). Status codes: `302` success, `400` malformed /
domain / taken, `409` unchanged and read-only, `404` no such Dean, `403` Dean — as designed. Error pages
carry no internals.

## 11. Persistence and Configuration

No column change; CHECK constraint updated by migration only (PC-2); unique index still backs uniqueness
and its violation is translated to `EmailAlreadyUsed` with uncommitted changes discarded
(`AnAddressTakenBetweenTheCheckAndTheSave_…`). No configuration change; no generated database file in
the change set; credential files remain git-ignored (`.gitignore:3`, `:15`).

## 12. Logging, Audit and Telemetry

Covered in §5 SC-10 / SC-11. `docs/hooks/tool-usage.jsonl` remains git-ignored (`.gitignore:66`).

## 13. Dependencies

No package or project reference added; vulnerability scan clean (§4).

## 14. Security Test Coverage

| Requirement | Test(s) |
|---|---|
| Admin only / Dean 403 / anonymous | page tests, `PermissionMatrixTests` |
| Antiforgery | `APostWithoutAnAntiforgeryToken_IsRefused` |
| Read-only in Application | use-case tests (guard before any repository call), page test per cause |
| Stamp rotation / session end | `AValidChange_RotatesTheSecurityStamp`, `AnOpenSessionOfTheDean_EndsOnItsNextRequest` |
| Credentials untouched | `AValidChange_LeavesEverythingElseOfTheAccountUntouched`, `AllElseOfTheAccountStays_WhenTheLoginChanges` |
| No address in audit / log | audit tests, two log-absence tests |
| Domain server-side, subdomain | `AnAddressOutsideTheSchoolDomain_…`, `WithNoSchoolDomainKnown_…` |
| Uniqueness incl. Admin, race | `AnAdminsAddress_IsAlreadyUsed_…`, `AnAddressTakenBetweenTheCheckAndTheSave_…` |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Dean changes own login to take another identity | `403` | page test | PASS |
| Admin hijacks an Admin's login via the Dean form | Admin id → `404`; domain method throws for non-Dean | tests + `AppUser.ChangeLogin` | PASS |
| Login set to an address of an Admin to collide with Google sign-in | uniqueness across roles | `AnAdminsAddress_IsAlreadyUsed_…` | PASS |
| Address outside the school / on a subdomain | server-side domain | tests | PASS |
| Write in read-only mode by direct POST | Application refusal, audited | tests | PASS |
| Old session keeps working after rename | stamp rotation | test | PASS |
| Cross-site POST | antiforgery | test | PASS |
| Rejected value leaks into logs / page | no echo, no log | tests | PASS |
| Concurrent Dean sign-in during the change | either the change or the sign-in hits the concurrency stamp; the sign-in retries and then finds no old address | code reading (`SignInDeanUseCase` retry, `UnitOfWork` `AppUserConflictException`) | PASS (INFO-1) |

## 16. Repository Hygiene

Change set: 14 modified + 4 new source files, 5 modified + 3 new test files, Story artifacts. No secret,
no `.env`, no database file, no `.xlsx` export, no IDE-local config among them.

## 17. Deviations

None security-relevant. The shared `DeanLoginAddress` helper (implementation INFO-1) is the spec's FR-002
"same code" requirement and is behaviour-preserving for creation (creation tests green).

## 18. Findings

- **INFO-1** (ERROR_HANDLING). `ChangeDeanLoginUseCase` does not catch `AppUserConflictException`: if the
  account row changes between the read and the save (for example the Dean signs in at that moment, or a
  second Admin acts on it), the request ends on the host's generic error page (`500`). Nothing is
  committed and no internals are shown; `SetDeanAccountStateUseCase` and `ResetDeanPasswordUseCase` behave
  the same. No correction required; a translated "the account changed, try again" answer would be a
  future usability improvement.
- **INFO-2** (OTHER). The login may be set to any free address of the school domain, including the
  technical account's address (`WorkspaceConnection` impersonation user); creation allows the same. A
  Dean login is a local password login, so it gains no Google access. No correction required.

## 19. Positive Controls

Controller policy + permission matrix; global antiforgery; read-only guard first and audited; server-side
domain; shared validation with creation; unique index + translated race; stamp rotation; credential
fields untouched; non-Dean guard in the domain method; audit without addresses; CHECK constraint with a
safe Down; no logging of the input.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus the full automated suite; no penetration test and no manual
browser session. The SC-10 log-absence evidence depends on the tests' log capture (see the open
verification finding INFO-2).

## 22. Verdict Rationale

Build clean, full suite green, every touched SC item PASS, no Critical, Major or Minor finding, no open
security decision. PASS → HUMAN_PR_APPROVAL. The open findings from earlier stages are outside this
review's responsibility and stay open: IMPLEMENTATION/INFO-1 (checked here — behaviour-preserving, no
security effect), IMPLEMENTATION_VERIFICATION/m-1 (functional test assertion) and INFO-2 (test
robustness).
