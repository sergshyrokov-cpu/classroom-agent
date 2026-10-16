---
artifact_type: test_strategy
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
  - path: docs/decisions/US-052-open-decisions.md
    version: 1
  - path: docs/designs/api/US-052-api-design.md
    version: 1
  - path: docs/designs/database/US-052-db-design.md
    version: 1
  - path: docs/reviews/specifications/US-052-spec-review.md
    version: 1
supersedes: null
---

# US-052 Test Strategy — Every Admin-only and Dean-only write has a forbidden-role test

## 1. Scope

Test-only Story (spec FR-014). It adds evidence for behaviour that already
exists: the forbidden- and allowed-role HTTP tests of the four one-role writes of
audit 2 F-1, and one permission-matrix test class over the installation host
(audit 2 I-1). No production code, no skeleton, no migration. API_DESIGN and
DB_DESIGN are NOT_APPLICABLE (no openapi, no entity model by design).

## 2. Test levels

| Level | Where | What |
|---|---|---|
| Integration / security (HTTP) | `Web/Security/OneRoleWriteAuthorizationTests` | real sessions over `InstallationTestHost`, PostgreSQL via Testcontainers (TC-2); Admin sign-in through the substituted Google and Control Plane ports (TC-4) |
| Security (metadata + policy service) | `Web/Security/PermissionMatrixTests` | `HostEndpoint.All` over the started host's `EndpointDataSource`; `IAuthorizationPolicyProvider` / `IAuthorizationService` with synthetic principals |

No unit level: there is no use case to isolate. No contract level: no endpoint
changes.

## 3. Scenarios

### Positive (AC-004, FR-008)

An Admin creates a Dean account, disables an active Dean, resets a Dean's
password; a full Dean session changes its own password. Each: `302`, the effect
in `app_user`, exactly one new audit row with the action's own code
(`dean_account_created`, `dean_account_disabled`, `dean_account_password_reset`,
`dean_password_changed`). Spec review m-2: the state change is tested in the
disable direction only.

### Negative / security (AC-001…AC-003, FR-001…FR-007)

- A full Dean session (password sign-in, forced change completed — spec I-4)
  posts the create form, the state form and the reset form for a second, existing
  Dean.
- An Admin posts a complete own-password form.
- Each answer is `403` with the translated `Error.Forbidden` text (SC-4 v66);
  a fingerprint of every `app_user` row (all columns read by `AppUsersAsync` plus
  the security stamp) and the audit-row count is equal before and after.
- A `400`, `409`, `302` or `404` fails the assertion (spec §8).

### Matrix (AC-005, FR-009…FR-013)

- Endpoint table: 55 rows (method × pattern) — 8 SC-4 anonymous, 4 "any
  signed-in user" (FR-011, each naming its requirement), 43 §2 cells
  naming their policy; each §2 row is named through the policy table (spec
  review m-3). Fails on an endpoint not in the table, a row with no endpoint, a
  different policy set (class + method attributes combined), an endpoint with no
  declared policy (fallback only), a duplicate method+pattern; lists all
  mismatches in one message.
- Policy table: the 16 role policies with their §2 row and roles. Fails when a
  constant of `InstallationPolicies` other than `AuthenticatedUser` and
  `CompleteTemporaryPasswordChange` (spec review m-1) is missing from the
  table, or the table names one that is not defined; when a policy is not
  registered; when it admits a role set different from its row, or an
  unauthenticated visitor.

### Boundary / validation

Inputs are deliberately valid (VR-001…VR-004) so no validation refusal can stand
in for the authorization one: fresh in-domain email, SC-2-length passwords,
`Disabled` for an active account, a token from the landing page of the same
session. No boundary of production input is under test.

### Persistence

Only as observed effect / absence of effect (FR-004, FR-008). No schema test.

## 4. Fixtures

`InstallationTestHost.StartWithControlPlaneHttpAsync` with
`DeanAccountHostExtensions.ApprovingChannel()` (normal mode, FR-007);
`DeanAccountHostExtensions.StartSignedInAsync` for the Admin;
`InsertDeanAsync`, `SignInAsDeanAsync`, `CompleteForcedChangeAsync`;
`AppUsersAsync`, `AuditRowsAsync`, `SecurityStampAsync`. No new helper in
`TestInfrastructure`.

## 5. Excluded scenarios

- Re-enable direction of AC-002 (spec review m-2: optional).
- Control Plane endpoints (Story out of scope).
- Allowed-/forbidden-role tests of endpoints other than the four of F-1 (spec
  §10: audit 2 §3 found them covered).

## 6. Known limitations

- The registered-policy check enumerates the constants of
  `InstallationPolicies`, the one place policy names live; a policy registered
  under a string literal outside that class would be caught only by the endpoint
  table (an endpoint using it would not match its row).
- Red phase: the tests pass against today's code by design (Story Notes,
  spec I-6). The mutation proof — each new test failing once against a
  temporarily removed attribute — was blocked in this stage by the session's
  auto-mode guard against weakening security in `src/`; spec I-6 places it in
  the implementation report, which will need the Owner's explicit permission.

## 7. Open Decisions

None (docs/decisions/US-052-open-decisions.md v1). FR-015 did not trigger: every
endpoint's declared policy agrees with its §2 row.
