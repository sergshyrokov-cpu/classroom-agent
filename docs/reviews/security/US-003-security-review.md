---
artifact_type: security_review
story: US-003
version: 1
status: APPROVED
created_at: 2026-09-17T10:35:00Z
updated_at: 2026-09-17T10:35:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-003-manage-allowed-admins.md
    version: null
  - path: docs/specifications/US-003-spec.md
    version: 1
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
  - path: docs/designs/api/US-003-api-design.md
    version: 1
  - path: docs/designs/api/US-003-openapi.yaml
    version: 1
  - path: docs/designs/database/US-003-db-design.md
    version: 1
  - path: docs/designs/database/US-003-entity-model.md
    version: 1
  - path: docs/tests/US-003-test-strategy.md
    version: 1
  - path: docs/tests/US-003-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-003-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 3
security_sensitive: true
runtime_checks: PARTIAL
---

# US-003 Security Review — Manage AllowedAdmin entries

## 1. Executive Summary

**Result: PASS** — 0 Critical, 0 Major, 1 Minor, 3 Informational.

The Story adds Owner-only Control Plane pages that store school employees' emails
(`allowed_admin`). Verified controls: every new route sits behind the `Owner`
policy with no anonymous or antiforgery exemption; both state changes are POST
under the global antiforgery filter; entries are reachable only through their own
installation (no cross-installation access); the email must be in exactly the
installation's domain, enforced in the service and by a database trigger;
uniqueness holds under concurrency via a unique index; revocation deletes the
email with the row; audit rows carry internal ids only, in the same transaction;
no log statement or audit column carries an email. Build and the full suite are
green (499/499) per the implementation report; no dependency or project reference
changed; no vulnerable package reported.

Remaining risk is low: the lost-race path's log output is statically clean but not
proven by a test (MIN-1, same as US-002 MIN-1). Next: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As in `inputs` above, plus `trebovaniya.md` v70 (§2, §3 AllowedAdmin, §5 audit,
§9), `security-conventions.md` (SC-2, SC-3, SC-4, SC-10, SC-11, SC-12, SC-13),
`AGENTS.md`, and the changed files under `src/ClassroomAgent.ControlPlane/` and
`tests/ClassroomAgent.Tests/`.

## 3. Security-Relevant Scope

- **Host:** Control Plane only (private network, HTTPS).
- **Functionality:** Admins section on `GET /installations/{id}`;
  `GET /installations/{id}/admins/new`; `POST /installations/{id}/admins`;
  `GET|POST /installations/{id}/admins/{adminId}/revocation`.
- **Assets:** AllowedAdmin emails (personal data of school staff); the entry list
  itself (decides who may configure a school — SC-3); Control Plane audit rows.
- **Trust boundaries:** Owner browser → Control Plane controller →
  `AllowedAdminRegistry` → PostgreSQL. No Google port, no service channel, no
  outbound call.
- **Components:** `AllowedAdminsController`, `AddAllowedAdminRequest`,
  `AllowedAdminEmailAttribute`, `AllowedAdminRegistry`, `InstallationRegistry.GetAsync`,
  `AllowedAdmin` + configuration + migration `AddAllowedAdmin`, `AuditEvent`
  factories, views, translations.

## 4. Environment and Tools

| Item | Value |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | available (used by the implementation run) |
| `dotnet list ClassroomAgent.sln package --vulnerable` | no vulnerable packages in either project (nuget.org source) |
| Static review | changed source, views, migration SQL, configuration, `.gitignore` |
| Test evidence | implementation report: build 0/0, 499/499 pass, format clean |
| Not run by the reviewer | the test suite itself (relied on the report plus inspection of the tests) |

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | no role added; Owner policy only |
| SC-2 Authentication | NOT_APPLICABLE | no credential or session change; the Owner's 30-minute idle timeout is unchanged (see INF-2) |
| SC-3 AllowedAdmin | PASS (Control Plane side) | entries stored per installation, domain-bound (`AllowedAdminRegistry.AddAsync` + `trg_allowed_admin_domain_match`), lower case (`ck_allowed_admin_email_lower`), no password column; the per-login check itself is US-008 |
| SC-4 Authorization | PASS | `[Authorize(Policy = OwnerSession.OwnerPolicy)]` on `AllowedAdminsController`; no `[AllowAnonymous]`/`IgnoreAntiforgery` added (grep); routes `guid`-constrained; POST only for changes; enumeration tests (`AnonymousEndpointTests`, `AntiforgeryTests`) reach the new endpoints via `HostEndpoint.SamplePath` |
| SC-5 Read-only mode | NOT_APPLICABLE | Control Plane has no read-only mode |
| SC-6 No DB UI | PASS | no diagnostic endpoint added |
| SC-7 Key | NOT_APPLICABLE | no key or reference touched |
| SC-8 Google | NOT_APPLICABLE | no Google access |
| SC-9 Channel | NOT_APPLICABLE | no channel change |
| SC-10 Hygiene | PASS (MIN-1) | no logger in new code; no `EnableSensitiveDataLogging` / `Include Error Detail`; views use Razor encoding (no `Html.Raw`); `RejectedAddedAndRevokedEmails_NeverReachTheLogFile` |
| SC-11 Audit | PASS | `AuditEvent.AllowedAdminAdded/Revoked` — owner actor, target `allowed_admin` + internal id, succeeded, request id; written in the change's transaction; refusals write nothing; `AllowedAdminAuditTests` incl. JSON scan for emails and UUIDs |
| SC-12 Owner | PASS | no `Contracts` or `Domain` reference; csproj unchanged |
| SC-13 Outbound | PASS | no outbound call |

