---
artifact_type: test_strategy
story: US-067
version: 1
status: DRAFT
created_at: 2026-10-29T09:01:01Z
updated_at: 2026-10-29T09:01:01Z
produced_by: test-writer
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
supersedes: null
---

# US-067 Test Strategy — An Admin changes a Dean's login

## 1. Scope

One new operation, `POST /settings/deans/{deanId}/login` (api-design D-1), its use case, one new `AppUser`
method, one new audit action and its CHECK migration. Everything around it — sign-in, the session stamp check,
creation, the template list — already exists and is exercised end to end, not re-tested in isolation.

## 2. Test levels

| Level | Class | What it proves |
|---|---|---|
| Unit (Application, ports in memory, TC-1) | `Application/UseCases/ChangeDeanLoginUseCaseTests` | evaluation order FR-002, every refusal outcome, the change itself FR-003, the audit row FR-007, read-only FR-008, the unique-index race FR-004, "same code as creation" by comparing outcomes, the entity invariant (Admin refused, blank refused) |
| Integration / contract over HTTP, real PostgreSQL (TC-2, TC-3) | `Web/Pages/ChangeDeanLoginPageTests` | every status of api-design §2 (302, 400, 403, 404, 409 ×2, anonymous 302), the page form (FR-010), sign-in and session effects (FR-005), template author, audit rows as stored, logs (SC-10), AC-010 reuse of the old address |
| Persistence / migration (TC-2, PC-2) | `Infrastructure/Persistence/DeanLoginChangedAuditSchemaTests` | the CHECK accepts the code for the success and the read-only refusal row (SPEC_REVIEW m-1), stays closed, the migration name and order, Down refused over rows, Down/Up round trip |
| Translation (TC-8, NFR-073) | `Web/Localization/DeanAccountTranslationTests` (existing, key list extended) | the four new keys of api-design D-5 in uk and en, both files the same key set |

## 3. Scenarios

**Positive.** Valid change (mixed case and spaces → stored lower-cased); same id; new stamp; one audit row; PRG with
the confirmation; screen shows the new login; template list shows the new author; new address + old password signs
in; old address free for a new account; disabled account may change (OD-001 a).

**Negative.** Old address refused like an unknown login (generic redirect, audit `unknown_login`); old cookie sent to
`/sign-in`; malformed / empty / missing field / outside domain / subdomain / suffix-attack domain / no school domain;
taken by a Dean or an Admin in any case; race on the unique index; same address (OD-002 a, `409`); unknown id, Admin's
id (`404`); non-numeric id (no route); missing antiforgery token (`400`).

**Boundary.** Letter case and surrounding spaces on both "same address" and "taken"; whitespace-only input; a field
omitted from the form; an unknown id combined with a malformed address (account checked first, I-1); no school domain
combined with the account's own address (domain before "same address", I-1); own address never "already in use".

**Validation.** Every refusal reuses the creation keys and gives the same refusal as creation for the same input
(FR-002 "same code", FR-011, AC-004) — asserted both on outcomes (unit) and on page text (HTTP).

**Security.** Admin allowed, Dean `403` for another account and their own, anonymous → `/sign-in` (TC-5, SC-4);
antiforgery; the school domain never taken from the form (only `newEmail` is posted); stamp rotation ends a session;
audit row holds neither address (unit: every property; HTTP: `row_to_json` of the stored row); neither the rejected
value nor the new/old address on success reaches the log (SC-10); read-only refusal in Application with its refused
audit row (AD-6, BR-026).

**Persistence.** Login change touches only `email`, `normalized_email`, stamps (AC-006: hash, temporary mark,
counter, lockout, disabled, language, last sign-in, created_at compared column by column); CHECK
`ck_audit_event_action`; migration `AddDeanLoginChangedAuditAction`, its Down guard (db-design §4).

## 4. Fixtures

- `DeanAccountWorld` gains `ChangeLogin` (the use case over the in-memory ports) and `UnitOfWork.NextCommitFailure`
  (a one-shot `UniqueEmailViolationException` on commit, for FR-004).
- `DeanAccountTestData` gains `Paths.Login(id)`, `Fields.NewEmail`, `Audit.LoginChanged`, four `TextKeys` (also in
  `TextKeys.All`).
- `DeanAccountHostExtensions.ChangeDeanLoginAsync` posts the form (a `null` value omits the field).
- Existing: `InsertDeanAsync`, `SignInAsDeanAsync`, cookie replay (US-064), `InsertTemplateAsync`, `ReadOnlyModeHost`.

## 5. Excluded scenarios

- The unique-index race over HTTP: two Admin requests cannot be interleaved deterministically between the pre-check
  and the save; FR-004 is proved at the use case with the commit failure the index produces
  (`UnitOfWork` already maps it, `UnitOfWork.cs:28-31`).
- `DbUpdateConcurrencyException` on a concurrent edit of the same row: db-design §2 designs no new behaviour.
- The structural "same code, not a copy" of FR-002 is not asserted by structure (that would test implementation);
  identical outcomes and identical page messages for the same inputs are the observable evidence.
- Password rule against the new login (FR-012): no change in code; the existing password-policy tests cover it.

## 6. Known limitations

- `ASuccessfulChange_WritesNeitherAddressToTheLog` and `TheRejectedValue_IsNotWrittenToTheLog` pass before
  implementation by design (nothing is logged when nothing happens — the "vacuous red phase" note); each also asserts
  the HTTP status of the post, which is red until the endpoint exists, so neither is vacuous.

## 7. Open Decisions affecting testing

None. OD-001 a and OD-002 a are applied (AC-006 / disabled change; FR-006 `409`).
