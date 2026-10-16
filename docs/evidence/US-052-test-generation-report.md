---
artifact_type: test_generation_report
story: US-052
version: 1
status: DRAFT
created_at: 2026-10-16T14:12:00Z
updated_at: 2026-10-16T14:12:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-052-spec.md
    version: 1
  - path: docs/designs/api/US-052-api-design.md
    version: 1
  - path: docs/designs/database/US-052-db-design.md
    version: 1
  - path: docs/tests/US-052-test-strategy.md
    version: 1
  - path: docs/tests/US-052-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-052 Test Generation Report

## 1. Files

Created:

- `tests/ClassroomAgent.Tests/Web/Security/OneRoleWriteAuthorizationTests.cs` — 8 tests (AC-001…AC-004)
- `tests/ClassroomAgent.Tests/Web/Security/PermissionMatrixTests.cs` — 3 tests (AC-005)

Modified: none. No file under `src/` changed (`git status --short` shows only the
two test files and the two `docs/tests/` artifacts as new, plus the pre-existing
`.claude/settings.local.json` change, which is not part of this Story).

Compile-only skeleton: none — the Story adds no production declaration.

## 2. Commands and results (2026-10-16)

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~PermissionMatrixTests` | 3 / 3 passed |
| `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~OneRoleWriteAuthorizationTests` | 8 / 8 passed |
| `dotnet test ClassroomAgent.sln` | 6068 / 6068 passed, 0 failed, 0 skipped (7 m 38 s; previous total 6057 + 11 new) |

## 3. Red-phase classification

All 11 new tests **pass before IMPLEMENTATION — by design** (Story Notes, spec
I-6): the behaviour exists, only its proof was missing. Why they are not vacuous:

- **Forbidden-role tests** assert `403` plus the translated `Error.Forbidden`
  text — a `400` (token), `409` (read-only), `302` (session) or `404` would fail.
  Each has an allowed-role twin built the same way (same session kind, same
  token source, same form) that gets `302`, the database effect and exactly one
  new audit row of the action's code, so the request is valid and the "nothing
  changed" fingerprint can detect a change (spec I-5).
- **Matrix test** compares the declared policy set of every endpoint with a
  55-row table and every table row with an endpoint; FR-015 did not trigger —
  every declared policy agrees with its §2 row, including the journal export
  under `UseReportTemplates` (spec I-3).
- **Policy test** asks the real `IAuthorizationService` for Admin, Dean and an
  unauthenticated principal per policy.

**Mutation proof not run at this stage.** A temporary, reverted mutation of
`src/` (remove `ManageDeanAccounts` from `DeanAccountsController`, remove
`ChangeOwnPassword` from the own-password POST, widen `EraseParticipantData` to
Dean) was refused by the session's auto-mode guard as security weakening. It
was not worked around. Spec I-6 assigns this proof to the implementation report;
it needs the Owner's explicit permission there.

Unexpected failures: none.

## 4. Coverage

Every AC-001…AC-006 is mapped in `docs/tests/US-052-ac-test-matrix.md`.
Spec review m-1 honoured (`AuthenticatedUser` and
`CompleteTemporaryPasswordChange` exempt from the policy table), m-2 (disable
direction only), m-3 (every §2 endpoint row names its §2 row through the policy
table; anonymous and signed-in rows name their requirement).

Untested Acceptance Criteria: none.

## 5. Delegation

Test design, both test classes and the red-phase verdict were done inline — the
HTTP tests turned on baseline timing (fingerprint after sign-in) and token
source, where a cheap model could plausibly err. The build and the three test
runs were delegated to `cheap-worker` subagents.

## 6. Open Decisions

None.

## 7. Overall result

PASS — tests compile, all green, every AC mapped; mutation proof deferred to
IMPLEMENTATION per spec I-6.
