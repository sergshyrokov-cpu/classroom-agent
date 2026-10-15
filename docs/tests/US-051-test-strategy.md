---
artifact_type: test_strategy
story: US-051
version: 1
status: DRAFT
created_at: 2026-10-15T21:00:00Z
updated_at: 2026-10-15T21:00:00Z
produced_by: test-writer
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
supersedes: null
---

# US-051 Test Strategy — The Google sign-in admits only Admin accounts

## 1. Scope

The decision of `CompleteGoogleSignInUseCase` for an existing account whose role is
not Admin (FR-001…FR-005), what the person sees (FR-006), the log line (FR-007) and
the audit table's new refusal category (FR-008). No endpoint, route or status code
changes (API design NOT_APPLICABLE), so there is no contract test beyond the
existing callback behaviour.

## 2. Test levels

| Level | Where | Why |
|---|---|---|
| Integration over HTTP | `Web/UseCases/DeanGoogleSignInRefusalTests` | The refusal is observable only through the real callback: redirect, absent session cookie, page text, audit row, log line. Control Plane scripted over its real HTTP client, Google stubbed (TC-4); PostgreSQL via Testcontainers (TC-2). |
| Integration, use case with controlled interleaving | `Application/UseCases/GoogleFirstSignInRaceTests` | FR-005 depends on the real unique index rejecting the Admin insert; the competing account is inserted between the first read and the insert through `InterleavingAppUserRepository` (the US-048 pattern) — no timing. |
| Persistence | `Infrastructure/Persistence/NotAnAdminAccountRefusalTests` | AC-008: the CHECK constraint accepts the new code and still rejects unknown ones; the enum ⇄ code mapping round-trips (db-design §6). |
| Localization | `Web/Localization/NotAdminAccountTranslationTests` | FR-006 fixes the exact uk/en texts; NFR-073, TC-8. |

No unit test with fakes: every branch is cheaper and stronger to prove against the
real host, and the existing sign-in suite does the same.

## 3. Scenarios

**Positive (unchanged behaviour, AC-004, AC-006, FR-009):** approved address with no
account → new Admin, signed in; existing enabled Admin → signed in, stamped, success
row; a concurrently created enabled Admin → still signed in; after the refusal the
Dean signs in by password through the forced temporary-password change.

**Negative (AC-001…AC-003, AC-005, AC-007):** approved Dean account → refused, no
session, account unchanged (every column compared as row JSON, including both
stamps), "Dean account" text shown; refusal audited once with the Dean as actor and
`not_an_admin_account`; disabled Dean → `not_an_admin_account`, not
`account_disabled`; Dean with "not approved", "unknown installation", "Control
Plane unavailable" → the pre-v96 categories and texts; a Dean created between the
first read and the Admin insert → refused, unchanged, no Admin row, exactly one
committed audit row; a disabled Admin created in the meantime → `account_disabled`,
not stamped; a disabled Admin → still `account_disabled`.

**Boundary / order of checks (FR-004):** each Control Plane reply combined with a
Dean account; disabled × Dean; disabled × Admin.

**Validation:** none new (VR-001). VR-003 (closed list) covered by the near-miss
codes `not_admin_account` and `NotAnAdminAccount`.

**Security:** SC-3 (Dean never admitted through Google — no session cookie), SC-2
(Dean's counter, lockout, stamps untouched; password path still works), SC-11 (one
row, actor id and role, category, no email — the row JSON has no `@`), SC-10 (log
lines carry no email; request id present), AD-6/BR-026 (read-only theory over all
three causes), I-4 (page shows no address).

**Persistence:** AC-008 accept / reject / round-trip; AC-007 audit row committed on
the race path (spec review M-3) — read on a separate connection, so only committed
data is visible.

## 4. Fixtures

`InstallationTestHost.StartWithControlPlaneHttpAsync` with `ScriptedHttpHandler`
(allowed / not allowed / 404 unknown installation / 500), the Google stub's `Email`,
`InsertDeanAsync` / `InsertAppUserAsync`, `ReadOnlyModeHost.Cause`,
`InterleavingAppUserRepository`. New constants in `SignInTestData`:
`TextKeys.RefusedNotAdminAccount`, `RefusalCategories.NotAnAdminAccount`. The new
key is deliberately not added to `SignInTestData.TextKeys.All`, which is the US-008
`PageTextKeys` contract.

## 5. Excluded scenarios

- A role other than Admin and Dean: none exists in v1 (I-1); the `Role != Admin`
  form cannot be distinguished from `Role == Dean` by a test.
- An exact log event id (2205): the spec does not name it (review M-4); the test
  asserts a Warning under an event name different from every existing sign-in
  event.
- English page rendering of the refusal: the page language path is US-008's; the
  English text is proved at the translation level.

## 6. Known limitations

- AC-006 and the AC-003/AC-004 regression scenarios pass before implementation by
  design — they guard behaviour that must not change.

## 7. Open Decisions

None (`docs/decisions/US-051-open-decisions.md` has none).
