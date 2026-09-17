---
artifact_type: test_strategy
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T11:07:03Z
updated_at: 2026-09-17T11:07:03Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-004-suspend-resume-installation.md
    version: null
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/designs/api/US-004-api-design.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/designs/database/US-004-db-design.md
    version: 1
  - path: docs/designs/database/US-004-entity-model.md
    version: 1
supersedes: null
---

# US-004 Test Strategy — Suspend and resume an Installation

## 1. Scope

The Control Plane pages and behaviour added by US-004: the status action on the
detail page, the suspend and resume confirmations, the two POSTs, the "unchanged"
notice, concurrency, audit rows, authorization, antiforgery and translations.
Nothing in an installation host is exercised; no port is substituted because the
Story calls nothing outside the Control Plane (SC-13).

## 2. Test levels

| Level | Why | Classes |
|---|---|---|
| Integration (host + real PostgreSQL via Testcontainers, TC-2) | every AC is observable through HTTP and the database; the concurrency guarantee exists only in PostgreSQL | `InstallationStatusChangeTests`, `InstallationStatusUnchangedTests`, `InstallationStatusAuditTests` |
| Security (integration) | TC-5 allowed/forbidden role, anonymous, setup gate, antiforgery, GET safety, method set | `InstallationStatusAuthorizationTests`, `InstallationStatusAntiforgeryTests`; `InstallationAuthorizationTests` (US-002) adjusted |
| Localization (TC-8) | every contract key in `uk` and `en`; default language; untranslated values | `InstallationStatusTranslationTests` |
| Unit | none — the transition rule is two rows of a table and its only risk (atomicity) is a database property; a unit test with a substituted `DbContext` would prove nothing (TC-2 forbids InMemory) | — |

Existing host-wide tests cover the new endpoints without change: the anonymous
endpoint enumeration and the global antiforgery enumeration (US-001) pick up every
new route; `TranslationCompletenessTests` checks `uk`/`en` key parity;
`MigrationTests` keeps asserting the three existing migrations, which also guards
db-design §6 (no migration).

## 3. Scenarios

### Positive
- Active installation: detail offers suspend only; suspended: resume only (AC-001).
- Suspend and resume confirmation pages show name, domain, explanation, POST form
  with token, cancel link; they change nothing (AC-002, AC-004).
- Suspend / resume change only `status` and `updated_at` (= fake clock), keep
  identifier, name, domain, client ID, created_at and AllowedAdmin rows, redirect to
  the detail page which offers the opposite action; the list shows the new status
  (AC-003, AC-005).
- Suspend/resume repeated three times, each audited (AC-005, AC-008).
- Matching notice rendered on the detail page (AC-006).

### Negative
- Suspend an already suspended / resume an already active installation → `302`
  with the notice, nothing written (AC-006).
- Same confirmation submitted twice; stale second tab (AC-006).
- Confirmation page for an installation already in the target status → redirect
  with notice (AC-006, spec I-3).
- Notice not matching the status, unknown, script-like, upper-case, repeated →
  not rendered, `200`, value not echoed (AC-006, API design §4).
- Unknown GUID and non-UUID identifiers on all four operations → `404` (AC-007).
- Over-posted `status`, `name`, `domain`, `clientId`, `identifier`, `reason`
  ignored (spec S-03).
- Other installation unaffected.

### Boundary / concurrency
- Five sessions suspend the same installation concurrently → exactly one change,
  one audit row, four "unchanged" redirects, no `500` (AC-006, FR-005).
- Suspend and resume concurrently, five rounds → either one row
  (`installation_suspended`, final suspended) or two rows in order
  (`installation_suspended`, `installation_resumed`, final active); both `302`.

### Security
- No session → `302 /sign-in`; before setup → `302 /setup`; principal without the
  Owner role → `403`; all without change or audit (AC-009, TC-5).
- New route patterns exist, none anonymous, only GET/POST (AC-009).
- POST without token or with another session's token → `400` page expired,
  nothing changed (AC-010).
- GET with query values changes nothing; PUT/PATCH/DELETE change nothing (AC-010).
- Audit rows carry no name, domain or UUID identifier; rows cannot be updated or
  deleted (AC-008, SC-11).
- Name and domain never reach the log file (FR-012, SC-10).

### Validation
There is no user-entered field (spec §6); validation is the route identifier
(VR-001) and the ignored body (VR-002), both covered above.

### Persistence
- `installation` row compared as a whole record before/after (only status and
  `updated_at` differ).
- `updated_at` equals the fake clock after a change — proves the set-based update
  sets it (db-design §5.1, PC-6).
- No migration: covered by the unchanged `MigrationTests`.

## 4. Fixtures

- `PostgreSqlFixture` (assembly fixture) and `ControlPlaneTestHost` of US-001.
- `InsertInstallationAsync(status: …)` of US-002 for a given starting status;
  `InsertAllowedAdminAsync` of US-003.
- New: `InstallationStatusTestData` (paths, element ids, notice values) and
  `InstallationStatusHostExtensions` (`SuspendInstallationAsync`,
  `ResumeInstallationAsync`, `LoadTokenAsync`). The token is taken from
  `/installations/new`, whose form exists regardless of status, so a POST can be
  sent when the confirmation page itself would redirect.
- Several concurrent sessions are separate `SignInAsync` clients of the one Owner.

## 5. Excluded scenarios

- Effect on a running installation (read-only mode, push, legitimacy check) —
  out of scope (US-005 … US-007).
- Showing a reason or a status-change time — not a feature (v71); the detail
  tests assert only what is rendered by contract.
- Exact wording of translations — implementor's choice within the spec; tests
  assert key presence, difference between languages and use on the pages.

## 6. Known limitations

- Concurrency tests show the absence of anomalies over five sessions / five rounds;
  they cannot prove the interleaving where both opposite requests reach the lock
  at once happens on every run. The guarantee rests on the conditional update
  reviewed in db-design §5.2.
- `InstallationNameAndDomain_NeverReachTheLogFile` passes before implementation
  because nothing is logged yet by the missing endpoints; it becomes meaningful
  once they exist.

## 7. Open Decisions affecting testing

None.
