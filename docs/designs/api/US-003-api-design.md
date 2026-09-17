---
artifact_type: api_design
story: US-003
version: 1
status: DRAFT
created_at: 2026-09-17T09:33:00Z
updated_at: 2026-09-17T09:33:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-003-spec.md
    version: 1
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 70
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
supersedes: null
---

# US-003 API Design — Manage AllowedAdmin entries

Contract: `docs/designs/api/US-003-openapi.yaml` (`info.version: "1"`).

## 1. Rationale

- **No `/api/v1` endpoint.** As in US-001 and US-002, every screen is a
  server-rendered Razor page and every change an HTML form post. No script is
  involved (the revoke confirmation is a page, spec I-2). No response uses the
  API-6 JSON body: errors are the host's error page or the form shown again
  (AD-9, SC-4 v66).
- **Why a contract.** `test-writer` needs fixed routes, form field names, status
  codes, redirect targets and message keys before the code exists (TC-3, TC-5).
- **Not `NOT_APPLICABLE`.** The Specification defines pages, forms and status
  codes.
- **Builds on US-001 and US-002.** The host-wide rules of `US-001-openapi.yaml`
  and the `{id}` route rule, datetime display and output encoding of
  `US-002-openapi.yaml` apply unchanged.

## 2. Routes

| Method | Path | Anonymous | Policy | Purpose | Spec |
|---|---|---|---|---|---|
| GET | `/installations/{id}` (changed) | no | `Owner` | detail page gains the Admins section | FR-001, FR-006 |
| GET | `/installations/{id}/admins/new` | no | `Owner` | add form | FR-002 |
| POST | `/installations/{id}/admins` | no | `Owner` | add an entry | FR-003 |
| GET | `/installations/{id}/admins/{adminId}/revocation` | no | `Owner` | revoke confirmation | FR-004 |
| POST | `/installations/{id}/admins/{adminId}/revocation` | no | `Owner` | revoke the entry | FR-005 |

- `{id}` is the installation's UUID identifier, as in US-002.
- `{adminId}` is the entry's **UUID identifier**, constrained to a GUID. Entries are
  addressed by a UUID, never the internal numeric key, for the same reason as
  installations (US-002 I-5): no sequential key in URLs. `db-designer` therefore
  gives `AllowedAdmin` a unique UUID identifier generated server-side.
- Sub-resource `admins` under its installation (spec FR-008); `revocation` is a
  noun sub-resource of the entry (API-3: no verbs), whose GET is the confirmation
  and whose POST performs it. `new` is the conventional form page, as in US-002.
- No `PUT`/`PATCH`/`DELETE`: HTML forms post; there is no editing
  (`trebovaniya.md` §3, v70).

## 3. Host-wide behaviour

Unchanged from US-001 (`US-001-api-design.md` §3) and US-002 §3. Consequences:

- **Before setup** every route answers `302 /setup`.
- **No session** → `302 /sign-in`, before antiforgery.
- **Non-GUID `{id}` or `{adminId}`** matches no route → anonymous `404` catch-all,
  `Error.NotFound`.
- **Well-formed GUIDs that identify nothing** — no installation with `{id}`, no
  entry with `{adminId}`, or an entry that belongs to another installation — `404`
  with the same error page `Error.NotFound`. The three cases are indistinguishable
  (FR-008).
- **Antiforgery refusal** → `400`, error page `Error.PageExpired`.

## 4. Operations

### GET /installations/{id} (changed)

Everything of US-002 stays. Added, after the existing values, an **Admins**
section:

- heading `AllowedAdmins.Title`;
- when fewer than two entries: warning `AllowedAdmins.FewerThanTwoWarning`, in an
  element with `id="allowed-admins-warning"` (FR-006); absent with two or more;
- when no entries: `AllowedAdmins.Empty`;
- otherwise a table, one row per entry ordered by email ordinal (I-4): email, date
  added (US-002 datetime-display, e.g. `17.09.2026 09:33 UTC`), and a link
  `AllowedAdmin.Revoke` to `/installations/{id}/admins/{adminId}/revocation`;
- a link `AllowedAdmins.Add` to `/installations/{id}/admins/new`, always.

Only entries of this installation are rendered.

### GET /installations/{id}/admins/new

- `200`: installation name and domain (for orientation), field `email` (empty),
  hint `AllowedAdmin.Email.DomainHint` with the installation's domain as argument,
  the antiforgery token, and a link back to `/installations/{id}`.
