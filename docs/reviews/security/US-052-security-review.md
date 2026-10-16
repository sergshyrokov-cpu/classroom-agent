---
artifact_type: security_review
story: US-052
version: 1
status: APPROVED
created_at: 2026-10-16T14:54:44Z
updated_at: 2026-10-16T14:54:44Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-052-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-052-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
verdict: PASS
---

# US-052 Security Review

## 1. Executive Summary

**PASS — 0 Critical, 0 Major, 0 Minor, 4 Informational.**

US-052 is test-only. The working tree changes no file under `src/`. It adds two
test classes under `tests/ClassroomAgent.Tests/Web/Security/`. No production
security control changed, so the Story cannot weaken one. What it adds is
evidence for controls that already existed:

- **SC-4 / TC-5:** the four one-role writes of audit 2 F-1 now have a
  forbidden-role test and an allowed-role test.
- **SC-1 / §2:** one test pins the whole installation permission matrix as an
  endpoint → policy table and a policy → §2 row → roles table.

I reran everything independently: build with 0 warnings, the full suite at
6068/6068 with 0 skipped, and the vulnerable-package scan, which was clean. That
closes verification note N-4. The mutation proof recorded in the implementation
report (spec I-6) shows that the new tests are not vacuous.

The four informational notes are about the limits of the evidence. None of them
is a risk the Story introduces.

## 2. Reviewed Artifacts

These are the inputs in the front matter. API_DESIGN and DB_DESIGN are
NOT_APPLICABLE records (no endpoint and no schema change), and no OpenAPI or
entity-model file exists, by design. Open Decisions v1: none. Every input is
current and none is SUPERSEDED. The specification is APPROVED, recorded at
HUMAN_SPEC_APPROVAL.

## 3. Security-Relevant Scope

- **Exposed functionality:** unchanged on both hosts. The Control Plane is out
  of scope (Story).
- **Assets the tests protect:** Dean accounts (creation, state, password hash,
  lockout, security stamp), an Admin account (no local password), and audit rows.
- **Trust boundary exercised:** browser → installation host, using real cookie
  sessions and real antiforgery tokens over the in-process test server.
- **Components touched:** none in production. In tests: `InstallationTestHost`,
  `HostEndpoint`, `DeanAccountHostExtensions` and `DeanAccountTestData`, all
  reused unchanged.

## 4. Environment and Tools

Windows 10 Pro, .NET 10 SDK, Docker available (Testcontainers PostgreSQL).
These commands were delegated to `cheap-worker` and run from the repository
root:

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors (incremental, 7 projects) |
| `dotnet test ClassroomAgent.sln` | 6068 / 6068 passed, 0 failed, 0 skipped, 8 m 14 s, one run, no Npgsql stall |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | no vulnerable packages in any of the 7 projects (nuget.org + local SDK feed) |
| `git diff --stat HEAD -- src tests` | empty — no tracked file under `src/` or `tests/` changed |
| `git status --porcelain` | only the two new test files, Story artifacts, workflow state and `.claude/settings.local.json` |

Read inline: both test classes, `AppUserRow`, `HostEndpoint` (anonymous and
static-file predicates), the `DeanAccountTestData` constants, `.gitignore`
entries, and the `.claude/settings.local.json` diff (one line, no
credential-like content).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added. `PermissionMatrixTests.RolePolicies` holds only Admin/Dean. `ThePolicyTable_HoldsEveryRolePolicy_AndNothingElse` fails when a policy is added without a §2 row. |
| SC-2 Authentication | NOT_APPLICABLE | No authentication code changed. The tests use the real Dean password sign-in and forced change, and the substituted Google sign-in. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No change. The Admin sign-in in the tests goes through the approving Control Plane test channel. |
| SC-4 Authorization | PASS | Four new forbidden-role tests assert `403` plus the translated "forbidden" text, not a redirect, 400, 404 or 409. The token is valid, so the refusal is the authorization one. The matrix test fails on an endpoint with no declared policy (`no declared policy (fallback only)`), on a policy mismatch, on a missing or extra row, and on duplicates. Anonymous rows match the SC-4 list. |
| SC-5 Read-only mode | NOT_APPLICABLE | No change. The tests run in normal mode (FR-007), so a 409 cannot stand in for a 403. |
| SC-6 … SC-9, SC-12, SC-13 | NOT_APPLICABLE | No database UI, key, Google, channel, Contracts or outbound change. |
| SC-10 Hygiene | PASS | The tests log nothing. Fixtures are synthetic (`*.example.test` addresses, made-up passphrases). |
| SC-11 Audit | PASS | Refused requests: the audit row count is unchanged (part of the fingerprint). Allowed requests: exactly one new row with the action's own name. This makes the "no row" check non-vacuous. |

