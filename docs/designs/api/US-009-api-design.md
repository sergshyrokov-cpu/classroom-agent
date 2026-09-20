---
artifact_type: api_design
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T13:32:00Z
updated_at: 2026-09-20T13:32:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
supersedes: null
---

# US-009 API Design — Configure WorkspaceConnection

Companion to `docs/designs/api/US-009-openapi.yaml`.

## 1. Scope of the contract

Two operations, both on the installation's public port, both server-rendered:

| Operation | Purpose |
|---|---|
| `GET /settings/workspace-connection` | the connection settings page |
| `POST /settings/workspace-connection` | save the school's technical account |

Nothing else changes. The Control Plane gains no endpoint — a school's
connection is its own data and is never reported (spec S-11). The installation's
private port is untouched. The host-wide rules US-008 established apply
unchanged and are listed once in the contract's `x-host-wide-rules` rather than
repeated per operation.

The landing page gains a link to the settings (spec FR-013). A navigation entry
is not an operation: `GET /` keeps the contract US-008 gave it.

## 2. Decisions this stage made

### 2.1 No `/api/v1` operation, and none is missing

`trebovaniya.md` §8 puts screens on Razor and reserves `/api/v1` for what a
client consumes as data. The Admin edits this setting in a form, and no client
reads it, so a JSON resource would exist only to be documented. The `409`
read-only mapping this Story depends on is the host-wide one US-008 configured
and proved end to end; nothing here re-declares it (spec FR-008).

When a later Story needs the connection as data — a diagnostic client, an
installer — it adds `/api/v1/workspace-connection` then, and the request and
response schemas of this contract are the shapes to reuse.

### 2.2 The path is singular

`/settings/workspace-connection`, not `/settings/workspace-connections`. API-3's
plural rule is about collections; there is exactly one connection per
installation and no way to address a second (`trebovaniya.md` §3, spec FR-001).
A plural path would promise a collection the database forbids.

`/settings` itself is **not** an endpoint in this Story: the section exists in
the navigation, not as a page of its own. US-010, US-011 and US-012 each add
their page under the same prefix, and the first Story that needs a settings
landing page adds it then (spec I-10).

### 2.3 The domain is not a field of the request

OD-001 is resolved: the domain is displayed, not typed. The request therefore
carries **one** field, the impersonation user's email, and the request schema is
`additionalProperties: false` — a domain arriving under any name has no property
to bind to (spec VR-003).

This is a UI decision, not a relaxation of the control. The BR-020 check stays in
`Application` and is applied to the value that will be written, which the use
case reads from `LegitimacyState` at the moment of the save (spec FR-006 step 5).
The contract documents that refusal as a reachable `409` because it is reachable
— by a crafted request, by a later API client, or by a `LegitimacyState` that
changed between the page render and the post.

### 2.4 `409` is the answer for every state refusal, `400` for a malformed request

The split is the one API-5 draws, applied consistently:

| Kind of failure | Status |
|---|---|
| The request violates a stated constraint (VR-001…VR-003), or the antiforgery token is missing or invalid | `400` |
| The request is well formed and the installation's state refuses it — domain mismatch, impersonation domain mismatch, domain not yet confirmed, read-only mode | `409` |

Read-only mode is already `409` by API-5 and by US-008's handler. Making a
BR-020 refusal `409` as well means **every** "the installation refuses this
because of its state" answer carries one status, which is what a test asserts and
what an operator reads in the access log. A `400` there would say the Admin sent
something malformed, which they did not: the address may be perfectly well
formed and simply belong to another domain.

`403` stays what it is — the caller's *role* is wrong (a Dean) — and is never
used for a refusal about state.

### 2.5 Success redirects; a refusal re-renders

A successful save answers `302` back to the page (Post-Redirect-Get): a reload
must not re-submit a form that writes an audit row. The confirmation travels in
TempData, protected by Data Protection, never as a query parameter — the rule
US-008 set when a crafted link could otherwise render arbitrary text on the
school's own page.

A `400` or a `409` re-renders the page in place, with the typed address still in
the form so it can be corrected (spec FR-005). Redirecting a refusal would mean
carrying that address through TempData to redisplay it, which puts a school
account's address into a cookie for no gain.

### 2.6 The view model carries keys, not sentences

Every message in `WorkspaceConnectionPageModel` — the read-only reason, the
confirmation, the refusal, each field error — is a translation **key**.
`Application` holds no user-visible string (AD-6), presentation resolves the key
against the Ukrainian and English files (NFR-073, spec FR-011), and the domain
and the address are data that are rendered as stored and never translated.

### 2.7 One state value, plus one boolean

`WorkspaceConnectionState` has four members and the page needs all four.
Consumers that only care whether they may act — synchronization (EPIC-1), "check
access" (US-011) — read `connectionUsable`, which is true only for `Configured`.
Two representations of one fact is a duplication worth its cost here: OD-002's
whole point is that a caller must not be able to forget the mismatch case, and a
boolean cannot be forgotten by a `switch` that grows a fifth member later.

