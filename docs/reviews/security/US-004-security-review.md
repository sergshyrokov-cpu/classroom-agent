---
artifact_type: security_review
story: US-004
version: 1
status: APPROVED
created_at: 2026-09-17T11:18:35Z
updated_at: 2026-09-17T11:18:35Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-004-suspend-resume-installation.md
    version: null
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/decisions/US-004-open-decisions.md
    version: 1
  - path: docs/designs/api/US-004-api-design.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/designs/database/US-004-db-design.md
    version: 1
  - path: docs/designs/database/US-004-entity-model.md
    version: 1
  - path: docs/tests/US-004-test-strategy.md
    version: 1
  - path: docs/tests/US-004-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-004-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-004 Security Review — Suspend and resume an Installation

## 1. Executive Summary

**PASS.** Four new Owner-only Control Plane endpoints (GET/POST
`/installations/{id}/suspension`, GET/POST `/installations/{id}/resumption`) and a
changed detail page. They are covered by the Owner policy, the global antiforgery
filter and the `guid` route constraint; the status change binds nothing from the
request, changes one column by a conditional update and writes its audit row in the
same transaction; the notice query value is matched against a closed set and never
echoed. No new anonymous endpoint, package, migration, outbound call or personal data.
No Critical, Major or Minor finding; three informational notes.

## 2. Reviewed Artifacts

As listed in `inputs`; `trebovaniya.md` v71; `security-conventions.md`.

## 3. Security-Relevant Scope

- **Host:** `ClassroomAgent.ControlPlane` only (private network, HTTPS, DC-6).
- **Assets:** `Installation.status` — the Owner's lever that stops a school
  (BR-023); Control Plane audit rows; installation name and domain (not personal
  data, still kept out of logs and audit, SC-10/SC-11).
- **Trust boundaries:** Owner browser → Control Plane controller → `ControlPlane.Services`
  → PostgreSQL. No installation, Google or other outbound boundary is crossed.
- **Components:** `InstallationStatusController`, `InstallationStatusService`,
  `InstallationStatusNotice`, `InstallationsController.Detail`,
  `Views/Installations/Detail.cshtml`, `Views/InstallationStatus/Confirmation.cshtml`,
  `AuditAction`/`AuditEvent`/`AuditEventConfiguration`, resx files, `Program.cs` DI.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 running; Testcontainers PostgreSQL.
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors (re-run by reviewer).
- `dotnet test --solution ClassroomAgent.sln --no-build` — 571 total, 571 passed,
  0 failed, 0 skipped (re-run by reviewer).
- `dotnet list ClassroomAgent.sln package --vulnerable` — both projects report no
  vulnerable packages for the configured sources.
- `git diff` on `*.csproj` and `appsettings*.json` — no changes.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added; Owner policy only. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Entries untouched; `Suspend_ChangesOnlyStatus_…` asserts they stay. |
| SC-4 Authorization | PASS | `[Authorize(Policy = OwnerSession.OwnerPolicy)]` on `InstallationStatusController`; no `[AllowAnonymous]` added (grep: only Setup, SignIn, Error, catch-all as before); `GlobalAntiforgeryFilter` validates every non-safe method with no exemption; state change only on POST; tests `InstallationStatusAuthorizationTests` (anonymous → `/sign-in`, before setup → `/setup`, role-less principal → `403`, route set GET/POST only) and `InstallationStatusAntiforgeryTests` (no token, foreign token → `400`; GET and PUT/PATCH/DELETE change nothing); US-001 enumeration tests green. |
| SC-5 Read-only mode | NOT_APPLICABLE | Control Plane has no read-only mode; this Story sets the status that later drives it (US-005 … US-007). |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | No key or key reference involved. |
| SC-8 Google | NOT_APPLICABLE | No Google access. |
| SC-9 Channel | NOT_APPLICABLE | Nothing sent to an installation; no `Contracts` change. |
| SC-10 Hygiene | PASS | Errors via existing error page; notice value never echoed (`Detail_NoticeNotMatchingOrUnknown_…` with a script payload); no new log statement; `InstallationNameAndDomain_NeverReachTheLogFile` green after implementation (now exercising the real endpoints); no `EnableSensitiveDataLogging`; no `Html.Raw` in views. |
| SC-11 Audit | PASS | `installation_suspended` / `installation_resumed` written in the change transaction only when one row changed (`InstallationStatusService.ChangeStatusAsync`); actor Owner id, target installation internal id; `InstallationStatusAuditTests` assert fields, absence for GET/refusals/unchanged, no name/domain/UUID, immutability. |
| SC-12 Owner | PASS | No reference to `ClassroomAgent.Domain`; no teaching data. |
| SC-13 Outbound | PASS | No HTTP client or outbound call added. |

## 6. Authentication and Authorization

| Endpoint | Owner | No session | No Owner role | Before setup |
|---|---|---|---|---|
| GET `/installations/{id}/suspension` | 200 / 302 notice | 302 `/sign-in` | 403 | 302 `/setup` |
| POST `/installations/{id}/suspension` | 302 | 302 `/sign-in` | 403 | 302 `/setup` |
| GET `/installations/{id}/resumption` | 200 / 302 notice | 302 `/sign-in` | 403 | 302 `/setup` |
| POST `/installations/{id}/resumption` | 302 | 302 `/sign-in` | 403 | 302 `/setup` |

The controller returns `Forbid()` when the Owner id claim is missing, before any
service call. Identifiers: the only request identifier is the installation UUID;
the Control Plane is single-tenant (one Owner), so no cross-account access exists.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

