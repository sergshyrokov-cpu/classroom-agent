---
artifact_type: test_strategy
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T14:05:00Z
updated_at: 2026-09-20T14:05:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-009-configure-workspace-connection.md
    version: null
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/designs/api/US-009-api-design.md
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/designs/database/US-009-db-design.md
    version: 1
  - path: docs/designs/database/US-009-entity-model.md
    version: 1
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-009 Test Strategy — Configure WorkspaceConnection

## 1. Scope

Everything the Story adds: the connection settings page and its save, the BR-020
invariant, validation, the audit rows, the read-only refusal, the new table and
the amended audit constraints, and the Ukrainian and English translations.

Out of scope, because the Story is: any Google call (there is none to stub), the
connection instructions (US-010), "check access" (US-011), Dean accounts
(US-012) and the Control Plane, whose schema and endpoints this Story leaves
untouched.

## 2. Test levels and the seam each uses

### 2.1 Everything is driven through the running host

Every behavioural test goes through `InstallationTestHost` over HTTP, with a
real migrated PostgreSQL database (TC-2) and the Control Plane answering the
US-008 Admin login check over `ScriptedHttpHandler` so an Admin can sign in
(TC-4). That is the seam this Story's behaviour is actually observable at: the
page, the form, the status codes of api-design §5, the stored row and the audit
rows.

No test references a production type this Story introduces. That is deliberate
and it is the same constraint US-008 worked under: TEST_WRITING may not create
production source, so a test naming `SaveWorkspaceConnectionUseCase` would not
compile. The cost is recorded in §8.

### 2.2 The authorization policy is proven as a policy, not only as a page

A Dean cannot sign in yet — US-012 brings the password form — so the
forbidden-role half of TC-5 is proven by asking the host's own
`IAuthorizationService` to authorise a synthetic Dean principal against the
policy name, and an Admin principal against the same policy. This is the real
production policy object, evaluated the way the pipeline evaluates it; only the
principal is synthetic. The endpoints are separately asserted to declare that
policy and to allow no anonymous access.

### 2.3 The database is asserted in SQL

Constraints, the singleton index, the absence of other indexes and of foreign
keys, and the amended audit constraints are asserted by inserting rows directly
and reading `information_schema` / `pg_constraint`. Where db-design names a
constraint, the test asserts the **constraint name** in the `PostgresException`,
so a row cannot be rejected by the wrong rule and still pass.

### 2.4 Logs are read after the host stops

`ReadLogEventsAsync` stops the host and parses its Serilog JSON files. Nothing
in this Story is written by a background activity, so the stop-then-read shape
is safe here; the `WaitForLogEventAsync` form is not needed.

## 3. Positive scenarios

- an Admin opens the settings page and sees the school's domain, the state and
  the two explanatory sentences;
- a valid save redirects back to the page, stores exactly the domain and the
  technical account, and shows them on reopening with a confirmation;
- an address in mixed case with surrounding spaces is stored normalised;
- every equivalent spelling of the allowed domain — as typed, upper case, with a
  trailing dot, padded — is accepted;
- a second save changes the one record and keeps its identity;
- saving the identical values again is accepted;
- saving again clears an OD-002 domain mismatch;
- the landing page carries the navigation entry that leads to the page.

## 4. Negative scenarios

- anonymous access is redirected to sign-in; a Dean principal is refused by the
  policy;
- a save without the antiforgery token is `400` and writes nothing;
- an address outside the installation's domain — a neighbouring school, a
  personal address, a subdomain — is `409` and writes nothing;
- a stored connection bound to another domain does not license a save into that
  domain;
- a save while no legitimacy check has ever succeeded is `409` and writes
  nothing, and a domain recorded by an `upgrade_required` answer does not
  license it either;
- in each of the three read-only causes the save is `409`, the table is
  untouched and a stored connection is left alone;
- a malformed request is `400` and reaches no audited action.

## 5. Boundary scenarios

