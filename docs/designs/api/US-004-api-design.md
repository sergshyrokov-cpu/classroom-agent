---
artifact_type: api_design
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T10:56:00Z
updated_at: 2026-09-17T10:56:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/decisions/US-004-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 71
  - path: docs/designs/api/US-003-openapi.yaml
    version: 1
supersedes: null
---

# US-004 API Design — Suspend and resume an Installation

Contract: `docs/designs/api/US-004-openapi.yaml` (`info.version: "1"`).

## 1. Rationale

- **No `/api/v1` endpoint.** As in US-001 … US-003, every screen is a
  server-rendered Razor page and every change an HTML form post. No script is
  involved (confirmations are pages, spec I-1). No response uses the API-6 JSON
  body: errors are the host's error page (AD-9, SC-4 v66).
- **Why a contract.** `test-writer` needs fixed routes, status codes, redirect
  targets, element ids and message keys before the code exists (TC-3, TC-5).
- **Not `NOT_APPLICABLE`.** The Specification defines pages, forms and status
  codes.
- **Builds on US-001 … US-003.** The host-wide rules of `US-001-openapi.yaml`, the
  `{id}` route rule and output encoding of `US-002-openapi.yaml`, and the
  confirmation-page pattern of `US-003-openapi.yaml` apply unchanged.

## 2. Routes

| Method | Path | Anonymous | Policy | Purpose | Spec |
|---|---|---|---|---|---|
| GET | `/installations/{id}` (changed) | no | `Owner` | detail page gains the status action and the "unchanged" notice | FR-001, FR-004 |
| GET | `/installations/{id}/suspension` | no | `Owner` | suspend confirmation | FR-002 |
| POST | `/installations/{id}/suspension` | no | `Owner` | suspend | FR-003 |
| GET | `/installations/{id}/resumption` | no | `Owner` | resume confirmation | FR-002 |
| POST | `/installations/{id}/resumption` | no | `Owner` | resume | FR-003 |

- `{id}` is the installation's UUID identifier, as in US-002.
- `suspension` and `resumption` are noun sub-resources of the installation (API-3:
  no verbs), like `revocation` in US-003: GET is the confirmation, POST performs it.
- Two separate endpoints, not one status toggle and not a posted target status
  (spec I-1): a stale submission can only be "unchanged", never the opposite
  action. Nothing about the target is read from the request.
- No `PUT`/`PATCH`/`DELETE`: HTML forms post.

## 3. Host-wide behaviour

Unchanged from US-001 §3, US-002 §3 and US-003 §3. Consequences:

- **Before setup** every route answers `302 /setup`.
- **No session** → `302 /sign-in`, before antiforgery.
- **Non-GUID `{id}`** matches no route → anonymous `404` catch-all,
  `Error.NotFound`.
- **Well-formed GUID that identifies nothing** → `404`, `Error.NotFound`.
- **Antiforgery refusal** → `400`, error page `Error.PageExpired`.
- **Principal without the `Owner` role** → `403`, `Error.Forbidden`.

## 4. The "unchanged" notice

Spec FR-004 and I-2 need a one-time message on the detail page after a redirect.
Mechanism: a **query parameter with a closed set of values**, not TempData:

- `?notice=already-suspended` or `?notice=already-active` on the redirect to
  `/installations/{id}`.
- The detail page renders the notice **only if the value matches the
  installation's current status** — `already-suspended` only while suspended,
  `already-active` only while active. So the message can never state a status the
  installation no longer has, even when the URL is reloaded or bookmarked later.
- Any other value, a repeated parameter, or a value not matching the current
  status renders no notice and is not an error (`200`). The parameter's value is
  never echoed: the text comes from a fixed translation key.
- Why not TempData: it adds a cookie (with its own attributes to secure, SC-4) and
  session-scoped state that is harder to assert in tests; the query parameter
  carries no data, only a choice between two fixed keys.

