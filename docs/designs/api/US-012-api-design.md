---
artifact_type: api_design
story: US-012
version: 1
status: DRAFT
created_at: 2026-09-26T17:26:10Z
updated_at: 2026-09-26T17:26:10Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/decisions/US-012-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-012 API Design — Create and manage Dean accounts

Companion to `docs/designs/api/US-012-openapi.yaml`. The contract is the
authority on shapes and status codes; this document records why they are what
they are.

## 1. Scope of the contract

Ten server-rendered operations on the installation's public port, in three
groups:

| Group | Operations | Who |
|---|---|---|
| `/settings/deans` | `GET`, `POST`; `POST /{deanId}/state`; `POST /{deanId}/password` | Admin |
| `/sign-in` | `GET` (extended), `POST`; `GET` and `POST /sign-in/change-password` | anonymous, then the account itself |
| `/account/password` | `GET`, `POST` | Dean |

No `/api/v1` path, no Control Plane endpoint, no `ClassroomAgent.Contracts`
type; `ContractVersion` stays 1. Nothing here calls Google (spec S-14).

The Story's sixteenth Acceptance Criterion group — AC-014, the Dean's `403` on
the three settings pages — adds **no** operation: it asserts the existing
contracts of US-009, US-010 and US-011 with a session that only now can exist.

## 2. Decisions this stage made

### 2.1 Four actions, three paths, no verbs

BR-014 gives the Admin four actions. `api-conventions.md` API-3 forbids verbs in
paths, so the contract expresses them as resources:

