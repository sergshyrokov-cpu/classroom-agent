---
artifact_type: implementation_verification
story: US-067
version: 1
status: APPROVED
created_at: 2026-10-29T10:04:10Z
updated_at: 2026-10-29T10:04:10Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-067-test-generation-report.md
    version: 1
  - path: docs/evidence/US-067-implementation-report.md
    version: 1
supersedes: null
---

# US-067 Implementation Verification — An Admin changes a Dean's login

## 1. Summary

**Verdict: PASS.** There are no Critical or Major findings, one Minor finding and one INFO note.

The code follows spec FR-001 … FR-012, the OpenAPI contract and the db-design. Every Acceptance Criterion has at least
one test that would fail without the behaviour. The deviations D-1 … D-4 are disclosed and sound, and so is INFO-1. The
implementation report matches the working tree. The Story's test classes pass: 148/148.

The review was done in a fresh context, from files and the working tree only.

Delegation:

- `cheap-worker` ran the build and the 7 relevant test classes (§8) and touched no files.
- All reading and every judgement were done here.

## 2. Reviewed Artifacts

Story (no version); spec v1 APPROVED; OD v1 APPROVED (OD-001 a, OD-002 a); api-design v1 + openapi v1; db-design v1 +
entity model v1; test strategy v1; AC matrix v1; test generation report v1; implementation report v1. All inputs of the
implementation report name these current versions — none is stale.

## 3. AC Verification

| AC | Tests read | Proves the AC? |
|---|---|---|
| AC-001 | U `AValidChange_GivesTheSameAccountTheNewLogin_LowerCased`, `ADisabledAccount_CanHaveItsLoginChanged`; P `AValidChange_RedirectsBack_…`, `TheDeansTemplates_ShowTheNewAddressAsAuthor` (before/after on the list), `EarlierAuditRows_StillReferToTheSameAccount` | Yes |
| AC-002 | P `TheNewAddressSignsInWithTheOldPassword_AndTheOldAddressIsRefused` — the new address in mixed case signs in, and the old one is redirected to `/sign-in` with a new `unknown_login` refused row | Yes, with m-1: the generic message text is not asserted |
| AC-003 | U `AValidChange_RotatesTheSecurityStamp`; P `AnOpenSessionOfTheDean_EndsOnItsNextRequest` (replayed cookie: 200 before, 302 `/sign-in` after) | Yes |
| AC-004 | U malformed (6), outside or subdomain or suffix domain (3), no domain, `EveryRefusedAddress_IsRefusedExactlyAsCreationRefusesIt` (7; it runs creation itself); P `ARefusedAddress_…` (6, including the missing field; it also posts to creation and compares the message), `TheRejectedValue_IsNotWrittenToTheLog` | Yes |
| AC-005 | U another Dean's address (2 letter cases), an Admin's address, the race through `NextCommitFailure`; P `AnAddressInUse_…` (3) | Yes |
| AC-006 | U `AValidChange_LeavesEverythingElseOfTheAccountUntouched` (lockout set up and asserted to be in force); P `AllElseOfTheAccountStays_WhenTheLoginChanges`, column by column in PostgreSQL | Yes |
| AC-007 | U one row in one transaction, no address in any property, factory; P a stored row with no address in its `row_to_json`; S the CHECK accepts the code, the list stays closed, migration name, Down guard, Down/Up | Yes |
| AC-008 | U the guard runs before any repository read (`Users.Reads == 0`) and writes one refused row; P a 409 per read-only cause, row unchanged, one refused row; S the refusal row is accepted | Yes |
| AC-009 | P `ADean_IsForbidden_…` (another account and their own: 403, rows unchanged), `AnAnonymousRequest_IsSentToSignIn`; `PermissionMatrixTests` has the row | Yes |
| AC-010 | P `TheOldAddress_IsFreeForANewAccount` — creation succeeds, two Deans, different ids | Yes |
| FR-006 | U own address (3 forms) → `LoginUnchanged`, nothing written; P 409 with the message, row unchanged | Yes |

No test asserts on a stub instead of the system. The absence assertions (nothing written, no audit row, value not
echoed) each sit beside a positive check that the request reached the action: the status code and the refusal message.
Since the route now exists, `AnUnknownIdAndAnAdminsId_Answer404` — green by design in the red phase — is meaningful:
the screen check shows the host is serving.

## 4. Specification Coverage

| FR | Where | Verdict |
|---|---|---|
| FR-001 | `DeanAccountsController.ChangeLogin`, `SignInRoutes.DeanLogin`; global antiforgery (test `APostWithoutAnAntiforgeryToken_IsRefused`) | met |
| FR-002 | `ChangeDeanLoginUseCase.ExecuteAsync`, steps 1–6 in spec order. Steps 3–4 are the shared `DeanLoginAddress`, which creation also calls; its logic is a verbatim extraction of the former private methods (diff read). Step 6 uses the same `FindByNormalizedEmailAsync` as creation, across all roles | met |
| FR-003 | `AppUser.ChangeLogin` sets `Email` and `NormalizedEmail` to `Normalize(email)` and calls `RotateSecurityStamp` only; one `ExecuteInTransactionAsync`; PRG with `LoginChangedConfirmation` | met |
| FR-004 | `catch (UniqueEmailViolationException)` → `DiscardUncommittedChanges` → `EmailAlreadyUsed`. `UnitOfWork.SaveChangesAsync` maps only `uq_app_user_normalized_email` to it | met |
| FR-005 | follows from FR-003; integration tests cover sign-in, the old address, the session, re-creation, templates and audit ids | met |
| FR-006 | step 5 is compared on the normalized value before uniqueness → `LoginUnchanged` → 409 re-render, no write | met |
| FR-007 | `AuditEvent.DeanAccountManaged` guard widened to five actions; code mapping both ways; CHECK + migration | met |
| FR-008 | guard first; `AuditRefusalAsync` writes `DeanAccountManagementRefused(…, DeanLoginChanged, deanId, ReadOnlyMode, …)` under `PermittedServiceWrite.AuditEvent`, then rethrows; host maps the exception to 409 | met |
| FR-009 | the class-level `ManageDeanAccounts` policy covers the new action; permission matrix row | met |
| FR-010 | `Index.cshtml`: a per-row form with `newEmail`, `type="email"` and `autocomplete="off"`; on refusal `emailInput` is null, so the value is not re-filled | met |
| FR-011 | 4 keys in uk + en; refusals reuse the creation keys through `DeanAccountTextKeys.Of` | met |
| FR-012 | no password check on the login change; nothing added | met |

