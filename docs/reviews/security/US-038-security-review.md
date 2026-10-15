---
artifact_type: security_review
story: US-038
version: 1
status: APPROVED
created_at: 2026-10-15T10:50:00Z
updated_at: 2026-10-15T10:50:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-038-erase-participant.md
    version: null
  - path: trebovaniya.md
    version: 93
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-038-open-decisions.md
    version: 3
  - path: docs/designs/api/US-038-api-design.md
    version: 1
  - path: docs/designs/api/US-038-openapi.yaml
    version: 1
  - path: docs/designs/database/US-038-db-design.md
    version: 1
  - path: docs/designs/database/US-038-entity-model.md
    version: 1
  - path: docs/tests/US-038-test-strategy.md
    version: 1
  - path: docs/tests/US-038-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-038-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-038 Security Review — Erase all data of one participant on request

## 1. Executive Summary

**Result: PASS.** No Critical, Major or Minor finding; four Informational notes.

The Story adds an Admin-only erasure of one participant's data (search, confirmation,
erasure, lifting of an exclusion) and makes synchronization and the Meet load skip
excluded people. Principal controls, each verified in code and by a passing test:
a dedicated Admin-only policy on the whole controller; read-only refusal in
`Application` before any read or validation, audited under the BR-026 audit service
write; antiforgery on all three POSTs; no personal data in any address, log line,
audit row or exclusion row (the exclusion holds the Google user id and an
HMAC-SHA-256 fingerprint keyed with a per-installation random salt); the erasure and
the writers serialized by a transaction-scoped advisory lock so a concurrent sync
cannot re-store an erased person.

Limitations: code, configuration and test review plus a full independent build/test
run; no penetration testing.

## 2. Reviewed Artifacts

As in the front matter. All mandatory inputs current; none `SUPERSEDED`;
`HUMAN_SPEC_APPROVAL` recorded 2026-10-15T05:41:35Z. Open decisions v3: OD-001 (a),
OD-002 (a) resolved by the Owner.

## 3. Security-Relevant Scope

- **Host:** installation (`ClassroomAgent.Web`) only. Five operations under
  `/settings/erasure`: `GET` page, `POST searches`, `GET participants/{id}`,
  `POST participants/{id}/erasure`, `POST exclusion-liftings`; one home-page link.
- **Assets:** participant personal data (names, emails, grades, Meet participation —
  potentially minors); the erasure salt; the exclusion rows; audit rows.
- **Trust boundaries:** browser → installation (public HTTPS); controller → use cases;
  `Application` → persistence ports; synchronization / Meet load (background) →
  persistence under the fence.
