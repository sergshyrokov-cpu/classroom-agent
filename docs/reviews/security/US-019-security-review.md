---
artifact_type: security_review
story: US-019
version: 1
status: APPROVED
created_at: 2026-10-04T08:43:37Z
updated_at: 2026-10-04T08:43:37Z
produced_by: security-reviewer
inputs:
  - path: docs/evidence/US-019-implementation-report.md
    version: 1
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-api-design.md
    version: 1
  - path: docs/designs/database/US-019-db-design.md
    version: 1
  - path: docs/tests/US-019-ac-test-matrix.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 5
security_sensitive: true
runtime_checks: FULL
---

# US-019 Security Review — Trigger a synchronization from the UI

## 1. Executive Summary

**PASS.** 0 Critical, 0 Major, 0 Minor, 5 Informational.

The Story adds one state-changing endpoint, `POST /synchronization/requests`,
granted to Admin and Dean, which only enqueues an in-process request and writes
an audit row. Verified controls: its own policy (Admin + Dean) under the
deny-by-default fallback; global antiforgery; the US-012 restricted session in
front of it; the read-only guard as the first step of the use case, with the
refusal audited and no request enqueued; a return page chosen from the role, not
from the request; no Google call, no secret, no new outbound destination; audit
rows with ids and categories only, constrained by a new check constraint. Full
suite green, no vulnerable packages.

Limitation: this review ran in the same session as IMPLEMENTATION (§21).

## 2. Reviewed Artifacts

Front matter `inputs`, all current (none `SUPERSEDED`). `HUMAN_SPEC_APPROVAL`
recorded 2026-10-04.

## 3. Security-Relevant Scope

- **Installation host, public port:** new `POST /synchronization/requests`
  (`SynchronizationRequestController.Submit`); `GET /` and
  `GET /settings/workspace-connection` gain a form and a one-time message.
- **Assets:** audit rows; the synchronization trigger (a run reads school data
  from Google as the technical account — the trigger must not be usable by
  anyone outside Admin/Dean, nor in read-only mode); session cookie.
- **Trust boundaries:** browser → host; controller → `RequestSynchronizationUseCase`;
  use case → `ISynchronizationRequests` (in-process adapter over
  `SyncRunCoordinator`); application → PostgreSQL.
- **Not touched:** Control Plane, service channel, `Contracts`, Google ports,
  secret store, configuration.

## 4. Environment and Tools

.NET 10 SDK; Docker Desktop 29.8.0 with Testcontainers PostgreSQL.
Commands: `dotnet build ClassroomAgent.sln` (0/0), `dotnet test ClassroomAgent.sln`
(2835/2835, 0 skipped — the implementation report's run, re-checked against
`/tmp` output), `dotnet list ClassroomAgent.sln package --vulnerable` (no
vulnerable package in any of the 7 projects), `git status --ignored`.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | `StartSynchronization` = Admin + Dean, exactly the §2 row "Запуск синхронизации" ✔ ✔ (BR-004); no new role; no other cell changed (connection page stays Admin-only). |
| SC-2 Authentication | PASS | No change to sign-in; the Dean's restricted session redirects the new endpoint to `/sign-in/change-password` (`SynchronizationRequestAuthorizationTests.ADeanWithATemporaryPassword_…`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin sign-in unchanged. |
| SC-4 Authorization | PASS | `[Authorize(Policy = StartSynchronization)]` on the controller; policy registered in `InstallationSecurityServices`; no `[AllowAnonymous]`/`[IgnoreAntiforgeryToken]`; anonymous → `/sign-in`, missing token → `400` (tests); `HttpPost` only, `GET` requests nothing (`AGet_RequestsNothing`); US-008 endpoint enumeration and global antiforgery tests green with the new endpoint. |
| SC-5 Read-only | PASS | `RequestSynchronizationUseCase` calls `IReadOnlyModeGuard.EnsureAllowedAsync` first; on refusal only the audit row is written under `PermittedServiceWrite.AuditEvent`, then rethrown → `409`; port records 0 calls for all three BR-025 causes × both roles (`SynchronizationRequestRefusalTests`). The background run keeps its own read-only skip (US-013). |
| SC-6 No DB UI | NOT_APPLICABLE | Nothing added. |
| SC-7 Key | PASS | The use case reads no secret; no key, reference or token in any new type, view, log or row. |
| SC-8 Google | PASS | No Google call on the request path; the run it triggers is unchanged (US-013 … US-017). |
| SC-9 Channel | NOT_APPLICABLE | Untouched; an unusable connection refuses the press (OD-009 a) rather than enqueue a run that would be skipped. |
| SC-10 Hygiene | PASS | Logs: account id, request id, outcome / category only (`SynchronizationRequestLog`); `AnAcceptedPress_LogsOneInformationLine` asserts no email. No request body is bound, so nothing is echoed or logged. Errors through the existing handler. |
| SC-11 Audit | PASS | `AuditAction.SynchronizationRequested`; accepted and refused presses each write one row with actor id, role, outcome, category, request id, no target, no personal data (`SynchronizationRequestAuditTests`, `…RefusalTests`); `ck_audit_event_sync_request_shape` forbids a target, a non-user actor and foreign categories (`SynchronizationRequestAuditSchemaTests`). Rows are inserted only; no update/delete path added. |
| SC-12 Owner | NOT_APPLICABLE | No Control Plane change. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

| Caller | `POST /synchronization/requests` | Test |
|---|---|---|
| Anonymous | `302 /sign-in`, nothing enqueued/audited | `Anonymous_IsSentToSignIn_AndNothingHappens` |
| Dean, temporary password | `302 /sign-in/change-password` | `ADeanWithATemporaryPassword_…` |
| Dean | `302 /` (or `409`) | `SynchronizationRequestTests.DeanPress_…` |
| Admin | `302 /settings/workspace-connection` (or `409`) | `SynchronizationRequestTests.AdminPress_…` |

