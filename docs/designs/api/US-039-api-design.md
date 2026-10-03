---
artifact_type: api_design
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T08:51:23Z
updated_at: 2026-10-03T08:51:23Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/decisions/US-039-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-039 API Design — Choose UI language (Admin, Dean and Owner)

Contract: `docs/designs/api/US-039-openapi.yaml`.

## 1. Scope of the contract

One operation, `POST /account/language`, present on **both hosts** with the same
path, form and responses: the installation (Admin, Dean) and the Control Plane
(Owner) — OD-004. Plus one UI element, the header switcher, which posts to it.
Two existing installation views change only their date format (spec FR-009); no
existing operation's contract changes.

Not added: any GET, any `/api/v1` path, any anonymous endpoint, any antiforgery
exemption, any query or cookie culture source.

## 2. Decisions this stage made

### 2.1 Path `/account/language`

The installation already groups "what a signed-in user does to their own account"
under `/account` (`/account/password`, US-012). The language is the same kind of
thing, so it sits beside it. The Control Plane has no `/account` yet; using the
same path on both hosts keeps the switcher partial, the tests and the
documentation identical. The path names no account: the target is the session's
(AC-004).

### 2.2 `302` Post-Redirect-Get to the validated return path

Every successful form on both hosts answers `302` (US-009 … US-012). The
redirect target is the submitted `returnPath` if it passes VR-002, else `/`. The
redirect is what makes "the same page shown again in the chosen language"
(AC-001): the next request carries the re-issued cookie, so the culture provider
renders the new language.

### 2.3 Re-issue by copying the principal

The host builds the new principal from the current one, replacing only the
language claim. It does **not** call the existing `InstallationSession.SignInAsync`
/ `OwnerSession.SignInAsync` as they stand: both stamp a **new** sign-in time,
which would let a user extend the 8-hour absolute limit by switching language
(spec I-2, AC-009). Either a new helper in each host's `Security` namespace or an
overload that accepts the original sign-in time is acceptable; the observable
contract is the claim set and the cookie attributes in the `302` description.

### 2.4 One `400` for both refusals

A missing/invalid antiforgery token and an invalid `language` value both answer
`400` with the error page and the `Error.PageExpired` text. The legitimate
switcher never sends an invalid code, so the only way to get one is a tampered
or stale form, for which "reload and try again" is the right text; a separate
error text would be a new string for a case no real user reaches (spec I-6).
No new translation key is needed for refusals.

### 2.5 Bound as strings

`language` and `returnPath` are bound as strings, not as the `UiLanguage` enum:
MVC's enum binding accepts numbers and is case-insensitive, which would turn
`0`, `1` or `EN` into valid choices (VR-001). Validation lives in `Application`
(installation: a request model under `Application/Models/Requests`; Control
Plane: its `Services` layer), not in the controller.

### 2.6 GET answers `404` or `405`

There is no GET operation. Which of the two the host answers depends on
endpoint routing and the anonymous catch-all; earlier contracts recorded both
(US-001: `404`; US-008: `405`). The requirement is that nothing changes, so the
test asserts "one of `404`/`405`, nothing stored, session unchanged".

### 2.7 The temporary-password gate grows by one path

OD-007 resolved option 1: the installation's `TemporaryPasswordMiddleware`
admits `/account/language`. Admitting it is safe because the re-issued session
keeps the temporary-password claim, so the very next request — the redirect — is
again held at `/sign-in/change-password`, whatever the return path says.

### 2.8 Policies: existing ones only

Installation: `InstallationPolicies.AuthenticatedUser` (any signed-in Admin or
Dean). Control Plane: `OwnerSession.OwnerPolicy`. No new policy: the permission
matrix of §2 has no row for this, and §5 gives the choice to every Admin and Dean
and to the Owner.

## 3. Operation notes

### `POST /account/language` — installation

- Use case in `Application` loads the session's `AppUser`, sets `UiLanguage`,
  commits under `PermittedServiceWrite.SignInBookkeeping`, and is registered in
  `PermittedServiceWrites.Declarations` (spec FR-007). It returns a result value
  or DTO, not the entity (AD-8).