- an address of exactly 254 characters is accepted; one character more is `400`;
- a single-label domain, a label starting or ending with a hyphen, and a
  trailing dot are rejected by the database constraints;
- a subdomain of the allowed domain is not the allowed domain — the one case
  where "contains" and "equals" differ and the rule must equal.

## 6. Validation scenarios

The eleven malformed addresses of `WorkspaceConnectionTestData.MalformedAddresses`
cover VR-001 and VR-002: empty, blank, no `@`, two `@`, empty name part, empty
domain part, whitespace in either part, a single-label domain and a hyphen at a
label edge. A request without the field at all is covered separately, because an
absent field must not be read as an empty one.

The rejected value is asserted **absent from the log** in three shapes: a
malformed value, a refused foreign address and a successful save.

## 7. Security scenarios

| Requirement | How it is proven |
|---|---|
| S-01 the policy, both roles | `IAuthorizationService` with an Admin and a Dean principal; endpoints declare the policy; anonymous redirected |
| S-02 BR-020 in `Application` | a domain sent in the request body is ignored and the row is still bound to the installation's own domain |
| S-03 the domain comes from `LegitimacyState` | the never-confirmed and `upgrade_required` cases refuse the save |
| S-04 no credential column | `information_schema` shows no column whose name contains key, secret, credential, password, token or client id |
| S-05 read-only | three causes, `409`, nothing written, `PermittedServiceWrite` and its registry unchanged |
| S-06 audit without personal data | every audit row's JSON carries no `@` and no domain |
| S-08 antiforgery | no exemption metadata on either endpoint; a save without a token is `400` |
| S-09 log hygiene | no log line carries the typed address |
| S-10 nothing leaves the installation | the single outbound transport records no request during a save |

## 8. Known limitations

- **No unit test names a US-009 production type.** TEST_WRITING may not create
  production source, so the use case, the query, the comparison rule and the
  request model are exercised only through the host. IMPLEMENTATION should add
  the direct unit tests TC-1 prefers — above all for the domain comparison rule,
  which is a pure function and deserves its own table of cases.
- **`DomainNotConfirmed` cannot be separated from the read-only refusal over
  HTTP.** An installation that has never confirmed its legitimacy is always in
  read-only mode (US-007), and the guard runs first, so the refusal the Admin
  sees is the read-only one. The tests therefore accept **either** category in
  that case and assert everything else exactly. Proving the branch on its own
  requires a direct unit test of the use case, which IMPLEMENTATION must add;
  the Specification's AC-010 wording ("neither hides the other") cannot be
  satisfied at the HTTP level by this Story's own design.
- A concurrent double first-save (`23505` → re-read) is asserted at the database
  level only. Driving two simultaneous saves through one host would be timing
  dependent, which TC-6 forbids.
- The `409` of a state refusal is asserted as a status and a message; whether
  the response is the re-rendered page or the error page is asserted only where
  api-design fixes it (the read-only case renders the error page).

## 9. Fixtures

| Fixture | Role |
|---|---|
| `PostgreSqlFixture` | the real database per test class (TC-2) |
| `InstallationTestHost` | the migrated installation host, manual clock, scripted Control Plane |
| `WorkspaceConnectionHostExtensions` | signs an Admin in, opens the page, posts the save, reads the rows |
| `WorkspaceConnectionTestData` | the path, the field name, addresses, audit codes, translation keys |
| `ReadOnlyModeHost` | the three read-only causes of BR-025, reused unchanged |
| `ScriptedHttpHandler` | the single outbound seam; also how "no call left the installation" is asserted |

The save helper opens the settings page for an antiforgery token and falls back
to the landing page while that page does not exist, so a red-phase POST fails on
the production behaviour under test rather than on the fixture.

## 10. Open Decisions affecting testing

None. OD-001 and OD-002 were resolved before SPECIFICATION and the tests are
written against both resolutions: the save request carries no domain field
(OD-001), and a stored connection whose domain no longer matches is reported and
left alone (OD-002).
