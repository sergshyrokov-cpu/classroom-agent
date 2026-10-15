---
artifact_type: implementation_verification
story: US-051
version: 1
status: APPROVED
created_at: 2026-10-16T00:40:00Z
updated_at: 2026-10-16T00:40:00Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-051-test-generation-report.md
    version: 1
  - path: docs/evidence/US-051-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-051 Implementation Verification

## 1. Summary

**PASS.** The code does what the Specification v1 and DB design v1 say: the Google
sign-in refuses every existing account whose role is not Admin, after the
`AllowedAdmin` answer and before the disabled check, on both the normal path and
the concurrent-first-sign-in path, and leaves the account untouched. Every AC
AC-001…AC-009 has a test that fails without the behaviour. Both disclosed
deviations (D-1, D-2) are sound. Spec review M-3 is confirmed. Two Minor
findings, no Critical or Major.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-051-google-sign-in-admin-accounts-only.md` | — |
| Specification | `docs/specifications/US-051-spec.md` | 1 (APPROVED) |
| Open decisions | `docs/decisions/US-051-open-decisions.md` | 1 (none) |
| API design | `docs/designs/api/US-051-api-design.md` | 1 (NOT_APPLICABLE; no `openapi.yaml`) |
| DB design | `docs/designs/database/US-051-db-design.md` | 1 (no entity model, by design) |
| Test strategy / AC matrix / test generation report | `docs/tests/US-051-*`, `docs/evidence/US-051-test-generation-report.md` | 1 |
| Implementation report | `docs/evidence/US-051-implementation-report.md` | 1 |

The implementation report's inputs name the current versions; nothing is stale.

## 3. AC Verification

| AC | Tests (class key per AC matrix) | Proves the AC? | Verdict |
|---|---|---|---|
| AC-001 | R `AnApprovedDeanAccount_IsRefused_AndSeesTheDeanAccountMessage`, R `TheRefusal_LeavesTheDeanAccountExactlyAsItWas`, T ×4 | Yes. Redirect to `/sign-in`, no session cookie, uk text on the page, no address shown; the full `row_to_json` of every account is compared before and after with the clock advanced 3 h, so any stamp, counter, lockout, role or flag change would fail. T pins the exact FR-006 texts. | PASS |
| AC-002 | R `TheRefusal_IsAuditedAgainstTheDeanAccount_WithTheNewCategory` | Yes. Single row; action, outcome, actor type/id/role, category, request id; row JSON has no `@`. | PASS |
| AC-003 | R 3 Control Plane order tests + R `ADisabledDeanAccount_IsRefusedAsNotAnAdminAccount_NotAsDisabled` | Yes. The three regression guards keep pre-v96 categories; the disabled-Dean test fails if the disabled check came first. | PASS |
| AC-004 | R `AnApprovedAddressWithNoAccount_…`, `AnExistingAdminAccount_…`, `ADisabledAdminAccount_…` | Yes (regression guards, a Dean row present alongside). | PASS |
| AC-005 | R `InReadOnlyMode_…` ×3 causes | Yes. Same refusal, row and unchanged account in each read-only cause. | PASS |
| AC-006 | R `AfterTheRefusal_TheDeanStillSignsInWithThePassword_ThroughTheForcedChange` | Yes. Password sign-in → forced change → landing, counter 0. | PASS |
| AC-007 | G `ADeanCreatedInTheMeantime_IsRefusedAsNotAnAdminAccount` (+ 2 FR-005 siblings) | Yes. The race is forced deterministically (`InterleavingAppUserRepository`, real unique index); asserts refusal, two reads, Dean row unchanged, one account (no Admin), one committed audit row of the AC-002 shape. | PASS |
| AC-008 | P `TheNewCategory_IsAccepted`, `AnUnknownCategory_IsStillRejected` ×2, `TheCategory_RoundTripsThroughTheMapping`; `AppUserMigrationTests` | Yes, against PostgreSQL via the migrations. | PASS |
| AC-009 | R `TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail` | Yes — one `AdminSignIn*` event at Warning, not one of the pre-existing names, carrying the request id; no email in any log line. It does not pin the id 2205 itself (N-1). | PASS |

**Spec review M-3 confirmed.** On the race path the refusal row is added and saved
inside `ExecuteInTransactionAsync`, and the delegate returns normally, so the
transaction commits (`CompleteGoogleSignInUseCase.AdmitAsync`). The G test reads
the row through `host.AuditRowsAsync` on its own connection after the use case
returns, so it sees only committed data. A rollback would fail it.

## 4. Specification Coverage

| FR | Where | Verdict |
|---|---|---|
| FR-001 | `RefusalOf`: `Role != AppRole.Admin` → `NotAnAdminAccount` (I-1) | Implemented |
| FR-002 | Refusal paths only add an audit row; the account is never modified, no session | Implemented |
| FR-003 | Existing `RefuseWithAsync` / `AuditEvent.AdminSignInRefused(id, role, …)` under `PermittedServiceWrite.AuditEvent` | Implemented |
| FR-004 | `ExecuteAsync`: Control Plane reply → `RefusalOf` (role, then disabled) | Implemented |
| FR-005 | `UniqueEmailViolationException` branch: `Forget`, re-read, `RefusalOf`, refusal row, no stamp | Implemented |
| FR-006 | `GoogleSignInEvents.TextKey` → `SignIn.Refused.NotAdminAccount`; uk/en resx texts equal the spec table | Implemented |
| FR-007 | `SignInLog`: `EventId(2205, "AdminSignInNotAnAdminAccount")`, Warning, `{RequestId}` only | Implemented |
| FR-008 | CHECK constraint + mapping pair + migration | Implemented |
| FR-009 | No other behaviour changed; the diff touches only the listed members | Implemented |

No behaviour beyond the artifacts was found.

## 5. Design Conformance

- **API:** no endpoint, route, status code or body changed (API design NOT_APPLICABLE holds).
- **DB:** migration `20261015203416_AddNotAnAdminAccountRefusal` only drops and re-adds
  `ck_audit_event_refusal_category_value` with `not_an_admin_account` appended; `Down`
  restores the previous list. The model snapshot changes by that one line. Enum member
  and mapping pair as db-design §2. No entity attribute change.
- **AD-3/AD-4:** the logic is in `Application`; Web maps the outcome to a key and a log line only.
- **AD-6:** read-only handling stays in `Application` through `ServiceWriteScope`; the closed BR-026 list is not widened.
- **AD-8:** no entity crosses into Web; `SignInOutcome`/`SignInRefusal` are Application models.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | TEST_WRITING tests (two reads) | They also counted the retention purge's background `retention_purge_run` row, so the result depended on timing. Now they filter to `admin_sign_in` rows and the test's own request id. Assertions are not weakened: still "exactly one sign-in row, no email", and the round trip still reads the member back. | Accepted |
| D-2 | `AppUserMigrationTests` (count 19) | The registry test pins the migration count. It now expects 20 and asserts the new name, following the established pattern. | Accepted |

No undisclosed deviation was found.

## 7. Report Accuracy

The change set in report §4 equals `git status` for `src/` and `tests/`: 9 modified
production files, 2 new migration files, 2 modified test files, 4 new test classes.
Every file is traced. The new files were checked with ripgrep. `.claude/settings.local.json`
is modified but is outside the Story, as the report says. It must stay out of the commit.
The AC-to-test locations in the AC matrix all exist. There are no `TODO`/`TBD`/`FIXME`,
skipped tests or `NotImplementedException` in the changed or new files.

## 8. Story Test Run

Delegated to `cheap-worker` (no files touched):
`dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~GoogleFirstSignInRaceTests|FullyQualifiedName~NotAnAdminAccountRefusalTests|FullyQualifiedName~NotAdminAccountTranslationTests|FullyQualifiedName~DeanGoogleSignInRefusalTests|FullyQualifiedName~AppUserMigrationTests"`
→ exit 0; **32 total, 32 passed, 0 failed, 0 skipped** (20 s). The full suite was not
rerun here; `security-reviewer` runs it.

## 9. Findings

- **N-1 (Minor, test precision).** `DeanGoogleSignInRefusalTests.TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail`
  checks that the event is new, not that it is `AdminSignInNotAnAdminAccount`/2205 (spec FR-007, review M-4).
  A future refusal logged under another fresh name would still pass. Advisory; loop-back key would be `test_gap`.
- **N-2 (Minor, coverage).** The race-path refusal (FR-005) is tested only outside read-only mode. It writes only
  under `PermittedServiceWrite.AuditEvent`, the same write the covered normal path uses, so the risk is low. Advisory.

## 10. Verdict

**PASS** — no Critical or Major findings. N-1, N-2 and the accepted deviations D-1, D-2
go to the Owner at `HUMAN_PR_APPROVAL`.

Delegation: the Story test run went to `cheap-worker`. Every reading and judgement
was made directly here.