- Never `409`: read-only mode stores the choice (BR-026, AC-003).
- No audit row (OD-005). No log line is required; if one is written, it carries
  the account id and the accepted code only (FR-011).

### `POST /account/language` — Control Plane

- A `Services` method updates `Owner.UiLanguage`; the controller never touches
  `DbContext` (AD-3).
- No read-only mode on this host.

### Header switcher

- A shared partial rendered by each host's `_Layout` only when the endpoint
  requires sign-in and the user is authenticated. Pages on SC-4's anonymous list
  (sign-in, first-run setup, error page) render none — the layout must decide by
  the endpoint, not only by `User.Identity`, because a signed-in user also sees
  the anonymous error page (spec I-5).
- `returnPath` is the current request's `Path + QueryString`.

## 4. Authentication and authorization model

| Host | Session | Policy | Anonymous | Antiforgery |
|---|---|---|---|---|
| installation | `__Host-ca-session` (Lax) | `AuthenticatedUser` | no — `302 /sign-in` | required |
| Control Plane | `__Host-cp-session` (Strict) | `Owner` | no — `302 /sign-in` | required |

The account changed is always the one in the session claim; a request field
naming an account is ignored. Disabled or signed-out accounts never reach the
action: the per-request stamp check rejects the session first.

## 5. Error model

| Case | Status | Body | Stored |
|---|---|---|---|
| chosen (any mode) | `302` → return path or `/` | — | yes |
| invalid return path | `302` → `/` | — | yes |
| anonymous / rejected session | `302` → `/sign-in` | — | no |
| missing/invalid antiforgery | `400` | error page, `Error.PageExpired` | no |
| invalid `language` | `400` | error page, `Error.PageExpired` | no |
| GET or other method | `404` or `405` | error page | no |
| database failure | `500` | error page, `Error.Unexpected` | no |

No operation is under `/api/v1`; the API-6 JSON body is not used.

## 6. Acceptance Criterion → operation map

| AC | Operation / element | What the test asserts |
|---|---|---|
| AC-001 | `POST /account/language` + switcher | `302` to the return path; the next GET renders in the new language; the switcher marks the current language; Admin, Dean, Owner |
| AC-002 | `POST /account/language`; sign-in operations of US-001, US-008, US-012 | stored on own row only; a new sign-in issues the session with the stored language; another user unchanged |
| AC-003 | installation use case | in each BR-025 read-only cause the choice is stored; the use case is the only registry addition; Application-layer test |
| AC-004 | `POST /account/language` | anonymous → `302 /sign-in`, nothing stored; no token → `400`; GET → `404`/`405`, nothing stored; an `accountId` field is ignored; both hosts |
| AC-005 | `POST /account/language` | empty, `UK`, `de`, `0`, `english`, oversized → `400`, nothing stored, value absent from log output |
| AC-006 | switcher absence | sign-in, setup and error pages render no switcher and the default language |
| AC-007 | `GET /`, `GET /settings/deans`, Control Plane installation views | dates in each language's short date format |
| AC-008 | switcher | labels from both translation files on each host |
| AC-009 | re-issued cookie | sign-in time claim unchanged; the session still expires 8 h after the original sign-in; sign-out still invalidates it |
| AC-010 | `POST /account/language` | `returnPath` of `//evil`, `https://evil`, `/\evil` → `302 /` |
| OD-007 | temporary-password session | `POST /account/language` stored; redirect lands on `/sign-in/change-password`; other paths still redirected there |

## 7. Compatibility

- No existing operation's request or response changes.
- `TemporaryPasswordMiddleware`'s allowed list gains one path (US-012
  api-design §2.6 is extended, not contradicted: still "the form and nothing
  that opens another page").
- Two views change their date format (FR-009); their contracts do not.
- `ContractVersion` stays 1.

## 8. Open questions for later stages

None for the API. Notes for DB_DESIGN: no schema change is expected (OD-003);
both `UiLanguage` columns exist with `uk` / `en` values.