## 3. Operation notes

### `GET /settings/workspace-connection`

- Policy `ConfigureWorkspaceConnection` — the permission-matrix row, Admin `✔`,
  Dean `✘` (spec FR-010). Anonymous visitors are challenged to `/sign-in`, which
  is the host's existing behaviour, not a new anonymous endpoint.
- Writes nothing, including when the state is `DomainMismatch` (spec I-5).
- In read-only mode it answers `200` exactly as otherwise, with the reason
  rendered. Hiding or disabling the form is explicitly not how read-only mode is
  expressed (AD-6, TC-5).
- The response's `Cache-Control` is the host default for authenticated pages: no
  store. The page shows a school account's address and belongs in no shared
  cache.

### `POST /settings/workspace-connection`

- Same policy; antiforgery required through the global filter, with no exemption
  added (spec S-08).
- Order of refusals is the Specification's and is observable through the status
  and the audit category: read-only (`409`) before domain-unknown (`409`) before
  the two mismatches (`409`); malformed input (`400`) never reaches any of them
  because validation runs first.
- Exactly one audit row per request, whatever the outcome (spec FR-009), written
  inside the same transaction as the connection on success.
- A `GET` on this path is the page, not a save — there is no method-mismatch
  branch to document, because both methods are defined here.

## 4. Authentication and authorization model

| Operation | Authentication | Authorization |
|---|---|---|
| `GET /settings/workspace-connection` | installation session cookie | `ConfigureWorkspaceConnection` (Admin) |
| `POST /settings/workspace-connection` | installation session cookie | `ConfigureWorkspaceConnection` (Admin) |

- Deny by default remains the host rule; both operations declare their policy
  explicitly rather than relying on it (API-9, SC-4).
- The SC-4 anonymous closed list is unchanged, and the US-008 enumeration test
  passes unchanged.
- Each operation needs an allowed-role and a forbidden-role test (TC-5).

## 5. Error model

| Status | When | Body |
|---|---|---|
| `302` | saved | redirect to the page |
| `302` | not signed in | redirect to `/sign-in` |
| `400` | VR-001…VR-003, or antiforgery | the page with per-field messages |
| `403` | signed in as a Dean | the translated error page |
| `409` | domain mismatch, impersonation domain mismatch, domain not confirmed | the page with the refusal message |
| `409` | read-only mode | the translated error page, reason named (US-008 handler) |
| `500` | anything unmapped | the error page with no internals |

No response carries a stack trace, SQL, a type name, a path, a configuration
value or the rejected input (NFR-023, SC-10). The API-6 JSON body appears
nowhere in this Story, because no `/api/v1` path is added.

## 6. Acceptance Criterion → operation map

| Story AC | Operation |
|---|---|
| AC-001 Only an Admin reaches the connection settings | both |
| AC-002 An unconfigured installation says so plainly | `GET` |
| AC-003 Saving a valid connection stores the domain and the technical account | `POST` `302` |
| AC-004 A domain the Owner did not approve is refused | `POST` `409` `DomainMismatch` |
| AC-005 A technical account outside the domain is refused | `POST` `409` `ImpersonationDomainMismatch` |
| AC-006 Malformed input is rejected before business logic | `POST` `400` |
| AC-007 Changing the connection updates the one record | `POST` `302` (second save) |
| AC-008 Saving and changing are audited without personal data | `POST`, every outcome |
| AC-009 Read-only mode blocks the save and keeps the view | `GET` `200` + `POST` `409` |
| AC-010 An installation that has never been legitimated cannot be configured | `GET` `200` (`DomainUnknown`) + `POST` `409` `DomainNotConfirmed` |
| AC-011 Every new string is translated | both, in both languages |
| AC-012 The schema change ships as a migration | — (DB_DESIGN) |

## 7. Compatibility

- **Additive.** No existing path, request or response changes shape. The
  contract version stays `1` (DC-12), and `ClassroomAgent.Contracts` gains
  nothing — the service channel, the legitimacy check and the push are untouched.
- US-008's host-wide rules are inherited, not restated, and none is modified.
  In particular the anonymous catch-all still matches every unmatched path, which
  is why US-008 finding F-5 (a wrong method answering `404` where a contract
  documents `405`) stays open; this contract documents no `405` and so does not
  extend it.
- The private port contract of US-005 and US-006 is unchanged.

## 8. Open questions for later stages

- **DB_DESIGN** owns the singleton table, its check constraints and the
  migration (spec FR-012). Nothing in this contract constrains column types
  beyond the lengths the request schema already states.
- **TEST_WRITING** should assert the status codes of §5 directly, including that
  a `409` of a state refusal leaves the table unchanged, and that the `400` path
  writes neither a row nor a log line carrying the typed value.
- The `fieldErrors` shape mirrors API-6's optional `fieldErrors` so that a later
  `/api/v1` resource can reuse it without inventing a second shape.
