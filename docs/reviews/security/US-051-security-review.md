---
artifact_type: security_review
story: US-051
version: 1
status: APPROVED
created_at: 2026-10-15T21:17:41Z
updated_at: 2026-10-15T21:17:41Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-051-google-sign-in-admin-accounts-only.md
    version: null
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/decisions/US-051-open-decisions.md
    version: 1
  - path: docs/designs/api/US-051-api-design.md
    version: 1
  - path: docs/designs/database/US-051-db-design.md
    version: 1
  - path: docs/tests/US-051-test-strategy.md
    version: 1
  - path: docs/tests/US-051-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-051-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-051-implementation-verification.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-051 Security Review — The Google sign-in admits only Admin accounts

## 1. Executive Summary

**PASS.** The Story closes audit finding F-2: the Google sign-in path
(`CompleteGoogleSignInUseCase`) now refuses any existing account whose role is
not Admin. This covers both the ordinary path and the concurrent first sign-in
(the race path). Before this change a Dean could sign in through Google without
the password, the forced change or the lockout (SC-2). The checks run in the
order SC-3 v96 requires: `AllowedAdmin`, then the role, then the disabled state.
The refusal leaves the account untouched, writes one audit row with the new
category `not_an_admin_account` and no email, logs a Warning (event 2205) with
no email, and issues no session. No endpoint, policy, package or Google scope
changed.

Independent run: build had 0 warnings and 0 errors. The full suite passed
5962/5962 with 0 failed and 0 skipped. No vulnerable packages were found.

There are no Critical, Major or Minor findings, and 3 Informational ones.
Next: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

See `inputs` in the front matter. The Specification and the Open Decisions are
APPROVED (v1). The API design is NOT_APPLICABLE and has no `openapi.yaml`. The
DB design (v1) defines one migration. The implementation report (v1) and the
implementation verification (v1, PASS) are current. No input is SUPERSEDED.

## 3. Security-Relevant Scope

- **Host:** the installation (`Web`). The only surface is the existing Google
  OAuth callback, which is anonymous (SC-4 list). The Control Plane is unchanged.
- **Assets:** the integrity of Dean accounts (password, forced change, lockout
  state), the session cookie, and audit rows.
- **Trust boundaries:** browser → callback; callback → Application use case;
  use case → Control Plane port (unchanged); use case → PostgreSQL.
- **Changed components:** `CompleteGoogleSignInUseCase` (`RefusalOf`, race
  path), `SignInRefusal`, `AuditRefusalCategory`, `AuditEventConfiguration`
  (CHECK + mapping), migration `20261015203416_AddNotAnAdminAccountRefusal`,
  `GoogleSignInEvents.TextKey`, `SignInLog` (event 2205), and one translation
  entry each in uk/en.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available).
- `dotnet build ClassroomAgent.sln`: succeeded, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln`: 5962 total, 5962 passed, 0 failed, 0 skipped
  (7 m 39 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`:
  no vulnerable packages in all 7 projects (nuget.org + the local SDK feed).
- `git diff --stat -- '*.csproj'`: empty.
- **Delegated** (cheap-worker, read only): the build, the full test run, the
  vulnerability scan and the csproj diff. The code, test and configuration
  reading and every judgement in this review were done directly.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added. The check is `account.Role != AppRole.Admin` (`CompleteGoogleSignInUseCase.RefusalOf`), so the rule covers every non-Admin role, not just Dean. |
