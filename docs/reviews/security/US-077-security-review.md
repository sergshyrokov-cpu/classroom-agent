---
artifact_type: security_review
story: US-077
version: 1
status: APPROVED
created_at: 2026-11-08T14:54:25Z
updated_at: 2026-11-08T14:54:25Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-077-diagnostics-before-pilot.md
    version: null
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
  - path: docs/designs/api/US-077-api-design.md
    version: 1
  - path: docs/designs/database/US-077-db-design.md
    version: 1
  - path: docs/tests/US-077-test-strategy.md
    version: 1
  - path: docs/tests/US-077-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-077-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-077 Security Review

## 1. Executive Summary

**Verdict: PASS.** No Critical, Major or Minor finding; two Informational observations.

The Story keeps Google access tokens in process memory, adds a closed-list failure kind to one log line, changes one
event id, orders one query and versions static file addresses. The principal new asset is the remembered token. It is
held only in a private in-memory structure, is bound to the exact (account, scope set) pair, and does not reach a log, a
failure, the database or a response. The key is not held between token requests: a credential exists only for one
token request.

This is a code, configuration and test review, not a penetration test.

## 2. Reviewed Artifacts

As listed in `inputs`. `openapi` and `entity_model` do not exist (API and DB design NOT_APPLICABLE). Also read:
`docs/reviews/implementation/US-077-implementation-verification.md`, `git diff`, the new code files, and the secrecy and
log tests.

## 3. Security-Relevant Scope

- **Endpoints and pages:** none added or removed on either host. Views change only in the version attribute.
- **Assets:** Google access tokens (new, in memory), the service-account key (read path changed), the technical account
  address (cache key), the Google sign-in failure details (must stay out of the log).
- **Trust boundaries:** application to Google token and Classroom endpoints; browser to the installation (OAuth callback
  failure path; static files).
- **Components:** `GoogleAccessTokenCache`, `GoogleClassroomReader.TokenAuthorizer` and `RequestTokenAsync`,
  `GoogleRetryHandler`, `GoogleSignInEvents`, `GoogleSignInFailureClassifier`, `SignInLog`, `RefusalLog`,
  `WorkspaceConnectionRepository`, both hosts' `_ViewImports`.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0, Testcontainers available; free memory 7 GB at start.

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` | exit 0; 7954 total, 7954 passed, 0 failed, 0 skipped; 15 min 0 s; no Docker errors in the log |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | no vulnerable package in any of the 7 projects, against nuget.org |

Delegated to a `cheap-worker`, facts only: the build and the start of the full run. The package and hygiene commands were
run here. All code reading and every judgement were made here.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role or policy changed. |
| SC-2 Authentication | NOT_APPLICABLE | Dean and Owner sign-in untouched. |
| SC-3 AllowedAdmin | PASS | `OnRemoteFailureAsync` changes only the log call; the check is not on this path. |
| SC-4 Authorization | PASS | No endpoint, `[AllowAnonymous]` or antiforgery exemption changed. Only `ScriptTagHelper` and `LinkTagHelper` are enabled, so the form tag helper stays off; the view diff holds nothing but `asp-append-version`. |
| SC-5 Read-only mode | PASS | No write use case changed; a token is requested only inside a read, which read-only mode does not start. |
| SC-6 No DB UI | NOT_APPLICABLE | None added. |
| SC-7 Key | PASS | `RequestTokenAsync`: the credential is a local of one token request; `Entry` holds a string and an expiry. `BetweenRequests_TheReaderHoldsTheToken_ButNothingOfTheKey`, `TheKey_IsResolvedOncePerTokenRequest_…`. |
| SC-8 Google | PASS | Scope constants not in the diff; subject still the technical account; `GoogleAccessProbe` unchanged (S-06). Pair key is ordinal (`KeyOf`), and each `TokenAuthorizer` uses only its own key. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | `CallbackFailedWithKind` writes an enum and the request id only. `GuardAsync` carries no exception text, including a `FormatException` from a malformed token value. `NeitherTheTokenNorTheKey_…`, `AfterARun_…`, `NothingTheReaderHolds_…`, and AC-007's checks through the real handler. |
| SC-11 Audit | PASS | Audit row unchanged and asserted (`GoogleSignInFailureKindTests`). No new audited action. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` untouched. |
| SC-13 Outbound | PASS | Same token endpoint; Classroom requests carry the bearer token only; the token client has no `beforeAttempt`. |

## 6. Authentication and Authorization

No access rule changed. The failed Google callback keeps its refusal, redirect, `TempData` key and audit row. The new
branch only selects the log line. No anonymous endpoint added.

## 7. Credentials, Key and Google Access

