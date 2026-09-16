---
artifact_type: api_design
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-16T13:47:21Z
updated_at: 2026-09-16T13:47:21Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 69
  - path: docs/designs/api/US-001-openapi.yaml
    version: 1
supersedes: null
---

# US-002 API Design — Register an Installation

Contract: `docs/designs/api/US-002-openapi.yaml` (`info.version: "1"`).

## 1. Rationale

- **No `/api/v1` endpoint.** As in US-001, every screen is a server-rendered
  Razor page and every change an HTML form post; nothing calls these pages by
  script (the copy button uses the clipboard in the browser only). No response
  uses the API-6 JSON body: errors are the host's error page or the form shown
  again (AD-9, SC-4 v66).
- **Why a contract.** `test-writer` needs fixed routes, form field names, status
  codes, redirect targets and message keys before the code exists (TC-3, TC-5).
- **Not `NOT_APPLICABLE`.** The Specification defines pages, forms and status
  codes.
- **Builds on US-001.** The host-wide rules of `US-001-openapi.yaml`
  (`x-host-wide-rules`: setup gate, fallback authorization, global antiforgery,
  challenge to `/sign-in`, error page, culture, session lifetime) apply unchanged
  and are not repeated operation by operation.

## 2. Routes

| Method | Path | Anonymous | Policy | Purpose | Spec |
|---|---|---|---|---|---|
| GET | `/installations` | no | `Owner` | list | FR-001 |
| GET | `/installations/new` | no | `Owner` | registration form | FR-003 |
| POST | `/installations` | no | `Owner` | register | FR-003, FR-004, FR-008 |
| GET | `/installations/{id}` | no | `Owner` | detail page | FR-005 |
| GET | `/installations/{id}/name` | no | `Owner` | name form | FR-006 |
| POST | `/installations/{id}/name` | no | `Owner` | correct the name | FR-006 |
| GET | `/installations/{id}/client-id` | no | `Owner` | client ID form | FR-007 |
| POST | `/installations/{id}/client-id` | no | `Owner` | change the client ID | FR-007, FR-008 |
| GET | `/` (changed) | no | `Owner` | home page gains a link to `/installations` | FR-002 |
| GET | `/js/copy-identifier.js` | yes (SC-4 "Static files") | — | copy button script | FR-005, I-6 |