| SC-2 Authentication | PASS | A Dean is refused before `RecordSuccessfulSignIn`, so the password, the forced change and the lockout cannot be bypassed. The test `TheRefusal_LeavesTheDeanAccountExactlyAsItWas` compares the whole `app_user` row as JSON before and after. `AfterTheRefusal_TheDeanStillSignsInWithThePassword_ThroughTheForcedChange` proves the password path is unchanged. |
| SC-3 AllowedAdmin / one account one role | PASS | `ExecuteAsync`: the Control Plane is asked first, on every call (unchanged), then `RefusalOf` checks role before disabled. The race path re-reads the row and applies the same `RefusalOf` (FR-005). Tests: `ADeanAccountNotInAllowedAdmin_…`, `…UnknownInstallation…`, `…ControlPlaneDoesNotAnswer…`, `ADisabledDeanAccount_IsRefusedAsNotAnAdminAccount_NotAsDisabled`, `GoogleFirstSignInRaceTests` (3). |
| SC-4 Authorization | PASS | No endpoint, route, policy or anonymous surface was added. The refusal reuses the existing redirect to the sign-in page. No session cookie is set (`Assert.Null(callback.SetCookie(...))`). |
| SC-5 Read-only mode | PASS | The refusal writes only `PermittedServiceWrite.AuditEvent` (BR-026). `InReadOnlyMode_TheDeanAccountIsRefusedAndAuditedTheSameWay` covers each read-only cause. The race path uses the same declared write (see INFO-3). |
| SC-6 No DB UI | NOT_APPLICABLE | No diagnostic surface was touched. |
| SC-7 Key | NOT_APPLICABLE | No key or key reference was touched. |
| SC-8 Google | NOT_APPLICABLE | No scope or Google data call. The OAuth sign-in still calls no data API. |
| SC-9 Channel | NOT_APPLICABLE | The Control Plane call is unchanged. |
| SC-10 Hygiene | PASS | The log line carries only the request id (`SignInLog.Refuse`, event 2205). The audit row has no email (`DoesNotContain("@")` on the row JSON). The page does not echo the address. |
| SC-11 Audit | PASS | Exactly one row per refusal: actor `app_user`, the account's id and role (`dean`), category `not_an_admin_account`, request id. Rows are insert-only. `NotAnAdminAccountRefusalTests` proves the CHECK accepts the new category and still rejects an unknown one. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and the Control Plane are unchanged. |
| SC-13 Outbound | NOT_APPLICABLE | No new destination. |

## 6. Authentication and Authorization

The callback is still the only entry point. It is anonymous by the SC-4 list,
and `state` and the correlation cookie are proven by the handler before the use
case runs (unchanged). The session is issued only on `Succeeded`
(`GoogleSignInEvents.OnTicketReceivedAsync`), and every refusal branch returns
before `InstallationSession.SignInAsync`. The new branch cannot admit: the race
path returns from the transaction lambda before
`AuditEvent.AdminSignInSucceeded`, and `refused` is checked before `admitted`
is read. Findings: none.

## 7. Credentials, Key and Google Access

No password is read, set or compared on the Google path. The Dean's hash, stamp,
counter and lockout are untouched (proven by the whole-row comparison). There is
no change to the key or to scopes. Findings: none.

## 8. Sensitive Data Exposure

- **Page:** a fixed translated text with no address in it (asserted). The text
  says the address is a Dean account. See INFO-2.
- **Logs:** the request id only, at Warning. The test asserts the email is
  absent from every log line.
- **Audit:** ids, role and category only.
- **Exceptions:** no new user-visible exception. The `InvalidOperationException`
  on the race path predates this Story and reaches the single exception handler.
- **Exports and telemetry:** not touched.

## 9. Input Validation

The only input is the Google-verified email, which is validated as before
(VR-005: present, well-formed, `email_verified`) and normalised (BR-079). The
new code reads only the stored account's role. Findings: none.

## 10. API Security

There is no REST surface. The callback answers with a 302 to the sign-in page
exactly as for the other refusals. Findings: none.

## 11. Persistence and Configuration

Migration `AddNotAnAdminAccountRefusal` drops and re-adds
`ck_audit_event_refusal_category_value` with the extra value. `Down` restores
the previous list. The snapshot and the configuration agree. The mapping
round-trips (`TheCategory_RoundTripsThroughTheMapping`). There is no
`EnsureCreated`, no configuration change, and no new column. Findings: none.

## 12. Logging, Audit and Telemetry

See SC-10 and SC-11. `docs/hooks/tool-usage.jsonl` is git-ignored (`.gitignore:63`).
Findings: none beyond INFO-3.

## 13. Dependencies

No `.csproj` change. The vulnerability scan found nothing. No new project
reference.

## 14. Security Test Coverage