No behaviour beyond the artifacts was found. The success path logs nothing new: there is no logger in the use case or
in the action.

## 5. Design Conformance

- **OpenAPI.** The status codes match: 302 → `/settings/deans` or `/sign-in`, 400 (validation, race, antiforgery),
  403, 404 (unknown id or Admin id), 409 (read-only, unchanged). Path and field match D-1 and D-2. Keys are as D-5.
- **DB.** Only the login, the normalized login and the stamps are written. The CHECK list is in the configuration, the
  snapshot and the migration Up, and the three agree, with `dean_login_changed` appended. Down has the `DO $$ … RAISE
  EXCEPTION` guard with the designed message, then restores the 27-code list. No index or column changes.
- **Entity model.** `ChangeLogin(string, DateTimeOffset)` throws on blank input and on a non-Dean, and leaves `at`
  unstored, as §2 says.
- **Architecture.**
  - AD-3: `DbContext` does not reach Web, and the Application port types are unchanged.
  - AD-4: no Google call.
  - AD-6: read-only mode is enforced in Application, and the form is not hidden.
  - AD-8: the controller takes `long` and `string?` and returns the existing page model; no entity crosses.
  - AD-9: refusals are outcomes, not exceptions.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | test as written at TEST_WRITING | the snapshot was taken before the Dean's own sign-in, which writes `last_successful_sign_in_at`; moved after it, assertions unchanged | sound — the fix is in the setup, and the assertion is no weaker |
| D-2 | test setup | the Down tests migrated through a different history-table naming; now `migrate: false`, one context, as `DataHealthPersistenceTests` does | sound — assertions unchanged |
| D-3 | registries outside the Story's test classes | the permission matrix row and the migration count 26 follow directly from the approved designs | sound |
| D-4 | tooling | the line endings went back to CRLF; `history.jsonl` diff is the TEST_WRITING line only | sound |
| INFO-1 | `CreateDeanAccountUseCase` changed although not a US-067 feature | spec FR-002 requires the checks to be the same code; the extraction is verbatim, `SchoolDomainOf` keeps the null-domain rule, and `CreateDeanAccountUseCaseTests` passes 18/18 | sound (Owner to accept) |

## 7. Report Accuracy

The implementation report's change set equals `git status`: 14 modified and 4 new `src` files, and 5 modified test
files. The 3 new test classes and 4 test documents are traced in the test generation report §2 (helpers), §3 (the
skeleton members, now completed) and the AC matrix.

Every AC-to-test location in §3 of the report exists under the name given.

The diff holds no `TODO`, `TBD`, `FIXME`, `NotImplementedException`, skipped or commented-out test (searched).

## 8. Story Test Run

The run was delegated to `cheap-worker`, 2026-10-29T10:01:49Z … 10:03:58Z. Docker was up, and each filter ran as its
own `FullyQualifiedName~` call.

| Run | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `ChangeDeanLoginUseCaseTests` | 41/41 |
| `ChangeDeanLoginPageTests` | 29/29 |
| `DeanLoginChangedAuditSchemaTests` | 6/6 |
| `DeanAccountTranslationTests` | 45/45 |
| `PermissionMatrixTests` | 3/3 |
| `AppUserMigrationTests` | 6/6 |
| `CreateDeanAccountUseCaseTests` | 18/18 |

All exit codes were 0, and nothing was skipped. The full suite is not rerun here; that is SECURITY_REVIEW's job.

## 9. Findings

- **m-1 (Minor, test_gap — advisory).** `ChangeDeanLoginPageTests.TheNewAddressSignsInWithTheOldPassword_AndTheOldAddressIsRefused`
  does not assert the text the old address gets. AC-002 says the old address "is refused with the generic refusal
  message", and FR-005 says "the generic refusal message". The test proves the redirect to `/sign-in` and the audit
  category `unknown_login`. That category is written only by the unknown-login branch, which shows the generic message,
  so the risk is low. A follow of the redirect asserting `SignIn.Refused.WrongLoginOrPassword` would close it.
- **INFO-2.** The two "not written to the log" tests (`TheRejectedValue_IsNotWrittenToTheLog`,
  `ASuccessfulChange_WritesNeitherAddressToTheLog`) assert absence over `ReadLogFilesAsync`, and do not first assert
  that the files hold the request's log lines. They would pass if logging were off. This is the same pattern as the
  existing SC-10 tests: the host always writes request logs and stops before the read, which flushes them. Noted, not
  graded.

## 10. Verdict

**PASS** — no Critical or Major findings. m-1 and INFO-2 are advisory, and INFO-1 (IMPLEMENTATION) stays OPEN for the
Owner to accept at `HUMAN_PR_APPROVAL`. Next stage: `SECURITY_REVIEW`.