- **Components:** `ErasureController`, `EraseParticipantUseCase`,
  `LiftErasureExclusionUseCase`, `SearchParticipantsForErasureQuery`,
  `GetErasureConfirmationQuery`, `ParticipantErasureStore`,
  `ParticipantErasureReadSource`, `ErasureFence`, `ErasureSaltStore`,
  `ErasedParticipantRepository`, `EmailFingerprint`, `RunSynchronizationUseCase`
  (exclusion checks), `AuditEvent` factories, `ErasureLog`, migration
  `AddParticipantErasure`.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 running (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 errors, 0 warnings.
- `dotnet test ClassroomAgent.sln` — 5917 total, 5917 passed, 0 failed, 0 skipped
  (7 min 35 s). Independent of the implementation report's run.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` — no
  vulnerable packages in any of the 7 projects (nuget.org source).
- `git diff HEAD` over `*.csproj` / `Directory.*.props` — empty.
- Delegated (`cheap-worker`, read only): the four commands above. Code reading,
  judgement and the verdict were done by the reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Policy `EraseParticipantData` = authenticated + role Admin (`InstallationSecurityServices.cs`); matches §2 v93 row (✔ Admin, ✘ Dean). No Teacher/Student. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in or password change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin login path unchanged (`AdminLoginCheckEveryTimeTests` only gains the two tables in its snapshot). |
| SC-4 Authorization | PASS | `[Authorize(Policy = EraseParticipantData)]` on the controller; no `[AllowAnonymous]`; global antiforgery covers the POSTs, each form renders `@Html.AntiForgeryToken()`; no state change on GET (`AGetToTheErasureAddress_ErasesNothing`). Tests: `ErasureAuthorizationTests` (Admin allowed, Dean 403, anonymous → sign-in, forced-change Dean redirected, no-token POST 400, endpoint enumeration). |
| SC-5 Read-only | PASS | Guard first in both use cases, before the mark/validation; refusal audited inside `ServiceWriteScope.Declare(PermittedServiceWrite.AuditEvent)` and rethrown → 409. All three causes tested in `Application` (`EraseParticipantUseCaseTests`, `LiftErasureExclusionUseCaseTests`) and over HTTP (`ErasureReadOnlyHttpTests`). Erasure goes through the change tracker, so the `ReadOnlyModeUnitOfWork` backstop still applies (D-1). |
| SC-6 No DB UI | PASS | Search is a fixed parameterized query over four columns; no SQL input. |
| SC-7 Key | NOT_APPLICABLE | No key or key reference touched. The salt is not a credential and is generated in the database by the installation (spec I-2, S-05). |
| SC-8 Google | PASS | No Google port added or called by the new use cases; sync only filters what it already reads. |
| SC-9 Channel | NOT_APPLICABLE | No Control Plane or channel change. |
| SC-10 Hygiene | PASS | Only one new log call (`ErasureLog`, internal ids + counts); term and email to lift travel in POST bodies, never logged (`ErasureLoggingTests`). `EmailFingerprint.ToString()` prints nothing. Errors via the existing handler. |
| SC-11 Audit | PASS | `ParticipantErased` (target = participant id, five counts), `ErasureExclusionLifted` (target = exclusion id), refusals for both; no personal data; no update path. Tests: `ParticipantErasureTests`, `ParticipantErasureSchemaTests`, `LiftErasureExclusionUseCaseTests`. Audit rows are not erased (S-07). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` / `ControlPlane` untouched. |
| SC-13 Outbound | PASS | No new outbound destination. |

## 6. Authentication and Authorization

All five operations: Admin 200/302, Dean 403, anonymous redirect to sign-in. Route
ids are internal surrogate ids with a digit/length route constraint and a positive
parse (`TryParseId`); malformed or unknown → 404, nothing changed (tested). The
`participantId` reaches the store only after the exclusive fence; a participant
erased meanwhile is 404 with a full rollback. No cross-installation identifiers exist
(one database per school, AD-1).

## 7. Credentials, Key and Google Access

No password, key or scope change. Salt: 32 bytes from `RandomNumberGenerator`,
inserted with `ON CONFLICT DO NOTHING` (parameterized), immutable by a check
constraint, read with `AsNoTracking`, never rendered, logged or exported.
Fingerprint: `HMACSHA256.HashData(salt, UTF-8(NormalizeEmail(email)))`; the same
normalization in erasure, lifting and Meet load.

## 8. Sensitive Data Exposure

- **Views/DTOs:** `ErasurePageModel`, `ErasureConfirmationPageModel`,
  `ParticipantSearchResult` — DTOs only; Razor encodes all output; no `Html.Raw`.
  Names and emails are shown only to the Admin, which the §2 row permits.
- **Addresses:** search term and lifting email only in POST bodies; the one-time
  message is an enum key in TempData. The route carries only the internal id.
- **Logs / audit / exclusion row:** no name, email, Google user id in logs or audit;
  exclusion row holds the Google id and fingerprint only, as BR-076 defines.
- **Caching:** the global `NoStoreMiddleware` covers the new pages.

## 9. Input Validation

- Search term: trimmed, 2…254, else 400 with a field error; matched literally
  (EF parameter, no pattern injection); result capped at 50 (+1 probe).
- Lifting email: trimmed, ≤254, exactly one `@`, non-empty parts, no whitespace.
- Mark: only an explicit `true`; without it nothing is read or written.
- Route id: constraint + `long.TryParse(NumberStyles.None)` + `> 0`.

## 10. API Security

Implemented endpoints match the OpenAPI v1 exactly (five operations, methods,
status codes, PRG on success, 409 for read-only before validation). No `/api/v1`
endpoint; no undocumented field.

## 11. Persistence and Configuration

Migration `AddParticipantErasure`: `erased_participant` (unique Google id,
`bytea(32)` fingerprint), `erasure_salt` singleton, immutability checks, five
nullable audit count columns with shape checks, `meet_session.organizer_email`
nullable, expression indexes. No `EnsureCreated`. No configuration change. Raw SQL
limited to the advisory lock (constant key, parameter) and the salt insert
(interpolated → parameters).

## 12. Logging, Audit and Telemetry

Covered in §5 (SC-10, SC-11). Hook telemetry unchanged.

## 13. Dependencies

No package or project-reference change; vulnerability scan clean (§4). HMAC and
RNG from the base library (S-12).

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01/S-02 Admin-only, forbidden roles | `ErasureAuthorizationTests` |
| S-04 read-only, three causes | `EraseParticipantUseCaseTests`, `LiftErasureExclusionUseCaseTests`, `ErasureReadOnlyHttpTests` |
| S-05/S-06 no personal data in exclusion, logs, audit | `ParticipantErasureTests`, `ErasureLoggingTests`, `ParticipantErasureSchemaTests` |
| S-08 antiforgery | `ErasureAuthorizationTests.WithoutAnAntiforgeryToken_ThePostIs400_AndNothingChanges`, `InstallationCookieTests` |
| FR-011 no re-store by a concurrent sync | `ErasureExclusionSyncTests`, `ErasureExclusionMeetTests` |
| Retention purge leaves exclusions | `ParticipantErasureTests.TheRetentionPurge_LeavesExclusionsUntouched` |

All passed in the independent run.

## 15. Abuse Case Review

| Scenario | Protection | Status |
|---|---|---|
| Dean or anonymous posts an erasure | policy → 403 / sign-in | PASS (tested) |
| Erasure forced in read-only mode | `Application` guard → 409, audited | PASS (tested) |
| Cross-site form posting an erasure | global antiforgery → 400 | PASS (tested) |
| Erasure by GET link | GET route has no write | PASS (tested) |
| Erasure without the irreversible mark | nothing read or written, 400 | PASS (tested) |
| Sync re-stores the erased person mid-erasure | exclusive/shared advisory lock, exclusion read after the fence | PASS (tested) |
| Search term with `%`/`_` to enumerate | literal match; Admin may list participants anyway | PASS |
| Personal data in logs via the search / lifting form | POST bodies, never logged | PASS (tested) |

## 16. Repository Hygiene

Change set (git status): source, tests, migration, Story artifacts. No secret-like
file, generated database, `.xlsx` or IDE-local file. Live credential files not
opened.

## 17. Deviations

- **D-1** (erasure via change tracker instead of set-based statements): security-
  positive — keeps the read-only commit backstop and the US-037 confinement of set-
  based deletes. See INFO-1.
- **D-2** (pre-existing US-015 submitter defect fixed): `SaveChangesAsync` inside the
  course transaction, under the shared fence; no security effect. See INFO-4.
- **D-3…D-6:** no security effect; D-6 (lifting button disabled in read-only) is UI
  only, enforcement stays in `Application`.

## 18. Findings

**INFO-1 — OTHER (performance of a privacy operation).** `ParticipantErasureStore`
loads every matching Meet participation into memory before deleting. For one person
the volume is bounded; no security effect. If the Owner cares, record the D-1
mechanism in the db-design as the accepted one. Loop-back: none.

**INFO-2 — INPUT_VALIDATION.** On a search validation error the controller echoes
the untrimmed term back into the input value. HTML-encoded and bounded by Kestrel's
form limits; no risk. Loop-back: none.

**INFO-3 — AUDIT.** A lifting that matches nothing writes no audit row, so an Admin
can test whether an email was ever erased without a trace. This is the approved
design (api-design: "lifting with no match 200", audit only for a lifted
exclusion), Admin-only, and reveals no personal data beyond what the Admin supplied.
Noted for awareness only.

**INFO-4 — OTHER.** The implementation report notes a run can stay `running` when a
course transaction fails (outside this Story). Not a security issue; worth a
follow-up Story.

## 19. Positive Controls

Admin-only policy on the controller; read-only guard first in `Application` with an
audited refusal; antiforgery on all POSTs; PRG with enum-only TempData messages;
POST-only search and lifting inputs; literal parameterized search; HMAC-SHA-256 with
a per-installation random immutable salt; exclusion stores no name or email;
transaction-scoped advisory fence; audit rows with internal ids and counts only;
structured log line without personal data; no new package, endpoint outside the
contract or outbound flow.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

No penetration test or live-browser check. The fence's concurrency guarantee is
verified by the integration tests and by PostgreSQL advisory-lock semantics, not by
a load test.

## 22. Verdict Rationale

Green build and tests confirmed independently; every touched SC item PASS; required
security tests present and passing; no blocking Open Decision; only Informational
notes. Verdict **PASS** → `HUMAN_PR_APPROVAL`.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-038
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-038-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - INFO-1 erasure loads Meet rows into memory (D-1); no security effect
    - INFO-2 untrimmed search term echoed on validation error; encoded, bounded
    - INFO-3 a no-match lifting is not audited (approved design)
    - INFO-4 a run can stay running after a failed course transaction (outside Story)
```