- **create** → `POST /settings/deans` (API-4's create shape);
- **disable** and **re-enable** → `POST /settings/deans/{deanId}/state` with a
  `desiredState` field. They are one decision — the account is active or it is
  not — and one path keeps the pair symmetric; two paths (`/disable`,
  `/enable`) would be verbs in the URL;
- **reset password** → `POST /settings/deans/{deanId}/password`.

A `DELETE` exists nowhere. That is not an omission but the rule: BR-014 forbids
deleting an account and PC-11 owns the only deletion there is (spec FR-010).

### 2.2 Post-Redirect-Get everywhere a write succeeds

US-011 deliberately had none, because its result was not stored and a reload was
allowed to re-run the check. Here every successful write is followed by
`302` back to the screen with the confirmation in TempData, as US-009 does: a
reload must never re-create an account, re-disable one or reset a password
again. A failed validation re-renders instead, so the Admin can correct the
typed email without losing it.

### 2.3 The sign-in submission answers `302` for every outcome

Spec S-05 requires steps 1, 2 and 3 of FR-012 to be indistinguishable. The
simplest way to keep them so is to give the whole sequence one response shape:
`302`, with the outcome in the `Location` and in TempData. Steps 1, 2 and 3 share
both — the same redirect back to `/sign-in` and the same message key. Step 4
differs only in the message key, which SC-2 explicitly allows with the correct
password and no lockout. Steps 5 and 6 differ in the `Location`, which by then
is not a refusal at all.

A refused sign-in is therefore **not** `401`: no API-5 status is used, because
this is a Razor page, and a status that distinguished refusals would be the
oracle SC-2 forbids.

### 2.4 The Dean form lives on the existing `/sign-in` page

SC-4 names "Dean sign-in page" as an entry in the closed anonymous list. US-008
already serves `/sign-in` anonymously. Adding a second anonymous page would
widen that list for no gain, so the Dean's email-and-password form joins the
page that is already there and the list grows by one **method**, `POST /sign-in`,
not by a page. The `GET` is unchanged for existing callers (additive only).

### 2.5 The forced change is its own path, not a mode of `/account/password`

They look alike and are not: the forced change asks for no current password —
it was supplied seconds earlier — and it is reachable only by a session that has
passed step 5 and may reach nothing else; `/account/password` requires the
current password and is refused to an Admin. Merging them would put "sometimes
require the current password" inside one handler, which is exactly the kind of
conditional a password path should not have. The two share a view model with a
`mode` discriminator, so the translation keys and the layout stay in one place.

### 2.6 Step 5 opens a restricted session

Spec FR-006 says the forced change form is the only page such a Dean may reach.
The contract expresses that as a session created at step 5 whose every other
authenticated request redirects to `/sign-in/change-password`. The alternative —
carrying the half-authenticated state in TempData or a token — would invent a
second authentication mechanism; SC-2 already fixes one cookie.

`GET /sign-in/change-password` therefore has two `302` targets: `/sign-in` when
there is no session at all, and `/` when the session's password is not temporary
— so the page can never be used to change a password without having just proved
the old one.

### 2.7 `404`, not `403`, for an account that is not a Dean

Spec VR-005 and I-8 say a management action on an Admin account, or on an id
that matches nothing, is refused without disclosing which. `403` would announce
"this id exists but is not yours to manage"; `404` says only that there is no
such Dean. API-5 explicitly allows `404` for a resource "not visible to the
caller".

### 2.8 Two kinds of `409`, one status

As in US-011: read-only mode (BR-025, API-5) renders the host-wide translated
error page, and the state conflict of `POST /{deanId}/state` — already disabled,
already active, from a stale screen — re-renders the list with a message.
Read-only is evaluated **first** in every management operation (spec FR-003 step
1, FR-015), so a school that is both read-only and looking at a stale screen is
told about read-only.

### 2.9 Policy names proposed

`ManageDeanAccounts` (Admin), `ChangeOwnPassword` (Dean),
`CompleteTemporaryPasswordChange` (a session whose password is temporary). They
follow the existing `ConfigureWorkspaceConnection` / `ViewConnectionInstruction`
/ `RunAccessCheck` naming. DB_DESIGN confirms nothing here; the names are
IMPLEMENTATION's to register beside the three existing ones.

### 2.10 No pagination, recorded as a deviation

API-8 would paginate a collection that can grow unbounded. The Dean list is
bounded by the school's staff, and OD-003 was resolved by the Owner as "no
search and no paging in v1". The contract records this under
`x-pagination-deviation` so a reviewer meets a decision, not an oversight.

### 2.11 What no schema may carry

No password, no hash, no security stamp, no failed-attempt counter and no
lockout end appears in any response model — the two password forms carry
passwords **in** and nothing out. The typed email is preserved on a refusal so it
can be corrected; the typed password never is (spec VR-006, S-10).

## 3. Operation notes

### `GET /settings/deans`
Serves the list and the creation form. Works in read-only mode with the reason
named and no control hidden (AD-6). Writes nothing.

### `POST /settings/deans`
Evaluation order is the Specification's: read-only, address, domain, password
policy, uniqueness. `400` re-renders with the email preserved; `409` is
read-only only. One `AppUser` row and one audit row in one transaction, nothing
else staged (carried US-009 F-2).

### `POST /settings/deans/{deanId}/state`
`desiredState` outside the enum is `400`. Disabling rotates the security stamp;
re-enabling changes nothing else. `404` covers both "no such id" and "not a
Dean".

### `POST /settings/deans/{deanId}/password`
Accepted for a disabled or locked-out account — that is the point of a reset. It
clears the lockout, sets the temporary mark, rotates the stamp, and never
re-enables.

### `GET /sign-in` (extended) and `POST /sign-in`
The `GET` gains the Dean form and new refusal keys; the `POST` is the whole of
FR-012. The only non-`302` answer is `400` for a missing antiforgery token or a
missing field — before any account lookup, so a malformed request cannot move a
counter.

### `GET`/`POST /sign-in/change-password`
The forced change. `400` on a policy failure or on a new password equal to the
temporary one, which is detected by verifying the new password against the hash
still stored (spec VR-003).

### `GET`/`POST /account/password`
The Dean's own change; `403` for an Admin. A wrong current password is `400` and
is **not** a failed sign-in attempt: FR-013's counter belongs to sign-in.

## 4. Authentication and authorization model

| Operation | Anonymous | Policy | Role |
|---|---|---|---|
| `GET /settings/deans` | no | `ManageDeanAccounts` | Admin |
| `POST /settings/deans` | no | `ManageDeanAccounts` | Admin |
| `POST /settings/deans/{id}/state` | no | `ManageDeanAccounts` | Admin |
| `POST /settings/deans/{id}/password` | no | `ManageDeanAccounts` | Admin |
| `GET /sign-in` | **yes** (US-008) | — | — |
| `POST /sign-in` | **yes** (new on the SC-4 list) | — | — |
| `GET /sign-in/change-password` | no | `CompleteTemporaryPasswordChange` | the account itself |
| `POST /sign-in/change-password` | no | `CompleteTemporaryPasswordChange` | the account itself |
| `GET /account/password` | no | `ChangeOwnPassword` | Dean |
| `POST /account/password` | no | `ChangeOwnPassword` | Dean |

Every `POST` carries the antiforgery token, the anonymous one included (API-7,
SC-4 login-CSRF). The deny-by-default fallback policy of US-008 still catches
anything that declares nothing (API-9).

## 5. Error model

No operation is under `/api/v1`, so none uses the API-6 JSON body (API-6 first
line). Refusals are the host's translated error page (`403`, read-only `409`,
antiforgery `400` on a page that cannot re-render) or the re-rendered form with
per-field translation keys. Nothing rejected is echoed back, logged or audited
(spec VR-006, SC-10).

