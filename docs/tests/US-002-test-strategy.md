---
artifact_type: test_strategy
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-16T14:02:00Z
updated_at: 2026-09-16T14:02:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-002-register-installation.md
    version: null
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/designs/database/US-002-entity-model.md
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
supersedes: null
---

# US-002 Test Strategy — Register an Installation

## 1. Scope

The Control Plane pages of US-002: the installations list, registration, the
detail page, correcting the name, changing the client ID; uniqueness under
concurrency; audit rows; authorization, antiforgery and translations; the
`installation` table, its constraints, triggers and migration.

Out of scope: anything in an installation (`ClassroomAgent.Web`), the service
channel (US-005), suspension (US-004) — suspended rows are created by SQL only to
prove behaviour that must hold for them.

## 2. Test levels

| Level | Used for | Mechanism |
|---|---|---|
| **H** HTTP integration | every AC: status codes, redirects, form fields, messages, page content | `ControlPlaneTestHost` (`WebApplicationFactory<Program>`) over its own migrated PostgreSQL database; `FormClient` keeps cookies and the antiforgery token |
| **S** security | TC-5: anonymous, forbidden-role (forged principal without the `Owner` role), before-setup gate, antiforgery, GET safety, endpoint enumeration | same host; `HostEndpoint` enumeration |
| **P** persistence | db-design §3: columns, unique indexes, check constraints, triggers, migration order, model drift | raw SQL against the migrated Testcontainers database |

No unit or service-level test: every rule is observable over HTTP or in the
database, and a test naming a production type that does not exist yet would not
compile before IMPLEMENTATION. Tests use only existing production types
(`Program`, `ControlPlaneDbContext`) and raw SQL, so the red phase compiles
without a production skeleton (contrast US-001 OD-007).

All H/S/P tests start a host per test (TC-2: isolated database per test, no
order dependence). Time is the injected `TestTimeProvider`.

## 3. Scenarios

### Positive

- List with rows (name, domain, status label, `dd.MM.yyyy HH:mm UTC`, detail link);
  empty list message; home page link.
- Registration → `302 /installations/{uuid}`; row active, values stored,
  domain lower-cased, `created_at` = clock; distinct UUIDv4 identifiers.
- Detail page shows all six values, selectable identifier, copy button, script,
  configuration note, name and client-ID links.
- Rename changes only the name (and `updated_at`); duplicate names accepted;
  case-only change is a change.
- Client ID change changes only the client ID; leading zero preserved.
- Name and client ID changes allowed on a suspended installation (I-7).

### Negative

- Duplicate domain (same or other case, active or suspended) and duplicate
  client ID → `409` with the field message; both fields when both conflict,
  also when held by different installations.
- Client ID changed to another installation's → `409`.
- Unknown UUID and non-UUID `{id}` → `404` error page, on detail, edit GET and
  edit POST (404 before validation).
- Unchanged name / client ID → `302`, nothing written, no audit row.
- Posted `status`, `identifier`, `id`, `createdAt` ignored (over-posting).

### Boundary and validation (VR-001 … VR-003, OD-001, OD-002)

| Field | Rejected | Accepted |
|---|---|---|
| name | empty; 201 BMP chars; 201 astral code points; leading/trailing space and NBSP; `\n`, `\t`, ZWSP, RLM | 1 char; 200 BMP chars; 200 astral code points (400 UTF-16 units); Cyrillic with `«»№—` |
| domain | empty; 2 chars; 254 chars; scheme; path; `@`; Cyrillic; space; underscore; no dot; leading/trailing/double dot; hyphen at label start/end; 64-char label; `xn--` any case | `a.b`; 253 chars; 63-char label; hyphen inside; label starting with a digit; mixed case → lower |
| client ID | empty; 9 and 33 digits; space; letter; `+`; Arabic-Indic digits | 10, 21 and 32 digits; leading zero |