- `{id}` is the installation's UUID identifier (spec I-5), constrained to a GUID
  (`{id:guid}`). Links are emitted in lower-case hyphenated form
  (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`).
- Plural noun, kebab-case sub-resources, no verbs; `new` is the conventional
  form page of a collection (API-3 style kept for pages, as in US-001).
- No `PUT`/`PATCH`/`DELETE`: HTML forms post, and nothing is deleted
  (`trebovaniya.md` §3, v69). No route edits the domain or the status.

## 3. Host-wide behaviour

Unchanged from US-001 (`US-001-api-design.md` §3). Consequences for these routes:

- **Before setup** every route above answers `302 /setup`.
- **No session** → `302 /sign-in`; no return URL. The challenge runs before
  antiforgery, so a POST without a session is `302`, not `400`.
- **Non-UUID `{id}`** does not match any route and falls to the anonymous `404`
  catch-all: `404` with `Error.NotFound` for anyone. A UUID that matches no
  installation, requested by the Owner, is `404` with the same error page
  (`Error.NotFound`) — the two are indistinguishable to the Owner (FR-005).
  Before authorization, a well-formed UUID path without a session gives `302
  /sign-in`; a malformed one gives `404`. Neither reveals whether an
  installation exists.
- **Antiforgery refusal** → `400`, error page `Error.PageExpired`.

## 4. Operations

### GET /installations

- `200`: table with one row per installation — name, domain, status label,
  creation time, link to `/installations/{id}` — and a link to
  `/installations/new`. No query parameters; not paginated (I-2).
- Order: name case-insensitive (ordinal ignore-case), then domain (I-1).
- Status labels: `Installation.Status.Active`, `Installation.Status.Suspended`.
- Empty: `Installations.Empty` and the registration link.
- Creation time (OD-003): the UTC instant formatted as the Owner's culture short
  date pattern, a space, `HH:mm`, a space and `UTC`; in `uk`:
  `16.09.2026 13:29 UTC`. Never converted to a local time zone.

### GET /installations/new

- `200`: form with `name`, `domain`, `clientId` and the token; empty values. No
  status field.

### POST /installations

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page `Error.PageExpired` | — |
| 2 | binding: VR-001 `name`, VR-002 `domain`, VR-003 `clientId` — **all** failed fields reported at once | `400`, form again, messages per field, all three values refilled as typed | — |
| 3 | normalize `domain` to lower case (invariant) | — | — |
| 4 | domain or client ID already registered (any status) — both checked, both reported | `409`, form again, `Installation.Domain.Taken` and/or `Installation.ClientId.Taken`, values refilled | — |
| 5 | insert + audit row in one transaction | lost race on either unique constraint: `409` exactly as step 4 for the conflicting field; rollback | succeeded, `installation_created` |

Success: `302 Location: /installations/{id}` of the new installation.

In step 5, if the database reports a conflict, the service determines which
field(s) conflict (by the violated constraint, or by re-querying after rollback)
and returns step 4's result — never `500`.

### GET /installations/{id}

- `200`: identifier, name, domain, status label, creation time (as the list),
  client ID; the identifier in an element with `id="installation-identifier"`
  as selectable text, a copy button (`data-copy-target="installation-identifier"`)
  and the note `Installation.Identifier.ConfigurationNote`; links to
  `/installations/{id}/name` and `/installations/{id}/client-id`; a link back to
  `/installations`.
- No form or link that edits the domain, changes the status or deletes.
- `404`: unknown UUID.

### GET /installations/{id}/name

- `200`: form with `name` pre-filled with the stored name, and the token.
- `404`: unknown UUID.

### POST /installations/{id}/name

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page | — |
| 2 | installation exists | `404`, error page `Error.NotFound` | — |
| 3 | VR-001 `name` | `400`, form again, message, value refilled | — |
| 4 | ordinally equal to the stored name | `302 /installations/{id}`, nothing written | — |
| 5 | update name + audit row in one transaction | — | succeeded, `installation_renamed` |

Success: `302 Location: /installations/{id}`.

Step 2 precedes step 3: an unknown installation is `404` even with an invalid
name. The implementation checks existence before acting on `ModelState`.

### GET /installations/{id}/client-id

- `200`: form with `clientId` pre-filled with the stored value, the token, and the
  note `Installation.ClientId.ChangeNote` (recreated service account;
  super-admin must authorize delegation for the new client ID; picked up by the
  installation within 6 hours, at its next legitimacy check).
- `404`: unknown UUID.

### POST /installations/{id}/client-id

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page | — |
| 2 | installation exists | `404`, error page | — |
| 3 | VR-003 `clientId` | `400`, form again | — |
| 4 | equal to the stored client ID | `302 /installations/{id}`, nothing written | — |
| 5 | another installation has it | `409`, form again, `Installation.ClientId.Taken` | — |
| 6 | update + audit row in one transaction | lost race: `409` as step 5; rollback | succeeded, `installation_client_id_changed` |

Success: `302 Location: /installations/{id}`.

### GET / (home page, changed)

- Adds a link to `/installations` (`Home.Installations`). Otherwise as US-001.

### Static file `/js/copy-identifier.js`

- Served from the static files directory (anonymous per SC-4 "Static files",
  v68). Copies the text of the element named by `data-copy-target` with the
  Clipboard API and shows `Installation.Identifier.Copied` beside the button.
- No inline script and no inline event handler in any page of this Story. The
  script's texts come from the page (e.g. a `data-copied-text` attribute
  rendered from the translation), not hard-coded in the file (NFR-073).
- With scripts disabled the identifier stays selectable text.

## 5. Request models

Control Plane request types (AD-8): `RegisterInstallationRequest`
(`name`, `domain`, `clientId`), `RenameInstallationRequest` (`name`),
`ChangeInstallationClientIdRequest` (`clientId`). Nothing else is bound: a posted
`id`, `status`, `createdAt` or `identifier` field is ignored (over-posting).

| Field | Rules (spec) |
|---|---|
| `name` | required; 1–200 code points; first and last character not whitespace (`char.IsWhiteSpace` / `Rune.IsWhiteSpace`); no character of Unicode category `Cc` or `Cf` anywhere (VR-001, OD-002) |
| `domain` | required; 3–253 characters; `^[A-Za-z0-9.-]+$`; at least one `.`; labels split on `.`: none empty, each 1–63, none starting or ending with `-`, none starting with `xn--` case-insensitively (VR-002, OD-001) |
| `clientId` | required; `^[0-9]{10,32}$` with ASCII digits only (`[0-9]`, not `\d`, which matches other scripts' digits in .NET) (VR-003) |

Validation messages — one key per rule so tests assert the rule, not the wording:

| Key | Rule |
|---|---|
| `Installation.Name.Required` | empty |
| `Installation.Name.Length` | over 200 code points |
| `Installation.Name.EdgeWhitespace` | leading or trailing whitespace |
| `Installation.Name.InvalidCharacters` | `Cc` / `Cf` character |
| `Installation.Domain.Required` | empty |
| `Installation.Domain.Length` | under 3 or over 253 |
| `Installation.Domain.Characters` | character outside `A–Z a–z 0–9 . -` |
| `Installation.Domain.NoDot` | no dot |
| `Installation.Domain.Labels` | empty label, label over 63, hyphen at a label edge |
| `Installation.Domain.Idn` | `xn--` label |
| `Installation.Domain.Taken` | registered to another installation (FR-008 text incl. "resume, not register again") |
| `Installation.ClientId.Required` | empty |
| `Installation.ClientId.Format` | non-digit or length outside 10–32 |
| `Installation.ClientId.Taken` | registered to another installation |

Each field reports its first failing rule in the order of the table. A domain
failing `Characters` is not also checked for `Labels` / `Idn`.

## 6. Response models (view models)

Views receive DTOs only (AD-8), mapped in `ControlPlane.Services`:

- `InstallationListItemDto` — `Identifier` (Guid), `Name`, `Domain`, `Status`
  (enum: Active, Suspended), `CreatedAt` (UTC `DateTimeOffset`).
- `InstallationDetailDto` — the same plus `ClientId`.

Neither carries the internal numeric key, audit data or any secret. Values the
Owner entered are rendered with Razor's default HTML encoding; no `Html.Raw`.

## 7. Auth model

- Every operation of this Story: policy `Owner` (US-001), declared on the
  controller (API-9). None anonymous; no antiforgery exemption.
- The only anonymous addition is the static script, covered by the SC-4 "Static
  files" entry; the TC-5 enumeration test needs no new entry.
- Forbidden-role case (TC-5): the Control Plane has one role; the test for each
  operation is the unauthenticated request (`302 /sign-in`) plus, as in US-001,
  a principal lacking the `Owner` role (`403`, error page).

## 8. Error model

| Form | Used for |
|---|---|
| Error page | `400` antiforgery (`Error.PageExpired`), `403` (`Error.Forbidden`), `404` (`Error.NotFound`), `500` (`Error.Internal`) |
| Form shown again | `400` validation, `409` conflict — field-level messages, typed values refilled and HTML-encoded |

No message, page or log line names the other installation holding a conflicting
value (I-8), and none repeats a rejected value outside the refilled input.

## 9. Acceptance Criterion → operation map

| AC | Operations | Key assertions |
|---|---|---|
| AC-001 | GET `/installations`, GET `/` | rows with name, domain, status label, `16.09.2026 13:29 UTC` shape in `uk`; order; empty message; link from home |
| AC-002 | GET `/installations/new`, POST `/installations` | `302` to `/installations/{uuid}`; one row; domain lower case; status active; no status field; posted `status`/`identifier` ignored |
| AC-003 | POST `/installations`, POST `…/name`, POST `…/client-id` | `400` per rule with its key; boundaries (1/200 name, 3/253 domain, 63/64 label, 10/32 client ID); mixed-case domain accepted and lower-cased; values refilled |
| AC-004 | POST `/installations` (×2 concurrently), POST `…/client-id` | `409` for duplicate domain in other case, duplicate client ID, suspended holder; both fields flagged; concurrent: one row, other `409` not `500` |
| AC-005 | GET `/installations/{id}` | all six values; copy button + selectable identifier + note; no domain/status/delete control; unknown UUID `404`; non-UUID `404` |
| AC-006 | GET/POST `…/name` | only name changes; duplicate name accepted; unchanged → `302`, no write, no audit row |
| AC-007 | GET/POST `…/client-id` | only client ID changes; change note shown; unchanged → `302`, no audit row |
| AC-008 | POST `/installations`, `…/name`, `…/client-id` | one audit row per success with Owner actor, `installation` target, internal id; none on `400`/`404`/`409`/unchanged; no name/domain/client ID in the row |
| AC-009 | all operations | no session → `302 /sign-in`; before setup → `302 /setup`; not in anonymous enumeration |
| AC-010 | all POSTs | no token → `400` error page, nothing changed; no GET changes state |
| AC-011 | all pages | `uk` default; every key exists in `uk` and `en`; entered values shown untranslated |

## 10. Compatibility

- Additive to the US-001 contract. One change to an existing operation: `GET /`
  renders an extra link. No route, status code, cookie or host rule of US-001
  changes.
- Later Stories extend `/installations/{id}` without changing these operations:
  US-003 adds admin entries (e.g. `/installations/{id}/admins`), US-004 adds
  suspend/resume posts. Their names are theirs to design.
- The UUID in `{id}` is the same value later carried in the service channel
  (US-005); it is stable from this Story on.

## 11. Open questions

None blocking. Notes for later stages:

- **Audit action codes.** `installation_created`, `installation_renamed`,
  `installation_client_id_changed` and target type `installation` follow the
  snake_case codes of US-001's audit vocabulary; `db-designer` confirms them in
  the entity model.
- **Which internal id is the audit target** (the numeric key or the UUID) is
  `db-designer`'s (spec FR-004); the contract does not expose the numeric key.
- **Lost-race detection** — mapping the violated unique constraint to the
  conflicting field needs constraint names fixed by `db-designer`.