`500` is reachable only through the host's existing handler and, as everywhere,
leaks nothing.

## 6. Acceptance Criterion → operation map

| AC | Operation(s) |
|---|---|
| AC-001 | `GET /settings/deans` (200 / 302 / 403) |
| AC-002 | `POST /settings/deans` (302) |
| AC-003 | `POST /settings/deans` (400) |
| AC-004 | `POST /settings/deans`, `POST /{id}/password`, both change operations (400) |
| AC-005 | `POST /{id}/state` with `Disabled` (302) |
| AC-006 | `POST /{id}/state` with `Active` (302) |
| AC-007 | `POST /{id}/password` (302) |
| AC-008 | no operation — the absence is the criterion (`x-operations-deliberately-absent`) |
| AC-009 | all four management operations (409) |
| AC-010 | `POST /sign-in` (302, four outcomes) |
| AC-011 | `POST /sign-in` (302, step 2) |
| AC-012 | `POST /sign-in` (302 → `/sign-in/change-password`), then `POST /sign-in/change-password` |
| AC-013 | `GET`/`POST /account/password` |
| AC-014 | **no new operation** — the existing US-009, US-010 and US-011 operations, asserted with a real Dean session |
| AC-015 | every operation: every key in both languages |
| AC-016 | every write and every refusal above |

## 7. Compatibility

- `GET /sign-in` changes additively: a new form on the page and new refusal
  keys. No existing parameter, status code or redirect changes.
- `POST /sign-in` is a new method on an existing path. It does not affect
  `GET /sign-in/google`, `/signin-google` or `/sign-out`.
- The SC-4 anonymous list grows by exactly one entry; the US-008 enumeration test
  gains one row.
- No existing operation of US-009, US-010 or US-011 changes shape. They gain a
  caller who can now be refused for real.
- `ContractVersion` stays 1 (DC-12).
- Carried US-008 F-5 (a wrong method on `/sign-in/google` or `/sign-out` answers
  `404` where the openapi documents `405`) is **not** addressed here and is not
  made worse: the new routes document only the methods they implement.

## 8. Open questions for later stages

- **DB_DESIGN** decides the field that marks a password temporary on `AppUser`
  (spec FR-002), the new `AuditAction` and `AuditTargetType` members, the
  `AuditRefusalCategory` members the sign-in sequence needs beyond the existing
  `AccountDisabled` and `ReadOnlyMode`, and whether the audit check constraints
  need an amending migration as US-009 and US-011 each did (PC-2).
- **TEST_WRITING** needs stable markup for the list rows and the state controls,
  in the shape US-011 used (`id`/`data-` attributes), and must assert that steps
  1, 2 and 3 of `POST /sign-in` are identical in status, `Location` and message
  key.
- **IMPLEMENTATION** registers the three new policies beside the existing three
  and adds `POST /sign-in` to the US-008 endpoint enumeration test's anonymous
  list.