## 6. Authentication and Authorization

| Endpoint | Allowed (test) | Forbidden (test) |
|---|---|---|
| `POST /settings/deans` | Admin → 302, account created, one `Created` audit row | Dean → 403, no account, fingerprint unchanged |
| `POST /settings/deans/{deanId}/state` | Admin → 302, second Dean disabled, one row | Dean → 403, fingerprint unchanged |
| `POST /settings/deans/{deanId}/password` | Admin → 302, hash changed, one row | Dean → 403, fingerprint unchanged |
| `POST /account/password` | full Dean session → 302, hash changed, one row | Admin → 403, `PasswordHash` null, role `admin`, fingerprint unchanged |

The fingerprint (`FingerprintAsync`) covers every column of every `app_user` row:

- role and state (`IsDisabled`);
- the password fields (`PasswordHash`, `AccessFailedCount`, `LockoutEnd`);
- `UpdatedAt` and the other columns of `AppUserRow`;
- the security stamp of each row;

plus the total audit-row count. This is stronger than FR-004 requires.

The Dean session is complete: password sign-in, then the forced change, then the
landing page (spec I-4). So the refusal comes from `ManageDeanAccounts` and not
from the temporary-password policy.

## 7. Credentials, Key and Google Access

No password handling changed. The test passwords are constants in test code, not
credentials of any real system. No key or key reference is touched. The Admin
sign-in uses the substituted Google ports (TC-4). No live API is called.

## 8. Sensitive Data Exposure

No response, DTO, view, log, export or telemetry changed. The fixtures are
synthetic. No data from `classroom_cache.db` or exports was read.

## 9. Input Validation

Not changed. The test inputs are valid by construction (VR-001…VR-004), so a
validation refusal cannot mask the authorization result. One fact supports this:
under the mutation, the Admin own-password POST reached the action and got
`400` — evidence that the form is otherwise processed.

## 10. API Security

No endpoint was added, removed or changed. The matrix table is now the
executable inventory of the installation's 50+ method/route pairs. Any
undocumented endpoint added later fails `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_AndEveryRowIsAnEndpoint`.

## 11. Persistence and Configuration

No migration, entity or configuration change. Tests use Testcontainers
PostgreSQL (TC-2). There are no generated database files or exports in the
working tree. `classroom_cache.db` and `google_credentials.json` remain
git-ignored.

## 12. Logging, Audit and Telemetry

- No logging change.
- Audit coverage: see SC-11.
- `docs/hooks/tool-usage.jsonl` remains git-ignored (`.gitignore:63`).

## 13. Dependencies

No package and no project reference was added (the test project's references
are unchanged). The vulnerability scan was clean for all seven projects against
nuget.org on 2026-10-16.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| TC-5 forbidden role, 4 writes | `ADean_CreatingADeanAccount_Gets403_…`, `ADean_DisablingAnotherDean_Gets403_…`, `ADean_ResettingAnotherDeansPassword_Gets403_…`, `AnAdmin_PostingTheOwnPasswordForm_Gets403_…` |
| TC-5 allowed role, 4 writes | `AnAdmin_CreatingADeanAccount_Succeeds`, `AnAdmin_DisablingADean_Succeeds`, `AnAdmin_ResettingADeansPassword_Succeeds`, `ADean_ChangingTheOwnPassword_Succeeds` |
| SC-4 every endpoint declares a policy | `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_AndEveryRowIsAnEndpoint` |
| §2 policy admits exactly its roles, never anonymous | `EveryRolePolicy_AdmitsExactlyTheRolesOfItsMatrixRow` |
| §2 table complete | `ThePolicyTable_HoldsEveryRolePolicy_AndNothingElse` |