- Views render name and domain via Razor encoding; the notice text comes from a
  translation key selected in `InstallationStatusNotice.KeyFor`, the query value is
  compared, never rendered.
- DTO `InstallationStatusConfirmationDto` exposes identifier, name, domain, status —
  no internal key, no `updated_at` (AD-8).
- Audit rows: codes and internal ids only (test-verified).
- Logs: no new log statement; test-verified absence of name and domain.

## 9. Input Validation

- Route `{id:guid}` constraint; non-GUID → catch-all `404` (tested).
- POST bodies bind nothing; over-posted `status`, `name`, `domain`, `clientId`,
  `identifier`, `reason` are ignored (`PostedFields_AreIgnored_OnlyStatusChanges`).
- Query `notice`: exactly one value equal to the status-matching constant, ordinal
  comparison; repeated, unknown, upper-case and script values ignored (tested).
- Redirect targets are built from the route GUID and constants only — no
  open-redirect input.

## 10. API Security

Only the four approved operations exist (`StatusEndpoints_ExistAndNoneAllowsAnonymous`);
methods GET/POST; responses and redirect targets match `US-004-openapi.yaml`
(`Location` pattern includes only the two notice values). Unchanged outcome is `302`,
not an error, as approved.

## 11. Persistence and Configuration

- No schema change, no migration (matches db-design §6; `MigrationTests` green).
- `ExecuteUpdateAsync` filtered on `Id` and expected `Status`, setting `Status` and
  `UpdatedAt`; the audit insert follows in the same explicit transaction; zero rows →
  rollback, no audit. PostgreSQL row locking under READ COMMITTED re-evaluates the
  predicate for a waiting update (db-design §5.2). Concurrency tests green, repeated
  3× by the implementor.
- Existing triggers still protect identifier/domain immutability and no-delete.
- No configuration file changed; no generated database file or export in the change set.

## 12. Logging, Audit and Telemetry

Covered in §5 SC-10/SC-11. `docs/hooks/tool-usage.jsonl` not part of the change set.

## 13. Dependencies

No package or project reference added. Vulnerability scan: none reported for current
sources.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| Allowed + forbidden role per endpoint (TC-5) | `InstallationStatusAuthorizationTests` |
| Anonymous enumeration (TC-5) | US-001 `AnonymousEndpointTests`, `StatusEndpoints_ExistAndNoneAllowsAnonymous` |
| Antiforgery on every POST (TC-5) | `InstallationStatusAntiforgeryTests`, US-001 `AntiforgeryTests` |
| GET changes nothing (TC-5) | `GetRequests_WithQueryValues_ChangeNothing`, confirmation tests |
| Audit (SC-11) | `InstallationStatusAuditTests` |
| Concurrency / no audit-without-change | `InstallationStatusUnchangedTests.Concurrent*` |
| No internals / echo (TC-3, SC-10) | notice tests, log test |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Cross-site form auto-submitting a suspend | antiforgery `400` | `Post_WithoutToken_…`, `Suspend_WithTokenFromAnotherSession_…` | protected |
| Suspend via a crafted GET link (e.g. image tag) | GET changes nothing | confirmation GET tests | protected |
| Unauthenticated suspend of a known UUID | `302 /sign-in`, no change | authorization tests | protected |
| Replayed / double-clicked confirmation producing duplicate or phantom audit | conditional update, audit only on change | `SameConfirmationSubmittedTwice_…`, concurrent tests | protected |
| Stale tab turning a resume into a suspend | separate endpoints, fixed target | `StaleResumeAfterResumeElsewhere_DoesNotSuspend` | protected |
| Over-posting to rename or re-domain the school | nothing bound | `PostedFields_AreIgnored_OnlyStatusChanges` | protected |
| Reflected content via `notice` | closed set, never echoed | `Detail_NoticeNotMatchingOrUnknown_…` | protected |

## 16. Repository Hygiene

Live credential files and prototype exports/database are present in the root and
git-ignored (confirmed with `git status --ignored` / `git check-ignore`; not opened).
No secret-like file in the change set.

## 17. Deviations

None security-relevant. Implementation report deviations (method name, detail page
model wrapper, explicit `updated_at`) were checked and match the approved contract.

## 18. Findings

| ID | Severity | Category | Finding | Action |
|---|---|---|---|---|
| INF-1 | Informational | OTHER | Review ran in the same session as the implementation; independence is procedural, not personal. | Human look at `InstallationStatusService.ChangeStatusAsync` and `InstallationStatusController` advised at HUMAN_PR_APPROVAL. |
| INF-2 | Informational | TEST_COVERAGE | Concurrency tests show no anomaly over 5 sessions / 5 rounds but cannot force every interleaving; the guarantee rests on the reviewed conditional update. | None. |
| INF-3 | Informational | AUTHORIZATION | Until US-005/US-006 are delivered the status has no effect on a running installation, so suspension is not yet an effective stop. Known and accepted by the Story scope. | None in this Story. |

## 19. Positive Controls

Owner policy on the controller; global antiforgery without exemptions; `guid` route
constraint; no body binding; closed-set notice matched to current status; conditional
update with audit in one transaction; audit rows immutable (US-001 trigger);
Razor encoding; no new anonymous endpoint, package, migration or outbound call.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Code, configuration and test review with a full test run; no penetration test.
- Vulnerability scan limited to NuGet advisory data of the configured sources.

## 22. Verdict Rationale

Build and 571 tests green (independently re-run); every touched SC item PASS; all
security-relevant Acceptance Criteria (AC-006, AC-008 … AC-010) proven by tests;
no Critical, Major or Minor finding. Verdict **PASS**; next stage HUMAN_PR_APPROVAL.
