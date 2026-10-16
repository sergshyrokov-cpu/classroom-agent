---
artifact_type: ac_test_matrix
story: US-052
version: 1
status: DRAFT
created_at: 2026-10-16T14:10:00Z
updated_at: 2026-10-16T14:10:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-052-forbidden-role-tests.md
    version: null
  - path: docs/specifications/US-052-spec.md
    version: 1
  - path: docs/tests/US-052-test-strategy.md
    version: 1
supersedes: null
---

# US-052 Acceptance Criteria → Test Matrix

Test classes are in `tests/ClassroomAgent.Tests/Web/Security/`.
`OneRole` = `OneRoleWriteAuthorizationTests`, `Matrix` = `PermissionMatrixTests`.
Status = state at TEST_WRITING (passes by design, spec I-6).

| AC | Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | Dean posts valid create form | Integration/security | OneRole | `ADean_CreatingADeanAccount_Gets403_AndNothingIsWritten` | 403 + `Error.Forbidden` text; no account for the email; accounts + audit count unchanged | PASS |
| AC-002 | Dean disables a second active Dean | Integration/security | OneRole | `ADean_DisablingAnotherDean_Gets403_AndTheAccountIsUnchanged` | 403 + translated page; every account row incl. state, hash, failed count, lockout, stamp unchanged; audit count unchanged | PASS |
| AC-002 | Dean resets a second Dean's password | Integration/security | OneRole | `ADean_ResettingAnotherDeansPassword_Gets403_AndTheAccountIsUnchanged` | same as above | PASS |
| AC-003 | Admin posts own-password form | Integration/security | OneRole | `AnAdmin_PostingTheOwnPasswordForm_Gets403_AndTheAccountIsUnchanged` | 403 + translated page; Admin hash null, role admin, stamp unchanged; audit count unchanged | PASS |
| AC-004 | Admin creates a Dean | Integration | OneRole | `AnAdmin_CreatingADeanAccount_Succeeds` | 302; dean row exists; one new audit row `dean_account_created` | PASS |
| AC-004 | Admin disables a Dean | Integration | OneRole | `AnAdmin_DisablingADean_Succeeds` | 302; `is_disabled`; one new row `dean_account_disabled` | PASS |
| AC-004 | Admin resets a Dean's password | Integration | OneRole | `AnAdmin_ResettingADeansPassword_Succeeds` | 302; hash changed; one new row `dean_account_password_reset` | PASS |
| AC-004 | Full Dean session changes own password | Integration | OneRole | `ADean_ChangingTheOwnPassword_Succeeds` | 302; hash changed; one new row `dean_password_changed` | PASS |
| AC-005 | Endpoint ↔ matrix table (FR-009, FR-011, FR-012, FR-013) | Security | Matrix | `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_AndEveryRowIsAnEndpoint` | every endpoint is a row with its policy / anonymous; every row is an endpoint; all mismatches listed | PASS |
| AC-005 | Policy table complete (FR-010, review m-1) | Security | Matrix | `ThePolicyTable_HoldsEveryRolePolicy_AndNothingElse` | defined role policies = table, signed-in two exempt | PASS |
| AC-005 | Policy admits exactly its §2 roles (FR-010) | Security | Matrix | `EveryRolePolicy_AdmitsExactlyTheRolesOfItsMatrixRow` | registered; admitted roles = ✔ roles; anonymous refused | PASS |
| AC-006 | Existing suite green, no `src/` change (FR-014) | Regression | whole suite | `dotnet test ClassroomAgent.sln`; `git status src` | green; no file under `src/` changed | see report |