Non-vacuity: implementation report §3.1, mutation run with the Owner's
confirmation. Results under the mutation:

- all four forbidden-role tests failed;
- the endpoint matrix test listed all five drifted endpoints;
- the role matrix test caught `EraseParticipantData` widened to Dean.

`src/` is clean now (`git diff` empty), confirming the revert.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A Dean creates a Dean account with a forged-but-valid form | 403, nothing written | AC-001 test | PASS |
| A Dean disables or resets a colleague to take over the account | 403, account untouched incl. stamp and lockout | AC-002 tests | PASS |
| A future change drops the controller attribute | the suite fails | mutation proof §3.1 | PASS |
| A future endpoint is added under the wrong §2 policy, or with none | the matrix test fails | `Describe` + mismatch list | PASS |
| A policy is silently widened to another role | the role matrix test fails | mutation 3 | PASS |

## 16. Repository Hygiene

- No secret-like file is staged or untracked in scope.
- `.claude/settings.local.json` is modified (one line, from before this Story).
  It is IDE-local and must not be in the Story commit (DoD item 8).
- The live credential files were not opened. They are git-ignored.

## 17. Deviations

None against the specification, the security conventions or the implementation
report. The report's claims (no `src/` change, green build and tests, mutation
results) were confirmed independently where they can be re-observed.

## 18. Findings

No Critical, Major or Minor findings.

- **INFO-1 (TEST_COVERAGE):** The endpoint matrix leaves out anonymous endpoints
  shaped like static files (`IsStaticFile && AllowsAnonymous`, FR-012). Such an
  endpoint is still covered by `PublicPortAuthorizationTests` against the SC-4
  list, so nothing is unguarded. This is the same as verification N-1.
- **INFO-2 (TEST_COVERAGE):** AC-002 covers the disable direction only. The
  re-enable direction goes through the same class-level attribute, so the
  matrix test and the disable test together guard it. This is the same as
  verification N-2 and spec review m-2.
- **INFO-3 (TEST_COVERAGE):** The role-policy check evaluates registered
  policies against a synthetic principal (role claim plus name identifier), not
  a real session. Any extra requirement that reads other session claims would be
  evaluated against that principal. The HTTP tests cover the four F-1 endpoints
  with real sessions. This is the same as verification N-3.
- **INFO-4 (REPOSITORY_HYGIENE):** Keep `.claude/settings.local.json` out of the
  final commit (DoD item 8).

## 19. Positive Controls

Observed during this review:

- The fallback policy plus declared policies, now enforced endpoint-by-endpoint
  by a test.
- `403` with the translated page for a valid-token, wrong-role POST on both
  controllers.
- The authorization refusal happens before the use case: no audit row, no
  account change.
- Every installation role policy admits exactly its §2 roles and refuses an
  anonymous principal.
- No vulnerable packages.
- A green full suite.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Code, configuration and test review plus a test run. This was not a
  penetration test.
- The mutation proof was not repeated here. It modifies `src/` and needs the
  Owner's permission (memory: the auto-mode guard). It is accepted on the
  implementation report's recorded output, consistent with the verification
  stage.
- Vulnerability data is as of 2026-10-16 from the configured feeds.

## 22. Verdict Rationale

**PASS.** The Story changes no production code. It adds tests that independently
prove TC-5 for the four F-1 endpoints and pin the §2 matrix. The build and the
full suite are green on a fresh run. No dependency changed. No secret, generated
file or personal data is involved. The informational notes need no correction.

Delegation: build, full test run, vulnerable-package scan and git state went to
`cheap-worker` (facts only). Reading the tests and code, the checklist and the
verdict were done inline.