## 6. Authentication and Authorization

- All five operations: Owner policy. Unauthenticated → `302 /sign-in`; principal
  without the `Owner` role → `403`; before setup → `302 /setup`
  (`AllowedAdminAuthorizationTests`, 18 cases).
- **Object-level access:** `GetRevokeConfirmationAsync` joins the entry to the
  installation named in the route; `RevokeAsync` selects the id with the same
  condition and deletes by that id inside the transaction. An entry id combined
  with another installation's id is `404` on GET and POST, nothing deleted
  (`UnknownInstallationOrEntry_OrEntryOfAnotherInstallation_Returns404_ChangesNothing`).
- Add: the installation is resolved from the route; `installationId`,
  `addedBy`, `identifier`, `addedAt` in the form are not bound
  (`AddAllowedAdminRequest` has only `Email`; `Add_OverpostedFields_AreIgnored`).
- `addedBy` comes from the session claim (`OwnerSession.OwnerId`), not input.

## 7. Credentials, Key and Google Access

No password, token or key is stored or handled. `allowed_admin` has no credential
column (`AllowedAdminColumns_MatchDesign_NoPasswordOrSecretColumn`). No Google
scope or port involved.

## 8. Sensitive Data Exposure

| Surface | Result |
|---|---|
| Views | emails, installation name and domain HTML-encoded (Razor default; apostrophe encoded — `Detail_EmailWithApostrophe_IsHtmlEncoded`; markup refilled safely — `RejectedValues_AreHtmlEncodedWhenRefilled`) |
| DTOs | `AllowedAdminItemDto` (identifier, email, added-at), no internal id or added-by; confirmation DTO minimal |
| 404 page | contains no email (`…Returns404_ChangesNothing` asserts) |
| Logs | no application log line in new code; lost-race EF log statically without values (MIN-1) |
| Audit rows | internal ids only (`AuditRows_CarryNoEmailInstallationValuesOrIdentifiers`) |
| Exports / telemetry | none added |
| Fixtures | synthetic `*.example.test` addresses (TC-4) |

Revocation deletes the email with the row (`ExecuteDeleteAsync`; `Delete_IsAllowed`,
`Revoke_DeletesOnlyThatEntry…`), satisfying `trebovaniya.md` §5.

## 9. Input Validation

- `email`: `Required` + `AllowedAdminEmailAttribute` (254 total, one `@`, name part
  1–64 ASCII `[A-Za-z0-9._'-]`, dot rules), nothing trimmed; domain equality after
  lower-casing in the service; `409` on duplicates. 16 format cases, 6 foreign
  domains, boundaries 64/65 and 255 (`AllowedAdminValidationTests`).
- Unknown installation checked before validation (no information beyond `404`).
- Defence in depth in the database: `ck_allowed_admin_email_format`,
  `ck_allowed_admin_email_lower`, `uq_allowed_admin_installation_email`, domain-match
  trigger, no-update trigger (`AllowedAdminSchemaTests`).
- Oversized input: the 254 limit rejects long values before any query; the form
  post is bounded by ASP.NET Core's default form limits.

## 10. API Security

Only the contract's routes exist (`AllowedAdminEndpoints_ExistAndNoneAllowsAnonymous`
asserts the exact pattern list; no PUT/PATCH/DELETE). Errors are the host's error
page or the form; no internals. The confirmation GET changes nothing
(`GetRequestsWithFormValues_ChangeNothing`). Both POSTs refuse a missing or
foreign-session token with `400` (`AllowedAdminAntiforgeryTests`).

## 11. Persistence and Configuration

- Migration `AddAllowedAdmin` only; no `EnsureCreated`. FKs RESTRICT to
  `installation` and `owner`; triggers created and dropped symmetrically in
  `Up`/`Down`.
- Control Plane database only (AD-1). No configuration or `appsettings` change.
- Generated files (`classroom_cache.db`, `.xlsx`, live credential files) remain
  git-ignored (`git status --ignored`, `git check-ignore`); not opened.

## 12. Logging, Audit and Telemetry

- No `ILogger` use in `AllowedAdminsController`, `AllowedAdminRegistry` or the views.
- Add and revoke each write one audit row; double and concurrent revocation write
  exactly one (`Revoke_SubmittedTwice…`, `ConcurrentRevocations…`); refusals none.
- Audit row of a revoked entry cannot be updated or deleted
  (`AuditRowOfRevokedEntry_CannotBeUpdatedOrDeleted`).
- Hook telemetry untouched.

## 13. Dependencies