| Requirement / abuse case | Test |
|---|---|
| Dean admitted through Google (F-2) | `AnApprovedDeanAccount_IsRefused_AndSeesTheDeanAccountMessage` (no session cookie) |
| Account untouched | `TheRefusal_LeavesTheDeanAccountExactlyAsItWas` |
| Audit shape, no email | `TheRefusal_IsAuditedAgainstTheDeanAccount_WithTheNewCategory` |
| Order of checks | 3 × `ADeanAccount…Keeps…Refusal`, `ADisabledDeanAccount_…` |
| Admins unaffected | `AnApprovedAddressWithNoAccount_…`, `AnExistingAdminAccount_…`, `ADisabledAdminAccount_…` |
| Race: Dean created concurrently | `GoogleFirstSignInRaceTests.ADeanCreatedInTheMeantime_IsRefusedAsNotAnAdminAccount` |
| Read-only mode | `InReadOnlyMode_TheDeanAccountIsRefusedAndAuditedTheSameWay` |
| Password path intact | `AfterTheRefusal_TheDeanStillSignsInWithThePassword_ThroughTheForcedChange` |
| Log hygiene | `TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail` |
| DB constraint | `NotAnAdminAccountRefusalTests` (3) |

No test calls a live Google API or Control Plane (TC-4). The Control Plane is an
in-process HTTP double and the Google identity is a stub.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Owner approves a Dean's address as Admin; the Dean signs in through Google to skip the password | Refused, no session, account untouched | §14 rows 1–2 | PASS |
| Admin creates a Dean with an approved Admin's address while that Admin's first sign-in is in flight | The race path refuses and creates no Admin | race test | PASS |
| A locked-out or temporary-password Dean uses Google to escape the lockout or forced change | Refused before any stamp; the lockout and forced change remain | whole-row test, password-path test | PASS |
| A disabled Dean probes whether it is disabled through Google | Answered "not an Admin account", not "disabled" | `ADisabledDeanAccount_…` | PASS |
| The refusal leaks the address into logs or audit | Not written | log and audit tests | PASS |

## 16. Repository Hygiene

`google_credentials.json`, `classroom_cache.db` and `tool-usage.jsonl` are
git-ignored. No tracked `.db`, generated `.xlsx` or credential file was found.
No secret-like content appears in the change set.
`.claude/settings.local.json` is modified (INFO-1).

## 17. Deviations

There are no security deviations. The implementation verification's D-1 (test
reads excluding the retention-purge row) and D-2 (migration count 19→20) are
test-side and have no security effect.

## 18. Findings

- **INFO-1 (Informational, REPOSITORY_HYGIENE).** `.claude/settings.local.json`
  is modified in the working tree (a local permission entry was added). It is
  IDE-local configuration, outside the Story scope (DoD item 8, Git Policy).
  Correction: leave it out of the Story commit. Loop-back: none.
- **INFO-2 (Informational, DATA_EXPOSURE).** The refusal text tells the person
  that the address is a Dean account. That is account-state disclosure, but only
  to someone who has proven control of that Google address and whose address the
  Control Plane approved. The text is required by FR-006 and the Story. No
  correction is needed.
- **INFO-3 (Informational, TEST_COVERAGE).** This carries implementation
  verification N-1 and N-2. The log test does not pin event id 2205, and the
  race-path refusal is not exercised in read-only mode. Read-only safety holds
  by construction: the race path commits only `PermittedServiceWrite.AuditEvent`,
  the same declared write the tested ordinary path uses. Optional follow-up.
  Loop-back: none.

## 19. Positive Controls

- Role check before disabled check, on both the ordinary and the race path.
- The session is issued only on success.
- The whole-row integrity of the refused account is asserted.
- Audit rows are constrained by CHECK in the database as well as by the enum
  mapping.
- Neither the log nor the audit row carries an email.
- The Control Plane is still asked every time, with no cache.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus an independent full test run. No
penetration test was performed and no live Google domain was used. Text search
was confirmed by tests for the call paths that matter (the session is issued
only on success, and read-only mode on the ordinary path).

## 22. Verdict Rationale

The build and the full suite are green. Every touched SC item is PASS. The
security-sensitive Acceptance Criteria (AC-001, AC-002, AC-003, AC-005, AC-007)
are each proven by a passing test. There are no Critical, Major or Minor
findings and no open security decision. Verdict: **PASS**, and the next stage
is `HUMAN_PR_APPROVAL`.