| Value | Rendered when | Key | Element |
|---|---|---|---|
| `already-suspended` | status is suspended | `Installation.Status.AlreadySuspended` | `id="installation-status-notice"` |
| `already-active` | status is active | `Installation.Status.AlreadyActive` | `id="installation-status-notice"` |

## 5. Operations

### GET /installations/{id} (changed)

Everything of US-002 and US-003 stays. Added, next to the status value:

- **active** → a link `Installation.Suspend` to `/installations/{id}/suspension`,
  in an element with `id="installation-suspend"`; no resume link;
- **suspended** → a link `Installation.Resume` to `/installations/{id}/resumption`,
  in an element with `id="installation-resume"`; no suspend link;
- the notice of §4 when the query parameter matches.

Still no reason and no status-change time (FR-001). The installations list
`GET /installations` is unchanged (I-8).

Query parameter `notice` is optional; it never produces `400`.

### GET /installations/{id}/suspension

- **Status active** → `200`: installation name and domain; explanation
  `Installation.Suspend.Explanation` (read-only mode — viewing and export keep
  working, synchronization and configuration stop; no data is deleted; the Admins
  in the list stay); a form posting to the same path with the antiforgery token
  and button `Installation.Suspend.Confirm`; a link `Installation.Suspend.Cancel`
  to `/installations/{id}`.
- **Status already suspended** → `302 Location: /installations/{id}?notice=already-suspended`
  (spec I-3).
- Changes nothing, audits nothing.
- `404`: unknown installation.

### POST /installations/{id}/suspension

| Step | Check | Result | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page `Error.PageExpired` | — |
| 2 | installation exists | `404`, error page `Error.NotFound` | — |
| 3 | in one transaction: set status `suspended` **where** status is `active` (conditional on the stored value); if one row changed, write the audit row | row changed → commit; zero rows → nothing written | changed: succeeded, `installation_suspended` |
| 4 | redirect | changed: `302 /installations/{id}`; unchanged: `302 /installations/{id}?notice=already-suspended` | — |

The form carries no field; any posted field is ignored.

### GET /installations/{id}/resumption

- **Status suspended** → `200`: installation name and domain; explanation
  `Installation.Resume.Explanation` (the school returns to normal work); a form
  posting to the same path with the antiforgery token and button
  `Installation.Resume.Confirm`; a link `Installation.Resume.Cancel` to
  `/installations/{id}`.
- **Status already active** → `302 Location: /installations/{id}?notice=already-active`.
- Changes nothing, audits nothing.
- `404`: unknown installation.

### POST /installations/{id}/resumption

| Step | Check | Result | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page `Error.PageExpired` | — |
| 2 | installation exists | `404`, error page `Error.NotFound` | — |
| 3 | in one transaction: set status `active` **where** status is `suspended`; if one row changed, write the audit row | row changed → commit; zero rows → nothing written | changed: succeeded, `installation_resumed` |
| 4 | redirect | changed: `302 /installations/{id}`; unchanged: `302 /installations/{id}?notice=already-active` | — |

### Concurrency (spec FR-005)

Step 3 is a single conditional update; PostgreSQL row locking serializes
concurrent updates of the same row, and the second re-evaluates the condition
against the committed status. Hence:

- two suspends → one `302 /installations/{id}` with one audit row, one
  `302 …?notice=already-suspended` with none;
- a suspend and a resume → each applies only if the status is its precondition at
  its turn; audit rows = status changes made;
- neither request is ever `500` or `409`.

## 6. Request model

Neither POST binds a model. `SuspendInstallationRequest` and
`ResumeInstallationRequest` are empty: a posted `status`, `name`, `domain`,
`clientId`, `identifier` or any other field is ignored (over-posting, S-03).

## 7. Response models (view models)

DTOs only (AD-8), mapped in `ControlPlane.Services`:

- `InstallationDetailDto` (US-002, extended by US-003) — unchanged fields; it
  already carries `Status`. The view decides which link to show from `Status`
  and whether to show the notice from `Status` and the `notice` parameter. No new
  DTO field is required; the page model gains `Notice` (nullable, closed set).
