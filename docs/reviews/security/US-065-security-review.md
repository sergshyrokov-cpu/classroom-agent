---
artifact_type: security_review
story: US-065
version: 1
status: APPROVED
created_at: 2026-10-28T14:28:31Z
updated_at: 2026-10-28T14:28:31Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/decisions/US-065-open-decisions.md
    version: 2
  - path: docs/designs/api/US-065-api-design.md
    version: 1
  - path: docs/designs/api/US-065-openapi.yaml
    version: 1
  - path: docs/designs/database/US-065-db-design.md
    version: 1
  - path: docs/designs/database/US-065-entity-model.md
    version: 1
  - path: docs/tests/US-065-test-strategy.md
    version: 1
  - path: docs/tests/US-065-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-065-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-065-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-065 Security Review — An Admin enters an erasure exclusion by email before the person is loaded

## 1. Executive Summary

Result: **PASS** — 0 Critical, 0 Major, 0 Minor, 2 Informational.

The Story adds one Admin-only POST endpoint (`/settings/erasure/exclusions`), a use case that stores only a salted
HMAC fingerprint of the entered email plus one audit row, a synchronization step that matches roster emails against
those fingerprints and fills the exclusion's Google user id once, and a migration that replaces the immutability
check of `erased_participant` with a fill-only trigger.

Principal controls verified: controller-level `EraseParticipantData` policy (Admin only), global antiforgery with no
exemption, read-only guard before validation with an audited refusal under the BR-026 audit write, the email never
stored, logged or audited in clear, the fill-only trigger at database level, and no new package or outbound flow.

An independent full run in this review was green in one pass (6738/6738), which closes the gap the implementation
report left open (IMPLEMENTATION/INFO-1).

## 2. Reviewed Artifacts

As listed in the front matter; none is `SUPERSEDED`. Code: the 35 modified and 15 untracked files of the working tree
(`git status`), all inside the Story's change set as traced in the implementation report §4.

## 3. Security-Relevant Scope

- **Installation host, public:** `POST /settings/erasure/exclusions` (form field `email`); the erasure page `GET
  /settings/erasure` gains the entry form and the "already loaded" / "already excluded" blocks.
- **Background:** synchronization course transaction (`RunSynchronizationUseCase.AddMatchedInAdvanceAsync`).
- **Assets:** the entered email (personal data of a possibly minor student), the erasure salt, exclusion rows, audit
  rows, names and emails of already-loaded participants shown back to the Admin.
