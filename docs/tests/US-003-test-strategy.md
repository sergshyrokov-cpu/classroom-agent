---
artifact_type: test_strategy
story: US-003
version: 1
status: DRAFT
created_at: 2026-09-17T10:05:00Z
updated_at: 2026-09-17T10:05:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-003-manage-allowed-admins.md
    version: null
  - path: docs/specifications/US-003-spec.md
    version: 1
  - path: docs/designs/api/US-003-api-design.md
    version: 1
  - path: docs/designs/api/US-003-openapi.yaml
    version: 1
  - path: docs/designs/database/US-003-db-design.md
    version: 1
  - path: docs/designs/database/US-003-entity-model.md
    version: 1
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
supersedes: null
---

# US-003 Test Strategy — Manage AllowedAdmin entries

## 1. Scope

Control Plane only. Covered: the Admins section of the installation detail page,
the add form and submission, the revoke confirmation and submission, the
fewer-than-two warning and note, 404 handling, audit rows, authorization,
antiforgery, translations, logging, and the `allowed_admin` schema (constraints,
indexes, foreign keys, triggers, migration). Not covered: anything in a school
installation, the Admin login check (US-008), suspending (US-004).

## 2. Test levels

| Level | Used for | Mechanism |
|---|---|---|
| HTTP integration | every operation of the contract: status, redirect target, rendered values, message keys, stored rows, audit rows | `ControlPlaneTestHost` (`WebApplicationFactory`) over its own migrated PostgreSQL database (TC-2); `FormClient` without auto-redirect |
| Security | TC-5: allowed / unauthenticated / principal without `Owner` role per operation; before-setup gate; antiforgery; GET safety; other methods; no anonymous route; rejected and stored emails absent from logs | same host |
| Persistence | db-design §3: columns, PK, unique indexes and their names, FKs with RESTRICT, check constraints, domain-match and no-update triggers, delete allowed; migration order | raw SQL through Npgsql against the migrated database |
| Unit | none — every rule is observable through HTTP or the schema; a service-level unit test would add no evidence beyond the integration tests (TC-1 note) | — |

No Google port is involved; nothing is substituted (TC-4 trivially holds).

## 3. Scenarios

### Positive
- Entries of an installation listed with email and `dd.MM.yyyy HH:mm UTC` date,
  ordered by email; revoke link per entry; add link always.
- Adding a mixed-case email stores one lower-case entry with the Owner id and the
  fake clock's time, redirects to the detail page.
- Valid name-part shapes (`'`, `-`, `_`, one character, 64 characters).
- Twelve entries accepted (no limit).
- Adding and revoking on a suspended installation; the installation row unchanged.
- Confirmation page content; revoking deletes only that entry; revoking the last
  entry; re-adding a revoked email creates a new entry (new id, identifier, time).
- The same name part in two installations (different domains) is two entries.

### Negative
- Duplicate email (same case and other case) → `409 Taken`, value refilled.
- Foreign domains (personal, other school, subdomain, parent, trailing dot,
  trailing space) → `400 WrongDomain` naming the expected domain.
- Unknown installation, unknown entry, entry of another installation → `404` on
  GET and POST; non-GUID route values → `404`.
- Revocation submitted twice → second `404`; one audit row.
- Unknown installation with invalid email → `404` (existence before validation).

### Boundary
- Email 255 characters → `Length`; name part 64 accepted, 65 → `NameLength`.
- Warning with 0, 1 (shown) and 2, 3 (hidden) entries; confirmation note with 1, 2
  (shown) and 3 (hidden) entries (I-10: current count ≤ 2).

### Validation (first failing rule, API design §5)
`Required`, `Length`, `Format` (no `@`, two `@`, empty name part, empty domain
part), `NameLength`, `NameCharacters` (`+`, Cyrillic, inner space, leading space,
non-ASCII Latin, and `iv..an+` proving character rule before dot rule),
`NameDots` (leading, trailing, double), `WrongDomain`, `Taken`.

### Security
- Every operation: unauthenticated → `302 /sign-in`, nothing changed; before setup
  → `302 /setup`; forged session without `Owner` role → `403` error page;
  signed-in Owner → `200` for pages.
- New route patterns exist, none anonymous, no PUT/PATCH/DELETE; the US-001
  enumeration tests (anonymous list, antiforgery on every non-GET) now include
  them — `HostEndpoint.SamplePath` substitutes a UUID for `{adminId}`.
- Add and revoke without token or with another session's token → `400`, nothing
  changed; GETs with form-like query values change nothing; PUT/PATCH/DELETE do
  not add or revoke.
- Emails HTML-encoded (apostrophe, markup refilled into the input); confirmation
  page has no inline script or handler.
- Rejected, added, duplicate and revoked emails never appear in the log files.
- Audit rows carry no email, name part, installation name, domain or UUIDs; the
  revoked entry's audit row cannot be updated or deleted.

### Persistence
Columns and types; `pk_allowed_admin`; `uq_allowed_admin_identifier`,
`uq_allowed_admin_installation_email` (violation names asserted),
`ix_allowed_admin_added_by_owner_id`; `fk_allowed_admin_installation` and
`fk_allowed_admin_added_by_owner` with RESTRICT; `ck_allowed_admin_email_format`
and `_lower`; domain-match trigger refuses foreign domains; no-update trigger
refuses every update (including a no-op assignment); delete allowed; migration
`AddAllowedAdmin` third in order; no model drift.

### Concurrency
- Two concurrent additions of one email in different case → one `302`, one `409`,
  one row, one `allowed_admin_added` row (database constraint decides).
- Two concurrent revocations → one `302`, one `404`, one `allowed_admin_revoked`
  row.

## 4. Fixtures

- `PostgreSqlFixture` (assembly fixture, one container, a database per test).
- `ControlPlaneTestHost` with `TestTimeProvider`; the Owner created through
  first-run setup (`CreateOwnerAsync`); a second session via `SignInAsync`.
- New: `AllowedAdminTestData` (synthetic `*.example.test` emails, paths),
  `AllowedAdminRow`, `AllowedAdminHostExtensions` (add over HTTP, revoke over HTTP,
  read rows, insert rows directly with a chosen time).
- Reused: `InstallationHostExtensions` (insert installation, set status, forge a
  session without the role).

## 5. Existing tests changed

| Test | Change | Why |
|---|---|---|
| `MigrationTests.Migrations_…_InOrder` | expects `allowed_admin` and a third migration `_AddAllowedAdmin` | db-design §7, §8 |
| `InstallationAuthorizationTests.InstallationEndpoints_ExistAndNoneAllowsAnonymous` | excludes `installations/{id}/admins…` patterns from its exact US-002 list | the new routes are asserted by `AllowedAdminAuthorizationTests`; the US-002 list stays exact |
| `HostEndpoint.SamplePath` | `{adminid}` gets a UUID like `{id}` | otherwise the enumeration tests would hit the 404 catch-all instead of the new endpoints |

## 6. Excluded scenarios

- An email in two installations — impossible while `uq_installation_domain` holds
  (db-design §8).
- Deleting an Owner or installation with entries — no use case; both are
  protected (RESTRICT, US-002 no-delete trigger).
- Clipboard / script behaviour — this Story adds no script.
- Non-UTC display — the Control Plane shows UTC only (US-002 OD-003); TC-8's time
  zone case does not apply.

## 7. Known limitations

- Concurrency tests rely on two real requests racing; the database constraint and
  the affected-row count make the outcome deterministic regardless of which wins.
- `WrongDomain` wording is checked by the text before its `{0}` argument plus the
  domain; the exact sentence is the implementor's.

## 8. Open Decisions affecting testing

None.