- `InstallationStatusConfirmationDto` — installation `Identifier`, `Name`,
  `Domain`, `Status` (used by both confirmation pages; the service or controller
  redirects when `Status` already equals the target).
- `InstallationStatusChangeResult` — `NotFound` | `Changed` | `Unchanged` (AD-9:
  results, not exceptions).

Names and domains are rendered with Razor's default HTML encoding; no `Html.Raw`.

## 8. Auth model

- Every operation: policy `Owner` (US-001), declared on the controller (API-9).
  None anonymous; no antiforgery exemption; no new static file.
- TC-5 forbidden-role case: unauthenticated request (`302 /sign-in`) plus a
  principal lacking the `Owner` role (`403`, error page), as in US-002 and US-003.

## 9. Error model

| Form | Used for |
|---|---|
| Error page | `400` antiforgery (`Error.PageExpired`), `403` (`Error.Forbidden`), `404` (`Error.NotFound`), `500` (`Error.Internal`) |
| Redirect with notice | "unchanged" — not an error |

No page or log line contains the installation's name or domain beyond the rendered
page values (SC-10).

## 10. Acceptance Criterion → operation map

| AC | Operations | Key assertions |
|---|---|---|
| AC-001 | GET `/installations/{id}` | active: `installation-suspend` link present, `installation-resume` absent; suspended: the reverse; no reason or status-change time rendered |
| AC-002 | GET `…/suspension` | `200` with name, domain, `Installation.Suspend.Explanation`, confirm form, cancel link to detail; status unchanged, no audit row after GET |
| AC-003 | POST `…/suspension` | `302 /installations/{id}`; status `suspended`; identifier, name, domain, created_at, client_id and `allowed_admin` rows unchanged; detail shows resume link; list shows suspended |
| AC-004 | GET `…/resumption` | `200` with name, domain, `Installation.Resume.Explanation`, confirm form, cancel link; nothing changed |
| AC-005 | POST `…/resumption` | `302 /installations/{id}`; status `active`; other columns and entries unchanged; suspend/resume repeatable |
| AC-006 | POST either on target status; GET confirmation on target status; concurrent POSTs | `302 …?notice=already-…`; no change, no audit row; notice element rendered with matching key; notice not rendered when parameter mismatches status; two concurrent suspends → one row; suspend+resume concurrently → rows = changes, never `500` |
| AC-007 | all four new operations | unknown GUID → `404`; non-GUID → `404`; nothing changed or audited |
| AC-008 | both POSTs | one row per change: Owner actor, target `installation` + internal id, `installation_suspended` / `installation_resumed`; none on GET, `400`, `404`, unchanged; no name or domain in row or log |
| AC-009 | all operations | no session → `302 /sign-in`; before setup → `302 /setup`; covered by enumeration test |
| AC-010 | both POSTs | no token → `400` error page, status unchanged; GETs change nothing |
| AC-011 | all pages | `uk` default; every key in `uk` and `en`; name and domain untranslated |

## 11. Compatibility

- Additive to US-001 … US-003. One change to an existing operation:
  `GET /installations/{id}` renders the status action and accepts the optional
  `notice` query parameter. No route, status code or host rule of earlier Stories
  changes; US-002 and US-003 assertions on that page remain valid.
- US-003 operations keep ignoring the installation status (US-003 AC-006).
- US-005 (legitimacy check) and US-006 (push) read or deliver the status; neither
  is part of this contract.

## 12. Open questions

None blocking. Notes for later stages:

- **Audit codes** `installation_suspended`, `installation_resumed` with target type
  `installation` follow the existing snake_case vocabulary of US-002;
  `db-designer` confirms them and whether any database constraint lists action
  codes.
- **Conditional update and `updated_at`** — the update of step 3 bypasses the
  change tracker if done as a set-based update; `db-designer` fixes how
  `updated_at` is set (spec FR-010).