- **Trust boundaries:** browser → installation host; controller → Application use case; Application → PostgreSQL;
  Google roster data → synchronization (emails consumed only as fingerprint candidates).

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available); ~5.7 GB free RAM at start.
- `dotnet build ClassroomAgent.sln` — 0 errors, 0 warnings.
- `dotnet test ClassroomAgent.sln` — 6738 total, 6738 passed, 0 failed, 0 skipped (9 min 40 s), one run.
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable packages in any of the 7 projects.
- Delegation: none; the change set was small enough to read directly.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added; `PermissionMatrixTests` gains the cell `POST settings/erasure/exclusions → EraseParticipantData`, the §2 row extended in v104. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in or password change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Sign-in unchanged. |
| SC-4 Authorization | PASS | `[Authorize(Policy = InstallationPolicies.EraseParticipantData)]` on `ErasureController`; no `[AllowAnonymous]`, no antiforgery exemption; `ErasureExclusionEntryAccessTests`: Admin allowed, Dean 403 with nothing changed, Dean with temporary password redirected, anonymous to sign-in, no token → 400 with nothing changed, GET stores nothing. |
| SC-5 Read-only mode | PASS | `EnterErasureExclusionUseCase.ExecuteAsync` calls `IReadOnlyModeGuard.EnsureAllowedAsync` first, audits the refusal under `PermittedServiceWrite.AuditEvent` and rethrows → 409; tests for every cause, including an invalid email still 409. Synchronization's new step runs inside the existing run, already gated. No Google port in the use case. |
| SC-6 No DB UI | NOT_APPLICABLE | Nothing added. |
| SC-7 Key | NOT_APPLICABLE | No key handling. |
| SC-8 Google | PASS | No new Google call or scope; roster emails already read are only fingerprinted. |
| SC-9 Channel | NOT_APPLICABLE | Unchanged. |
| SC-10 Hygiene | PASS | No logger call in the use case or the new action; `ErasureExclusionEntryLoggingTests` assert neither the email (entered / invalid / loaded) nor the fingerprint reaches a log line; the route carries no personal data. |
| SC-11 Audit | PASS | `AuditEvent.ErasureExclusionEntered` — actor Admin, target the exclusion id, no email (v104 list); refusal via `ErasureRefused` with no target; DB check `ck_audit_event_exclusion_entry_shape`; audit repository stays add-only (see INFO-1). |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts` change. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

The §2 matrix row (v104) gives entering an exclusion to the Admin only. The new action inherits the controller policy;
the endpoint enumeration (`TheEntryPath_IsAProtectedEndpoint_WithTheErasurePolicy`, `PermissionMatrixTests`) would
fail on an unlisted anonymous route. No identifier is accepted from the request — only the email — so there is no
cross-object access path. Findings: none.

## 7. Credentials, Key and Google Access

No password, key or scope. The salt is created with `IErasureSaltStore.GetOrCreateAsync` inside the entry transaction
and rolled back on "nothing to enter"; it never leaves the database. Findings: none.

## 8. Sensitive Data Exposure

- **Storage:** only `EmailFingerprint.Of` (HMAC-SHA256 with the installation salt over the normalized email) is
  stored; `ErasedParticipant.EnterInAdvance` has no email field.
- **Responses:** "already loaded" lists name, email and course counts of loaded participants with that email — the
  same fields and role as the US-038 search (INFO-2). "Invalid" echoes the typed value back to its own submitter only
  (Razor-encoded).
- **Logs, audit, exceptions:** no email or fingerprint; `NothingToEnterException` is private and never escapes the use
  case.
- **Exports, telemetry:** untouched.

## 9. Input Validation

The email is trimmed and checked by the shared `LiftErasureExclusionUseCase.Validate` (required, max length,
one `@` with both parts, no whitespace) before any database access; failures answer 400 with a translated field error.
The form also sets `maxlength="254"`. Roster emails from Google are fingerprinted only when non-blank (VR-004).
Findings: none.

## 10. API Security

Only the endpoint in the approved OpenAPI exists; POST with antiforgery; Post-Redirect-Get on success; no internal
detail in the 400/409 answers (host error handling unchanged). Findings: none.

## 11. Persistence and Configuration

- Migration `AddErasureExclusionInAdvance`: `google_user_id` nullable with the "id or fingerprint" check; the
  immutability check replaced by trigger `tg_erased_participant_fill_only`, which rejects every update except a NULL
  id gaining a value with fingerprint, `erased_at`, `created_at` and `id` unchanged (`check_violation`). This is
  stricter than before at the row level and is proven by `ErasureExclusionInAdvanceSchemaTests`.
- The fill is a guarded `ExecuteUpdate … WHERE id = @id AND google_user_id IS NULL`, parameterized by EF Core.
- Down refuses while unmatched exclusions exist rather than deleting them (would undo an erasure). The
  IMPLEMENTATION_VERIFICATION m-1 case (Down after a fill fails on re-adding the old check) is rolled back and loses
  nothing; no security impact.
- No configuration change; no generated database or export in the change set.

## 12. Logging, Audit and Telemetry

See SC-10 and SC-11. Hook telemetry untouched.

## 13. Dependencies

No `.csproj` changed; no new package or project reference. Vulnerability scan clean (section 4).

## 14. Security Test Coverage

| Requirement / abuse case | Test |
|---|---|
| Admin allowed, Dean forbidden, anonymous redirected | `ErasureExclusionEntryAccessTests` |
| Antiforgery enforced, GET stores nothing | `ErasureExclusionEntryAccessTests` |
| Read-only 409, only the refusal audited | `ErasureExclusionEntryAccessTests`, `EnterErasureExclusionUseCaseTests` |
| Email and fingerprint never logged | `ErasureExclusionEntryLoggingTests` |
| Fill-only trigger exists and rejects a second update | `ErasureExclusionInAdvanceSchemaTests` |
| Audit row shape | `InstallationAuditEventSchemaTests`, use case tests |
| Excluded person never stored by synchronization or Meet | `ErasureExclusionInAdvanceSyncTests` |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Dean posts the entry directly | 403, nothing stored | access tests | PASS |
| Cross-site form post | 400 without token | access tests | PASS |
| Entry in read-only mode | 409 before validation, refusal audited | access + unit tests | PASS |
| Entry used to probe whether an email is loaded | Admin-only; same information as the Admin's search | code review | PASS (INFO-2) |
| Re-pointing an exclusion to another person by a later update | trigger rejects any second update | schema tests | PASS |
| Oversized / malformed email | 400, nothing stored | `Validate`, page tests | PASS |

## 16. Repository Hygiene

`classroom_cache.db`, `.xlsx` exports and `google_credentials.json` remain git-ignored (`git status --ignored`); not
opened. No secret-like file in the change set.

## 17. Deviations

Implementation deviations D-1 … D-6 reviewed for security impact:

- D-1 (architecture test narrowed): sound — see INFO-1.
- D-2 (adapted registry tests), D-3 (stored-id lookup reuse), D-4 (salt read per course transaction), D-5 (read-only
  notice text), D-6 (Down refusal): no security impact.

## 18. Findings

**INFO-1** — AUDIT, SC-11 — `tests/ClassroomAgent.Tests/Architecture/AuditDeletionConfinementTests.cs`.
The `ExecuteUpdate` ban is now waived for the whole file `ErasedParticipantRepository.cs`; the companion test
`TheExclusionFill_NeverNamesAuditRows` asserts that file never names `AuditEvent` / `audit_event`. Together they keep
audit rows out of reach of the waived file, so the property holds. A later `ExecuteUpdate` added there on another
table would not be caught by this test, but the exclusion table itself is guarded by the trigger. No correction
required.

**INFO-2** — DATA_EXPOSURE, SC-10 — `ErasureController.Enter`, `Views/Erasure/Index.cshtml`.
"Already loaded" shows names and emails of participants with the entered email, and "already excluded" confirms that
an exclusion exists for it. Both are visible only to the Admin, who already has the US-038 search and the lifting
answer showing the same facts. No new exposure; no correction required.

## 19. Positive Controls

Controller-level Admin policy; global antiforgery; read-only guard first with audited refusal; fingerprint-only
storage; no logging of the email; one audit row per entry with a DB shape check; database trigger making exclusions
fill-only; parameterized set-based fill; Down refuses instead of deleting exclusions; no new dependency or outbound
destination.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 … OD-003 are resolved (a).

## 21. Review Limitations

Code, configuration and test review plus one full automated test run; no penetration testing. The page was not
exercised in a real browser.

## 22. Verdict Rationale

Build clean, full suite green in one run, every touched SC item PASS, security tests present and passing, no Critical,
Major or Minor finding. Verdict PASS; next stage HUMAN_PR_APPROVAL.