No package or project reference changed (`git diff HEAD -- '*.csproj'` empty).
Vulnerability scan: none reported for current sources.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| Allowed / forbidden role per endpoint (TC-5) | `AllowedAdminAuthorizationTests` |
| Anonymous enumeration (TC-5) | `AnonymousEndpointTests`, `AllowedAdminEndpoints_ExistAndNoneAllowsAnonymous` |
| Antiforgery on every non-GET (TC-5) | `AntiforgeryTests.EveryNonGetEndpoint_WithoutToken_Returns400`, `AllowedAdminAntiforgeryTests` |
| Object-level access | `AllowedAdminRevocationTests` (foreign entry) |
| Domain binding | `AllowedAdminValidationTests`, `EmailOutsideInstallationDomain_IsRefusedByTrigger` |
| Audit (SC-11) | `AllowedAdminAuditTests` |
| No personal data in logs (SC-10) | `RejectedAddedAndRevokedEmails_NeverReachTheLogFile` (not the race path — MIN-1) |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Revoke another school's Admin by pairing an entry id with a different installation id | `404`, nothing deleted | registry join + test | PASS |
| Add a personal Gmail or another school's address as Admin (would let an outsider configure the school once US-008 exists) | `400 WrongDomain`; trigger refuses direct insert | service check + trigger + tests | PASS |
| Case or Unicode variation to create a duplicate entry | lower-casing, unique index | `Add_EmailAlreadyAnEntry_AnyCase…`, concurrent test; INF-3 | PASS |
| Forged `addedBy` / `identifier` in the form | not bound | `Add_OverpostedFields_AreIgnored` | PASS |
| Cross-site request revoking an Admin | antiforgery + POST only | antiforgery tests | PASS |
| Repeated revocation to create misleading audit history | one row only | double/concurrent tests | PASS |
| Markup in the email to inject script into the Owner's page | encoding; format rules | encoding tests | PASS |

## 16. Repository Hygiene

No secret-like file in the change set. Live credential files and generated
database/exports are ignored and were not opened. `TestResults/` is not tracked.

## 17. Deviations

- The implementor corrected a story-level test (clock advance 3 h → 10 min).
  Reviewed: the original advance exceeded the Owner's 30-minute idle timeout
  (SC-2, `trebovaniya.md` v68), so the test could not pass against correct code;
  the assertions were not weakened. Accepted (INF-2).
- `AllowedAdminRegistry` has no list method; the list lives in
  `InstallationRegistry.GetAsync` as the entity model states. No security effect.

## 18. Findings

### MIN-1 — Lost-race log output not proven by a test
- **Severity:** Minor · **Category:** LOGGING · **SC:** SC-10
- **Affected:** `AllowedAdminRegistry.AddAsync` catch path; EF Core update logging.
- **Evidence:** on a lost race EF Core logs the failed save. Statically no values:
  sensitive data logging is off and Npgsql error detail is not enabled (grep finds
  no `EnableSensitiveDataLogging` / `Include Error Detail`), so the PostgreSQL
  `DETAIL` with the key value is redacted. The log test covers only non-racing
  paths.
- **Expected:** no email in any log line.
- **Risk:** low — a future change enabling error detail would put emails into logs
  unnoticed.
- **Correction (optional):** extend the log test with a concurrent duplicate add.
- **Loop-back:** none required · **Verification:** the log test after a race.

### INF-1 — Review in the same session as implementation
- **Category:** OTHER. The review ran in the same conversation that wrote the
  code. Independence is limited; a human look at `AllowedAdminsController` and
  `AllowedAdminRegistry.RevokeAsync` at `HUMAN_PR_APPROVAL` is advised.

### INF-2 — Test corrected during implementation
- **Category:** TEST_COVERAGE. See §17; correction is consistent with SC-2 and does
  not weaken the test.

### INF-3 — Non-ASCII look-alikes in the domain part fold to ASCII
- **Category:** INPUT_VALIDATION. `ToLowerInvariant` maps a few non-ASCII
  characters (e.g. the Kelvin sign U+212A) to ASCII letters, so such input in the
  domain part can match the installation's domain. The stored email is then the
  correct ASCII address (name part is validated as ASCII; the database format check
  enforces ASCII), so no foreign or spoofed address can be stored. No action.

## 19. Positive Controls

Owner policy on the controller; global antiforgery; POST-only changes; `guid` route
constraints; object-level join on installation; server-side domain binding plus
trigger; lower-case and format checks in the database; unique index with conflict
mapping; physical delete of the email; audit in the same transaction with internal
ids only; no logging of emails; Razor encoding; no dependency change.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- The reviewer did not re-run the test suite; build and test evidence come from the
  implementation report (commands and counts recorded there) and inspection of the
  tests.
- No penetration testing; static, configuration and test review only.
- The race-path log (MIN-1) verified statically only.

## 22. Verdict Rationale

No Critical or Major finding; every touched SC item is PASS; required security tests
exist and pass per the recorded evidence; no blocking Open Decision. MIN-1 and the
informational notes are non-blocking. Verdict **PASS** → `HUMAN_PR_APPROVAL`.