Each rejected value asserts `400`, its message key, the typed value refilled and
nothing stored. All three fields invalid at once → all three messages.
Rename and client ID forms reuse the invalid name / client ID sets.

### Security (TC-5, SC-4, SC-10, SC-11)

- Each of the 8 operations: anonymous → `302 /sign-in`, nothing changed; before
  setup → `302 /setup`; forged principal without `Owner` role → `403`, nothing
  changed; signed-in Owner → `200` on every page.
- Enumeration: the five `installations…` patterns exist, none anonymous, no
  PUT/PATCH/DELETE; the existing US-001 enumeration and antiforgery tests now
  reach these endpoints (their sample path uses a UUID for `{id}`).
- No token or a token from another session → `400` page expired, nothing
  changed; GET with form values in the query changes nothing; PUT/PATCH/DELETE
  never change or delete.
- Output encoding: markup in a name is encoded on list, detail and form.
- No inline script or inline event handler; the copy script is a static file
  without hard-coded Cyrillic text.
- Logs: rejected and stored names, domains and client IDs never appear in the
  log file.
- Audit: exactly one row per success with Owner actor, `installation` target,
  internal id, `succeeded`, request id, clock time; none for validation, conflict,
  unchanged, unknown or antiforgery refusals; no name, domain, client ID or UUID
  in any row.

### Persistence (db-design §3, §7)

- Columns, types, lengths, nullability, primary key.
- Unique indexes and the constraint name reported on duplicate identifier,
  domain, client ID (the names the services map to conflicts).
- Check constraints by name; upper-case domain rejected; upper bounds accepted.
- Triggers: updating `identifier` or `domain` refused; updating name, client ID,
  status allowed; delete refused; both triggers present.
- Migrations `InitialOwnerAndAudit` then `AddInstallation`; no pending model
  changes (existing test).

### Concurrency (AC-004)

Two signed-in sessions of the Owner post registrations at the same time with the
same domain (different case) or the same client ID: exactly one `302` and one
`409` with the field message, one row, one `installation_created` audit row.
The assertion holds whichever request wins and whether the loser is caught by the
pre-check or by the unique constraint.

## 4. Fixtures

- `PostgreSqlFixture` (assembly fixture, `postgres:17-alpine`), one database per
  test.
- `ControlPlaneTestHost`, `FormClient`, `TestTimeProvider` (US-001).
- New: `InstallationTestData` (synthetic `*.example.test` domains, fake client
  IDs, a fixed unknown UUID, the `uk` time format), `InstallationRow`,
  `InstallationHostExtensions` (register over HTTP, read/insert rows by SQL, set
  status, forge a session without the `Owner` role).
- Changed: `HostEndpoint.SamplePath` substitutes a UUID for `{id}` so guid-
  constrained routes are reached by the enumeration tests.
- Changed: `MigrationTests` — the US-001 assertion "only owner and audit_event,
  exactly one migration" becomes "owner, audit_event, installation; migrations
  in order". The US-001 guarantee (tables and trigger of the first migration)
  is kept.

## 5. Excluded scenarios

| Scenario | Why |
|---|---|
| Copy button actually writing to the clipboard | needs a browser; the contract (button attributes, script served, selectable text fallback) is asserted instead |
| English page rendering | the Owner cannot switch language before US-039; key completeness in `en` is asserted |
| Two renames of the same installation at once | last-write-wins by design (db-design §5), no requirement to detect it |
| Service-level unit tests of the registry | behaviour fully observable over HTTP and in the database; avoids naming unbuilt types |

## 6. Known limitations

- The concurrency tests cannot force both requests past the pre-check; they
  prove the outcome, and the persistence tests prove the constraint names the
  lost-race mapping relies on.
- "Logs never contain values" covers the paths exercised by the test, as in
  US-001.

## 7. Open Decisions affecting testing

None. OD-001 … OD-003 are resolved (open decisions v2) and asserted: label rules
and `xn--` (OD-001), `Cc`/`Cf` in names (OD-002), UTC minute format (OD-003).
