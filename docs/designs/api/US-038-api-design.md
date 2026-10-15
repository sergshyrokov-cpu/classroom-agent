---
artifact_type: api_design
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T05:50:00Z
updated_at: 2026-10-15T05:50:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-038-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 93
  - path: docs/designs/api/US-008-openapi.yaml
    version: null
  - path: docs/designs/api/US-012-openapi.yaml
    version: null
supersedes: null
---

# US-038 API Design — Erase all data of one participant on request

Contract: `docs/designs/api/US-038-openapi.yaml` (`info.version` "1").

Delegated lookups (read only, `quick-look`): `artifact-schema.md` (api_design and
OpenAPI keys), `api-conventions.md` API-1 … API-10, the US-024 and US-012 API
designs and contracts (format, a form-`POST` operation), `SignInRoutes`,
`InstallationPolicies`, `DeanAccountsController`. YAML parse checked locally.

## 1. Scope of the contract

Five server-rendered operations under one Admin-only area, plus an additive
link on the home page. No `/api/v1` path: the UI is Razor (§8, API-1 intro) and
the Specification adds no JSON API (§10).

| Operation | Method / path | Writes |
|---|---|---|
| Erasure page | `GET /settings/erasure` | nothing |
| Search | `POST /settings/erasure/searches` | nothing |
| Confirmation | `GET /settings/erasure/participants/{participantId}` | nothing |
| Erasure | `POST /settings/erasure/participants/{participantId}/erasure` | the erasure transaction; or one refused audit row |
| Lifting | `POST /settings/erasure/exclusion-liftings` | deletes exclusions + audit rows; or one refused audit row |
| Home page | `GET /` | unchanged; one link added |

## 2. Decisions this stage made

### 2.1 Paths under `/settings`

The Admin's other own screens live under `/settings` (`/settings/deans`,
`/settings/workspace-connection`, `/settings/access-check`). Erasure is the
operator's task, not the Dean's daily work (§2), so it joins them. Nouns only:
`erasure` (the resource the `POST` creates for a participant), `searches`,
`exclusion-liftings` (API-3).

### 2.2 Search by `POST`, answered directly

Spec I-3 keeps the term out of the address. The `POST` changes nothing, so it is
answered with `200` and the page, not a redirect: a redirect would need to carry
the term (in the address or TempData) to show the results. A browser refresh
re-posts a harmless search. Antiforgery applies because the global filter covers
every `POST`; that costs nothing.

### 2.3 Lifting without a match is `200`

Spec §8 calls "no exclusion for this address" not an error. It re-renders the
page with the outcome and keeps the typed email so the Admin can correct a typo.
Success is PRG with a message key and **no** email.

### 2.4 Read-only before validation

Both writes call the read-only guard first (spec FR-004, FR-009). A request in
read-only mode is `409` whatever its body — no `400` for a missing mark leaks
through first. That matches US-012 and keeps the refusal audit uniform.

### 2.5 `302` for redirects

The existing contracts use `302` for Post-Redirect-Get; this Story follows them.

### 2.6 The participant id in the route

Same constraint as the course card (`^[0-9]{1,19}$`); non-matching addresses
fall to the catch-all `404`, an id with no participant gets `404` from the
action (spec VR-005, FR-016). The id is internal and not personal data.

### 2.7 The new policy

`EraseParticipantData`, Admin only, applied at controller level to all five
operations (spec FR-015). Name added to `InstallationPolicies`; registered next
to `ManageDeanAccounts`.

### 2.8 Form field names

`term`, `irreversibleConfirmed`, `email`. Only `true` sets the mark (VR-002); a
checkbox posts `true` when ticked and nothing otherwise.

### 2.9 What no schema carries

No Google user id, fingerprint, salt, exclusion id or date appears in any view
model. The confirmation shows the name and email the Admin already sees
elsewhere (spec FR-003).

## 3. Operation notes

### GET /settings/erasure

Empty forms; reads the one-time `ErasureMessage` TempData; `readOnlyNotice` from
the legitimacy mode query (display only).

### POST /settings/erasure/searches

`SearchParticipantsForErasureQuery`. Case-insensitive "contains" on `Email`,
`FullName`, `Surname`, `GivenName`, every participant, ≤ 50 results plus a
`truncated` flag; counts of teacher and student memberships per result in the
same query (bounded round trips, spec §9). The term is treated literally — `%`,
`_` and `\` are escaped before reaching a `LIKE`.

### GET /settings/erasure/participants/{participantId}

`GetErasureConfirmationQuery`. Courses with role, the five counts, the no-email
notice (OD-001 a), the read-only notice.

### POST /settings/erasure/participants/{participantId}/erasure

`EraseParticipantUseCase`. Order: guard → mark → participant → transaction →
log line. Mark unset → `400` re-render of the confirmation with
`IrreversibleMarkRequired` (the query runs again to fill the page).

### POST /settings/erasure/exclusion-liftings

`LiftErasureExclusionUseCase`. Order: guard → email → fingerprint → transaction.

### GET /

`<nav id="admin-settings">` gains one link; nothing else changes.

## 4. Authentication and authorization model

| Caller | All five operations |
|---|---|
| Anonymous | `302` → `/sign-in` |
| Dean with a temporary password | `302` → `/sign-in/change-password` |
| Dean | `403` |
| Admin | allowed |

SC-4 anonymous list unchanged. Read-only mode does not change authorization; it
refuses the two writes with `409` inside `Application` (AD-6, S-04).

## 5. Error model

All responses are HTML (no `/api/v1`, so no API-6 JSON body).

| Status | When |
|---|---|
| `400` | VR-001 / VR-002 / VR-003 failure → the same page re-rendered with a field error key; missing or invalid antiforgery token → the translated error page |
| `403` | Dean |
| `404` | Unknown or already erased participant; malformed id |
| `409` | Read-only mode on erasure or lifting → translated error page with the reason |
| `500` | Failure in the erasure transaction → rolled back, single error page, no detail |

## 6. Acceptance Criterion → operation map

| AC | Operations |
|---|---|
| AC-001 | search, confirmation |
| AC-002 | erasure |
| AC-003 | erasure (`400`) |
| AC-004, AC-005, AC-010, AC-011 | none — synchronization and Meet load (Application, no HTTP) |
| AC-006 | erasure (effect in the database) |
| AC-007 | lifting (`302`, `200`) |
| AC-008 | all five (`403` Dean); erasure and lifting (`409`); home page link |
| AC-009 | erasure, lifting (audit and log effects) |
| AC-012 | erasure (effect) |
| AC-013, AC-014 | search |
| AC-015 | confirmation, erasure (`404`) |
| AC-016 | lifting |
| AC-017 | page, search, confirmation (notice); erasure, lifting (`409` + refused audit) |
| AC-018 | none — retention purge |
| AC-019 | confirmation (`noEmailNotice`), erasure |
| AC-020 | the three `POST`s (`400`) |
| AC-021 | page, confirmation, home page |

## 7. Compatibility

Additive. No existing operation, policy, route or status changes; the home page
gains one link in the Admin block. `ContractVersion.Current` stays 1.

## 8. Open questions for later stages

None blocking. For DB_DESIGN: the `ErasedParticipant` schema and salt storage,
the optional `MeetSession.OrganizerEmail`, the audit action/target values and
count columns, the FR-011 enforcement mechanism, and indexes for the
case-insensitive search.