- The key is resolved from `ISecretStore` per token request and not kept; a reused token loads no key.
- A failed token request writes no entry; a refused (`401`) token is dropped only if the entry still holds it.
- A token is never sent for another pair (AC-002 tests decode subject and scopes from the signed assertion).
- Tokens with a zero or negative life are not sent (VR-002a); `S-01` is met by plain classes with no generated members.

## 8. Sensitive Data Exposure

Responses and views: nothing new. Logs: the kind and request id; the failure's message and `Data` are compared in memory
only. Exceptions: the closed list. Audit rows and exports: unchanged. Telemetry: `docs/hooks/tool-usage.jsonl` remains
git-ignored. The `?v=` value is a hash of a public file.

## 9. Input Validation

No request input added. Data from Google: empty token and non-positive life are refused (VR-001, VR-002a). See INFO-2 for
an out-of-range life. `context.Failure` is external and is only compared, never stored.

## 10. API Security

No endpoint or response changed. Versioned addresses are served under the same static file policy
(`AVersionedAddress_IsServedUnderThePagesPolicy`).

## 11. Persistence and Configuration

`OrderBy(c => c.Id)` only; tracking unchanged; parameterless. No migration, no `.csproj`, `.resx` or `appsettings` change
(`git status`). The token is in no database (`AfterARun_…`).

## 12. Logging, Audit and Telemetry

Exactly one application line for a failed callback; event id 2203 kept. `ReadOnlyWriteRefused` moves to 5290 with no
collision, so the two log events are now distinguishable. The framework's `Information` lines holding the failure
message remain filtered by the `Microsoft` → `Warning` override, unchanged.

## 13. Dependencies

No package or project reference added. Vulnerability scan: none found (section 4).

## 14. Security Test Coverage

| Property | Tests |
|---|---|
| Token bound to its pair | `DifferentAccounts_…`, `AccountsDifferingOnlyByCase_…`, `TheTwoScopeSets_…`, `AssertEveryRequestCarriedItsOwnPairsToken` |
| Key not held | `BetweenRequests_…`, `TheKey_IsResolvedOncePerTokenRequest_…` |
| Token not disclosed | `NeitherTheTokenNorTheKey_…`, `NothingTheReaderHolds_…`, `AfterARun_…` |
| Expired token not used or kept | `AnExpiredToken_…`, `ARefusalAfterExpiry_…` |
| Failure text not logged | `GoogleSignInFailureKindTests` |

No test reaches a live Google API: token and Classroom hosts are scripted. The key is `SyntheticServiceAccountKey`.

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Technical account replaced while a token is remembered | Old token not sent for the new account | ordinal pair key; AC-002 tests | PASS |
| Crafted callback with an attacker-chosen `error` or `state` | No attacker text in the log | closed-list enum; AC-007 tests | PASS |
| Token sent to another host via the token client | No bearer on the token request | token handler has no `beforeAttempt` | PASS |
| Key removed during a run | Noticed when a token is next needed | `ARemovedKey_…`; I-3, INFO-1 | PASS |

## 16. Repository Hygiene

Tracked `.xlsx` files are only the two blank templates in `docs/product/report-templates/`. Name matches on
`service account` are source files with no key. `google_credentials.json` and `classroom_cache.db` remain ignored. No
secret-like file in the change set.

## 17. Deviations

None from the approved security requirements.

## 18. Findings

**INFO-1 (Informational, SERVICE_ACCOUNT_KEY, SC-7).** `GoogleAccessTokenCache`. After the key is deleted following a
suspected leak (DC-5), the running installation keeps its remembered tokens for up to about 55 minutes. This is the
Owner-confirmed I-3; no code correction is required. A restart of the service empties them. Loop-back: none.

**INFO-2 (Informational, INPUT_VALIDATION).** `GoogleAccessTokenCache.Accept`. An `expires_in` too large for `TimeSpan` or
`DateTimeOffset` throws, and `GuardAsync` ends the read as `Unexpected` with no entry written. It fails closed, but this
was established by reading only; no test drives it, and the Specification does not require one. Loop-back: none.

## 19. Positive Controls

Plain token-holding classes; a credential scoped to one request; ordinal per-pair binding; conditional forget on `401`;
`GuardAsync` dropping all exception text; a closed-list enum in the log; only two tag helpers enabled.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

No penetration test. The real Google endpoints were not called (TC-4). The Story's manual check after the first kit
update stays with the Owner on the server.

## 22. Verdict Rationale

The build and all 7954 tests are green, every touched SC item is `PASS`, and no Critical or Major finding exists. The
open finding `US-077/IMPLEMENTATION_VERIFICATION/m-1` is about a test and stays open: reading the code shows no path that
crosses tokens (each `TokenAuthorizer` holds its own key), but this stage may not change the test.