- `404`: unknown installation.

### POST /installations/{id}/admins

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page `Error.PageExpired` | — |
| 2 | installation exists | `404`, error page `Error.NotFound` | — |
| 3 | VR-001 format of `email`, first failing rule (§5) | `400`, form again with the key, value refilled | — |
| 4 | lower-case the email (invariant) | — | — |
| 5 | domain part ≠ installation domain (VR-002) | `400`, form again with `AllowedAdmin.Email.WrongDomain` (argument: the installation's domain), value refilled | — |
| 6 | entry with this email exists in this installation | `409`, form again with `AllowedAdmin.Email.Taken`, value refilled | — |
| 7 | insert + audit row in one transaction | lost race on the (installation, email) unique constraint: `409` as step 6; rollback | succeeded, `allowed_admin_added` |

Success: `302 Location: /installations/{id}`.

Step 2 precedes step 3: an unknown installation is `404` even with an invalid
email. The implementation checks existence before acting on `ModelState`. Any
installation status is accepted (AC-006).

### GET /installations/{id}/admins/{adminId}/revocation

- `200`: the entry's email, the installation's name, explanation
  `AllowedAdmin.Revoke.Explanation` (only the right to sign in and configure is
  removed; synchronization and Deans keep working; stopping a school is a
  separate action), and — when the installation currently has two entries or
  fewer — note `AllowedAdmin.Revoke.FewerThanTwoNote` in an element with
  `id="revoke-fewer-than-two-note"` (I-10). A form posting to the same path with
  the antiforgery token and button `AllowedAdmin.Revoke.Confirm`; a link
  `AllowedAdmin.Revoke.Cancel` to `/installations/{id}`.
- Changes nothing, audits nothing.
- `404`: unknown installation, unknown entry, or entry of another installation.

### POST /installations/{id}/admins/{adminId}/revocation

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery | `400`, error page | — |
| 2 | installation exists and the entry exists and belongs to it | `404`, error page | — |
| 3 | delete the entry + audit row in one transaction; the delete is scoped to (installation, entry) and must affect exactly one row | zero rows affected (already revoked by a repeat or concurrent request): rollback, `404`, error page | succeeded, `allowed_admin_revoked` |

Success: `302 Location: /installations/{id}`.

The form carries no field: nothing but the token is posted; any posted field is
ignored.

## 5. Request model

`AddAllowedAdminRequest` (`email`) — the only bound field; a posted
`installationId`, `addedBy`, `addedAt` or `identifier` is ignored (over-posting).
The revocation POST binds no model.

| Field | Rules (spec VR-001) |
|---|---|
| `email` | required; 1–254 characters; exactly one `@`; name part 1–64 of `[A-Za-z0-9._'-]` (ASCII only), not starting or ending with `.`, no `..`; domain part non-empty. Not trimmed. |

Validation message keys — one per rule, reported in this order, first failing
rule only:

| Key | Rule | Where |
|---|---|---|
| `AllowedAdmin.Email.Required` | empty | binding |
| `AllowedAdmin.Email.Length` | over 254 characters | binding |
| `AllowedAdmin.Email.Format` | not exactly one `@`, or empty name part, or empty domain part | binding |
| `AllowedAdmin.Email.NameLength` | name part over 64 characters | binding |
| `AllowedAdmin.Email.NameCharacters` | name part character outside `A–Z a–z 0–9 . _ - '` (leading/trailing space included, I-5) | binding |
| `AllowedAdmin.Email.NameDots` | name part starts or ends with `.`, or contains `..` | binding |
| `AllowedAdmin.Email.WrongDomain` | lower-cased domain part ≠ installation domain (VR-002); names the expected domain | service |
| `AllowedAdmin.Email.Taken` | already an entry of this installation | service |

A space inside the domain part (e.g. trailing space) reaches `WrongDomain`,
because the installation domain never contains one; a leading space is in the
name part and fails `NameCharacters`.

Pattern for the binding rules (informative; the per-key checks above are
normative):
`^(?=.{1,254}$)(?!\.)(?!.*\.\.)[A-Za-z0-9._'-]{1,64}(?<!\.)@[^@]+$`

## 6. Response models (view models)

DTOs only (AD-8), mapped in `ControlPlane.Services`:

- `InstallationDetailDto` (US-002) gains `Admins` — a list of
  `AllowedAdminItemDto` in display order. Warning display is `Admins.Count < 2`,
  computed in the view model or service, not stored.
- `AllowedAdminItemDto` — `Identifier` (Guid), `Email`, `AddedAt` (UTC
  `DateTimeOffset`). No added-by id, no internal key.
- `AddAllowedAdminFormDto` — installation `Identifier`, `Name`, `Domain`.
- `RevokeAllowedAdminConfirmationDto` — installation `Identifier` and `Name`,
  entry `Identifier` and `Email`, `LeavesFewerThanTwo` (bool: current entry count
  ≤ 2).

Emails, names and domains are rendered with Razor's default HTML encoding (an
apostrophe in an email is encoded); no `Html.Raw`.

