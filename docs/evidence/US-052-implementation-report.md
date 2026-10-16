---
artifact_type: implementation_report
story: US-052
version: 1
status: DRAFT
created_at: 2026-10-16T14:28:34Z
updated_at: 2026-10-16T14:28:34Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-052-forbidden-role-tests.md
    version: null
  - path: docs/specifications/US-052-spec.md
    version: 1
  - path: docs/decisions/US-052-open-decisions.md
    version: 1
  - path: docs/designs/api/US-052-api-design.md
    version: 1
  - path: docs/designs/database/US-052-db-design.md
    version: 1
  - path: docs/tests/US-052-test-strategy.md
    version: 1
  - path: docs/tests/US-052-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-052-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 1
---

# US-052 Implementation Report

## 1. Summary

Test-only Story (spec FR-014, I-7): **no production code was written or
changed**. The behaviour — role refusals on the four one-role writes and the §2
policy matrix — already exists; TEST_WRITING added its proof. This stage
performed the spec I-6 mutation proof (with the Owner's live confirmation),
fixed the whitespace of one test file to satisfy `dotnet format`, and ran the
Definition-of-Done checks. `security_sensitive: true` because the Story is about
authorization evidence, though nothing security-relevant changed.

## 2. Source Artifacts

As in the `inputs` front matter. Open Decisions: none. API_DESIGN and DB_DESIGN
NOT_APPLICABLE.

## 3. Implemented Acceptance Criteria

| AC | Production location (unchanged) | Test | Status |
|---|---|---|---|
| AC-001 | `DeanAccountsController` class `[Authorize(ManageDeanAccounts)]` | `OneRoleWriteAuthorizationTests.ADean_CreatingADeanAccount_Gets403_AndNothingIsWritten` | PASS |
| AC-002 | same | `ADean_DisablingAnotherDean_…`, `ADean_ResettingAnotherDeansPassword_…` | PASS |
| AC-003 | `DeanPasswordController` POST `/account/password` `[Authorize(ChangeOwnPassword)]` | `AnAdmin_PostingTheOwnPasswordForm_Gets403_AndTheAccountIsUnchanged` | PASS |
| AC-004 | the four actions | `AnAdmin_CreatingADeanAccount_Succeeds`, `AnAdmin_DisablingADean_Succeeds`, `AnAdmin_ResettingADeansPassword_Succeeds`, `ADean_ChangingTheOwnPassword_Succeeds` | PASS |
| AC-005 | all controllers' attributes; `InstallationSecurityServices` policies | `PermissionMatrixTests` (3 tests) | PASS |
| AC-006 | — | full suite 6068/6068; `src/` unchanged | PASS |

### 3.1 Mutation proof (spec I-6)

Confirmed by the Owner in this conversation on 2026-10-16 (AskUserQuestion:
"Да, выполняй"). Applied by a scratchpad script, then reverted with
`git checkout -- src`; `git status --short src` was empty afterwards. Never
committed.

Mutations, together:

1. removed `[Authorize(Policy = ManageDeanAccounts)]` from `DeanAccountsController`;
2. removed `[Authorize(Policy = ChangeOwnPassword)]` from the own-password POST;
3. widened `EraseParticipantData` to Admin + Dean.

Results against the mutated build:

| Test | Result | Message |
|---|---|---|
| `ADean_CreatingADeanAccount_Gets403_…` | FAIL | Expected Forbidden, actual Found (302) |
| `ADean_DisablingAnotherDean_Gets403_…` | FAIL | Expected Forbidden, actual Found |
| `ADean_ResettingAnotherDeansPassword_Gets403_…` | FAIL | Expected Forbidden, actual Found |
| `AnAdmin_PostingTheOwnPasswordForm_Gets403_…` | FAIL | Expected Forbidden, actual BadRequest (the action ran and refused the form) |
| 4 allowed-role tests | PASS | (expected: mutations only widen access) |
| `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_…` | FAIL | lists all 5 drifted endpoints: `GET/POST /settings/deans`, `POST …/{deanid}/state`, `POST …/{deanid}/password`, `POST /account/password` — "no declared policy (fallback only)" |
| `EveryRolePolicy_AdmitsExactlyTheRolesOfItsMatrixRow` | FAIL | `EraseParticipantData`: §2 allows [Admin]; the policy admits [Admin, Dean] |
| `ThePolicyTable_HoldsEveryRolePolicy_AndNothingElse` | PASS | (expected: no policy added or removed) |

Every new forbidden-role test and both matrix checks fail when the boundary
they guard is broken; FR-013 (all mismatches in one message) is confirmed.

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `tests/ClassroomAgent.Tests/Web/Security/OneRoleWriteAuthorizationTests.cs` | new (TEST_WRITING) | AC-001…AC-004 |
| `tests/ClassroomAgent.Tests/Web/Security/PermissionMatrixTests.cs` | new (TEST_WRITING); whitespace fixed here by `dotnet format whitespace` | AC-005 |
| `docs/tests/US-052-test-strategy.md`, `docs/tests/US-052-ac-test-matrix.md`, `docs/evidence/US-052-test-generation-report.md` | new | Story artifacts |
| `docs/evidence/US-052-implementation-report.md` | new | this report |
| `docs/workflow/workflow-state.yaml`, `docs/workflow/history.jsonl` | modified by the orchestrator | workflow state |

No file under `src/`. `.claude/settings.local.json` is modified in the working
tree from before this Story — IDE-local, out of scope, must not be committed.

## 5. Validation Evidence (2026-10-16)

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (TEST_WRITING, same `src/` and test logic) | 0 | 6068 / 6068, 0 skipped |
| mutated build + both story classes | 1 | see §3.1 |
| `dotnet format ClassroomAgent.sln --verify-no-changes --include <2 test files>` | 2 → 0 | 3 WHITESPACE in `PermissionMatrixTests.cs` (lines 167–170) fixed by `dotnet format whitespace`; re-check exit 0 |
| `dotnet test --filter FullyQualifiedName~PermissionMatrixTests` (after format) | 0 | 3 / 3 |
| `dotnet test --filter FullyQualifiedName~OneRoleWriteAuthorizationTests` | 0 | 8 / 8 |

The full suite was not rerun after the whitespace-only fix; the restored `src/`
is byte-identical to the tree of the 6068/6068 run.

Delegated to `cheap-worker`: the build, the format check and the two story-class
runs after restoration (and, at TEST_WRITING, the full suite). Inline: the
mutation run (kept in one command so the revert could not be skipped), the
whitespace fix, and this report.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- None against the artifacts. FR-015 did not trigger.
- The auto-mode guard refused the mutation at TEST_WRITING; it ran here after
  the Owner's live confirmation.

## 8. Open Decisions

None touched or required.
