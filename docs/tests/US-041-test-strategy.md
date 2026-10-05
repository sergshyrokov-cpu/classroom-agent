---
artifact_type: test_strategy
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:33:25Z
updated_at: 2026-10-05T07:33:25Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/designs/api/US-041-openapi.yaml
    version: 1
  - path: docs/designs/api/US-041-api-design.md
    version: 1
  - path: docs/designs/database/US-041-db-design.md
    version: 1
  - path: docs/decisions/US-041-open-decisions.md
    version: 2
supersedes: null
---

# US-041 Test Strategy — An unknown file-like address answers 404

## 1. Scope

Both hosts: an unmatched path whose last segment contains a dot answers `404`
with the error page to every caller (installation public port: anonymous,
Admin, Dean; Control Plane: anonymous, Owner, before and after setup). Existing
static files, real protected endpoints, the setup gate and the private port
keep their behaviour.

## 2. Test levels

Integration only (`WebApplicationFactory` hosts over real PostgreSQL via
Testcontainers, TC-2): the behaviour is routing and pipeline order, which only
a running host shows. No unit test — there is no Application logic. No Google
port is involved (TC-4).

## 3. Scenarios

| # | Scenario | Host |
|---|---|---|
| S-1 | GET of 4 missing file-like paths (`/robots.txt`, `/css/no-such-file.css`, `/favicon.ico`, a nested one) × each caller → `404`, no `Location`, "not found" text | both |
| S-2 | POST/PUT/DELETE of `/robots.txt` anonymously → `404`, no redirect | installation |
| S-3 | Before setup, missing file-like path → `404`, not `302 /setup`; no Owner created | Control Plane |
| S-4 | Missing file-like path writes no account and no audit row | both |
| S-5 | The requested path is not echoed into the page | installation |
| S-6 | Every existing `wwwroot` file → `200` anonymously (before setup on the Control Plane) | both |
| S-7 | Real protected page → anonymous `302` to sign-in (regression) | both |
| S-8 | Before setup, real endpoint → `302 /setup` (regression) | Control Plane |
| S-9 | Private port: unmatched file-like path under `/health` → `404`, no redirect, no error page | installation |
| S-10 | Private port: `/robots.txt` → `404` from the port filter (regression) | installation |

## 4. Negative, boundary, validation, security, persistence

- Negative: S-1/S-2/S-3 are the negative cases (unknown address).
- Boundary: the dot is in the last segment (root level and nested), with
  different extensions; a missing file under an existing static directory.
- Validation: the path is not interpreted or echoed (S-5).
- Security: anonymous access gets only `404` and the translated text; no
  redirect reveals a page; regression guards S-7/S-8 prove deny-by-default and
  the setup gate are intact.
- Persistence: none (DB_DESIGN NOT_APPLICABLE); S-4 proves nothing is written.

## 5. Fixtures

`InstallationTestHost`, `JournalHostExtensions.StartAsync` (signed-in Admin /
Dean / anonymous), `InstallationTestHost.SendPrivateAsync`,
`ControlPlaneTestHost` (with and without `CreateOwnerAsync`),
`NoStore.StaticFilesOf`. No new fixture.

## 6. Excluded scenarios

- `/api/v1` API-6 body for an unmatched path — out of scope (OD-002).
- `/signin-google` — covered by existing US-008 tests (AC-004 = full suite green).
- Language of the error page for a signed-in caller in English — unchanged
  behaviour, covered by US-008/US-001 tests.

## 7. Known limitations

Signed-in callers already get `404` before implementation (the fallback
authorization policy only challenges anonymous callers), so those cases are
regression guards, not red tests (see the test-generation report).

## 8. Open Decisions affecting testing

None open. OD-001 (before setup → `404`) drives S-3; OD-002 excludes the
`/api/v1` body.