## 7. Auth model

- Every operation: policy `Owner` (US-001), declared on the controller (API-9).
  None anonymous; no antiforgery exemption; no new static file.
- TC-5 forbidden-role case: unauthenticated request (`302 /sign-in`) plus a
  principal lacking the `Owner` role (`403`, error page), as in US-002.

## 8. Error model

| Form | Used for |
|---|---|
| Error page | `400` antiforgery (`Error.PageExpired`), `403` (`Error.Forbidden`), `404` (`Error.NotFound`), `500` (`Error.Internal`) |
| Form shown again | `400` validation or wrong domain, `409` duplicate — message under the `email` field, typed value refilled and HTML-encoded |

No message, page or log line repeats a rejected email outside the refilled input
(SC-10).

## 9. Acceptance Criterion → operation map

| AC | Operations | Key assertions |
|---|---|---|
| AC-001 | GET `/installations/{id}` | only this installation's entries; email + `dd.MM.yyyy HH:mm UTC` in `uk`; ordered by email; `AllowedAdmins.Empty`; add link; revoke link per entry |
| AC-002 | GET `…/admins/new`, POST `…/admins` | `302 /installations/{id}`; one row; email lower case; added-by = Owner id; added-at UTC; posted `addedBy`/`addedAt` ignored; many entries accepted |
| AC-003 | POST `…/admins` | `400` per key; boundaries 254/255 total, 64/65 name part; mixed case accepted and lower-cased; personal, other-school, sub- and parent domain → `WrongDomain`; value refilled; nothing created |
| AC-004 | POST `…/admins` (repeat, ×2 concurrently) | same email in other case → `409` `Taken`; concurrent: one row, other `409` not `500` |
| AC-005 | GET/POST `…/revocation` | confirmation shows email + installation name + explanation; GET deletes nothing; cancel link to detail; POST deletes row; last entry revocable; re-add creates a new entry with new identifier and timestamps |
| AC-006 | POST `…/admins`, POST `…/revocation` on a suspended installation | same results; installation row unchanged |
| AC-007 | GET `/installations/{id}` | warning element present with 0 and 1 entries, absent with 2; add/revoke still offered |
| AC-008 | all operations with `{id}`/`{adminId}` | unknown or non-GUID installation, unknown entry, entry of another installation → `404`; second revocation POST → `404`, one audit row total |
| AC-009 | POST `…/admins`, POST `…/revocation` | one row per success: Owner actor, target `allowed_admin` + entry internal id, `allowed_admin_added` / `allowed_admin_revoked`; none on `400`/`404`/`409`/GET; no email in row or log |
| AC-010 | all operations | no session → `302 /sign-in`; before setup → `302 /setup`; covered by enumeration test |
| AC-011 | both POSTs | no token → `400` error page, nothing changed; GETs change nothing |
| AC-012 | all pages | `uk` default; every key in `uk` and `en`; emails, names, domains untranslated |

## 10. Compatibility

- Additive to US-001 and US-002. One change to an existing operation:
  `GET /installations/{id}` renders an extra section, and `InstallationDetailDto`
  gains `Admins`. No route, status code or host rule of earlier Stories changes;
  US-002 assertions on that page remain valid.
- US-004 adds suspend/resume to the same detail page; nothing here constrains it.
- The Admin login check of US-008 is a service-channel operation, not part of this
  contract.

## 11. Open questions

None blocking. Notes for later stages:

- **Entry identifier** — this contract requires a server-generated unique UUID per
  `AllowedAdmin` (§2); `db-designer` adds it beside whatever key PC-3 requires.
- **Audit codes** `allowed_admin_added`, `allowed_admin_revoked` and target type
  `allowed_admin` follow the existing snake_case vocabulary; `db-designer` confirms
  them and which internal id the target carries.
- **Lost-race detection** needs the (installation, email) unique constraint name
  fixed by `db-designer`.