The redirect target is a constant chosen from `User.IsInRole(Dean)`; a submitted
`returnUrl` is ignored (`ASubmittedReturnUrl_IsIgnored`). The `409` re-render
shows the Admin the connection page (which the Admin may see under
`ConfigureWorkspaceConnection`) and the Dean the home page only — a Dean never
receives the connection page model.

## 7. Credentials, Key and Google Access

No password, key or Google access in scope. The press cannot cause a Google
call in read-only mode (refused before enqueue) and cannot cause more than one
queued run (coordinator remembers one request).

## 8. Sensitive Data Exposure

View models gain a boolean and a translation key; the Dean's home page gains no
run status, time or diagnosis (`DeanPress_TheHomePageShowsRequested_AndNothingAboutAnyRun`).
Audit rows and logs carry no email or domain. TempData carries only a
translation key (INFO-2).

## 9. Input Validation

The request model binds nothing (spec VR-001); extra fields are ignored.
Malformed input has no surface beyond the antiforgery token (`400`).

## 10. API Security

Only the approved operation exists; method `POST`; no response body beyond the
redirect or the page. The absent `/api/v1/sync` (spec I-4) is not shipped
(INFO-5).

## 11. Persistence and Configuration

One migration, `AddSynchronizationRequestAudit`: amends `ck_audit_event_action`,
adds `ck_audit_event_sync_request_shape`; no table or column; no
`EnsureCreated`. No configuration change. Two pinned schema tests were extended
by exactly the new elements (implementation report §7) — not a weakening.

## 12. Logging, Audit and Telemetry

Events 5140 (`Information`) and 5141 (`Warning`), ids only. Audit coverage as
SC-11 above. Hook telemetry unchanged and git-ignored.

## 13. Dependencies

No `.csproj` changed; no package or project reference added.
`dotnet list package --vulnerable`: none.

## 14. Security Test Coverage

Allowed-role (Admin, Dean) and forbidden cases (anonymous, restricted session,
no token, `GET`) per TC-5; read-only in `Application` with the port substituted,
3 causes × 2 roles; audit rows for accepted and refused presses; schema
constraint; no test reaches Google (TC-4). The three tests that passed before
implementation guard regressions and are backed by red tests for each AC
(test-generation report §6).

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Press in read-only mode by posting directly | `409`, nothing enqueued | refusal tests (port 0 calls) | PASS |
| Cross-site POST | `400` | antiforgery test | PASS |
| Open redirect via `returnUrl` | ignored | `ASubmittedReturnUrl_IsIgnored` | PASS |
| Dean with temporary password bypassing the forced change | redirected | restricted-session test | PASS |
| Flooding presses | at most one queued run; one audit row per press | coordinator tests; OD-004 accepted no limit | PASS (INFO-3) |
| Learning run diagnosis as Dean | nothing shown | Dean home test | PASS |

## 16. Repository Hygiene

`classroom_cache.db`, the `.xlsx` exports, `google_credentials.json` and
`dac-classroom-agent-*.json` are ignored (`!!`); none is staged or referenced.
No secret-like file in the change set.

## 17. Deviations

None against security requirements. Implementation-report deviations (page
builder refactor, TempData key, log ids, pinned tests) were checked and carry
no security effect.

## 18. Findings

| ID | Severity | Category | Where | Observation | Action |
|---|---|---|---|---|---|
| INFO-1 | Informational | OTHER | `Controllers/HomeController.cs` | Three `using` directives left unused after the refactor (`System.Security.Claims`, `Application.UseCases`, `Domain.Enums`); not flagged by `dotnet format`. | Optional tidy-up. |
| INFO-2 | Informational | DATA_EXPOSURE | `HomeController`, `WorkspaceConnectionController` | The TempData value is rendered as a translation key without an allow-list. TempData is a Data-Protection-encrypted cookie and the output is HTML-encoded, so it cannot be forged or inject markup. | None required. |
| INFO-3 | Informational | API_SECURITY | `RequestSynchronizationUseCase` | No rate limit (OD-004): each press writes one audit row; at most one run is queued. Audit growth is bounded by the retention purge. | None; accepted by OD-004. |
| INFO-4 | Informational | AUDIT | `RequestSynchronizationUseCase` | Enqueue precedes the audit commit; a failing commit leaves a requested run without its row (`500`). Documented in db-design §5. | None; by design. |
| INFO-5 | Informational | API_SECURITY | `api-conventions.md` API-4 | `POST /api/v1/sync` stays a reservation, not implemented (spec I-4). | Owner's documentation call. |

## 19. Positive Controls

Deny-by-default fallback + dedicated policy; global antiforgery; restricted
session ahead of authorization; read-only guard first, refusal audited under
the BR-026 audit permission; constant role-based redirect; DB check constraint
shaping the new audit rows; no Google, secret or outbound change; no new
dependency.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 … OD-010 resolved.

## 21. Review Limitations

- Performed in the same session that implemented the Story; independence rests
  on re-reading the code and on the tests, not on a separate reviewer.
- Code, configuration and test review only; no penetration testing, no running
  instance on a network interface.

## 22. Verdict Rationale

Build and full suite green with recorded evidence; every touched SC item PASS;
security-sensitive ACs (AC-005, AC-006, AC-009) covered by passing tests; no
Critical, Major or Minor finding; no open security decision. → **PASS**, next
stage `HUMAN_PR_APPROVAL`.
