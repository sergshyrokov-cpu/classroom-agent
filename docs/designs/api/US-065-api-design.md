---
artifact_type: api_design
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T11:42:25Z
updated_at: 2026-10-28T11:42:25Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/decisions/US-065-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 104
  - path: docs/designs/api/US-038-openapi.yaml
    version: null
  - path: docs/designs/api/US-038-api-design.md
    version: 1
supersedes: null
---

# US-065 API Design — An Admin enters an erasure exclusion by email before the person is loaded

Contract: `docs/designs/api/US-065-openapi.yaml` (`info.version` "1").

Delegation: none. Read directly: the US-038 contract and API design,
`ErasureController`, `SignInRoutes` (erasure routes), the erasure DTOs
(`ErasurePageModel`, `LiftBlock`, `LiftFieldError`, `ErasureMessageKey`).

## 1. Scope of the contract

One new server-rendered form post in the existing Admin-only erasure area, and
an additive change to the erasure page's view model. No `/api/v1` path: the UI
is Razor (`trebovaniya.md` §8, API-1 intro), and the Specification adds no JSON
API.

| Operation | Method / path | Writes |
|---|---|---|
| Erasure page | `GET /settings/erasure` | nothing; view model gains `enter` (additive) |
| **Entry** | `POST /settings/erasure/exclusions` | salt if absent + one exclusion + audit row; or one refused audit row |

Synchronization (FR-006 … FR-008) and the Meet load (FR-009) have no HTTP
surface; lifting (FR-010) is unchanged.

## 2. Decisions this stage made

### 2.1 Path `/settings/erasure/exclusions`

The `POST` creates an exclusion, so the path is the plural noun of the resource
it creates (API-3), beside `searches` and `exclusion-liftings` (US-038 §2.1).
The spec's indicative route is kept.

### 2.2 Success is PRG with a message key

Like erasure and lifting: `302` to `/settings/erasure`, TempData key
`ErasureMessage` = `ExclusionEntered`, no email carried (spec FR-003, S-08).

### 2.3 "Already loaded" and "already excluded" are `200`

Nothing changed and nothing is wrong with the request — the same reasoning as
US-038 §2.3 for "no exclusion for this address". The page re-renders with
`enter.outcome`.

### 2.4 The entered email is echoed only on `400`

On a validation failure the Admin must correct the typed value, so it is kept in
the field of that response (spec VR-001). On `AlreadyLoaded` the matching
participants are listed with their stored emails, and on `AlreadyExcluded` there
is nothing to correct; the field is left empty, so the address appears in no
response that does not need it. This differs from lifting's
`NoExclusionForEmail` (US-038 §2.3), where a typo is the likely cause.

### 2.5 The "already loaded" list reuses the search result shape

The spec says the matches are listed exactly as US-038 search results (FR-004).
`EntryBlock.loadedParticipants` uses `ParticipantSearchResult` and the search's
order and cap of 50 with a `loadedTruncated` flag. Several participants can
share an email (US-038 I-7); 50 for one exact address is a safety bound, not an
expected size.

### 2.6 Read-only before validation

As US-038 §2.4: the guard runs first, so a request in read-only mode is `409`
whatever its body, and the refusal audit is uniform (spec FR-012).

### 2.7 One set of field-error values

The entry and the lifting share one validation (spec VR-001). The contract
fixes the values (`EmailRequired`, `EmailTooLong`, `EmailInvalid` — today's
`LiftFieldError`) and calls the type `EmailFieldError`; whether the existing
type is reused as is or renamed is IMPLEMENTATION's.

### 2.8 Field name

`email`, as in the lifting form.

## 3. Operation notes

### GET /settings/erasure

Unchanged except the view model: `enter` (empty block) and the new message key.
`readOnlyNotice` now also disables the entry button (AC-019). The page reads
only the legitimacy mode — no `WorkspaceConnection`, no Google (AC-002).

### POST /settings/erasure/exclusions

`EnterErasureExclusionUseCase` (indicative). Order: guard → email → transaction
under the exclusive erasure fence { loaded check → salt get-or-create →
fingerprint → existing-exclusion check → insert exclusion + audit } → commit →
PRG. The salt is created only on the way to an insert (see the contract's
`x-writes`).

## 4. Authentication and authorization model

| Caller | Page and entry |
|---|---|
| Anonymous | `302` → `/sign-in` |
| Dean with a temporary password | `302` → `/sign-in/change-password` |
| Dean | `403` |
| Admin | allowed |

Existing policy `EraseParticipantData` at controller level (spec FR-014). SC-4
anonymous list unchanged. Read-only mode does not change authorization; it
refuses the entry with `409` in `Application` (AD-6, S-04).

## 5. Error model

All responses are HTML (no `/api/v1`, so no API-6 JSON body).

| Status | When |
|---|---|
| `200` | `AlreadyLoaded` / `AlreadyExcluded` — not errors |
| `400` | VR-001 failure → page with `enter.fieldError`; missing or invalid antiforgery token → the translated error page |
| `403` | Dean |
| `409` | Read-only mode → translated error page with the reason; one refused audit row |
| `500` | Failure in the transaction → rolled back, single error page, no detail |

## 6. Acceptance Criterion → operation map

| AC | Operations |
|---|---|
| AC-001 | entry (`302`, effect + audit) |
| AC-002 | page, entry — no `WorkspaceConnection` |
| AC-003, AC-004, AC-013, AC-015, AC-016, AC-017 | none — synchronization (Application, no HTTP); AC-017's re-entry part → entry (`200` `AlreadyLoaded`) |
| AC-005 | none — Meet load |
| AC-006 | entry (`200` `AlreadyLoaded` + list) |
| AC-007 | entry (`200` `AlreadyExcluded`) |
| AC-008 | lifting (unchanged US-038 operation) after entry |
| AC-009 | entry (`409` + refused audit) |
| AC-010 | page, entry (`403` Dean, `302` anonymous) |
| AC-011 | entry (`400`) |
| AC-012 | entry vs synchronization (Application, fence) |
| AC-014 | entry (normalization in the loaded and excluded checks); sync part has no HTTP |
| AC-018 | entry (`400` antiforgery) |
| AC-019 | page (`readOnlyNotice`, disabled button) |
| AC-020 | page, entry (texts) |
| AC-021 | entry then lifting |

## 7. Compatibility

Additive. No existing operation, policy, route or status code changes. The
erasure page's view model gains one property and the message-key enum one
value. `ContractVersion.Current` stays 1.

## 8. Open questions for later stages

None blocking. For DB_DESIGN: nullable Google user id and the "at least one of
id and fingerprint" check; the immutability rule allowing the one fill; the
new audit action value and code; reuse of the erasure fence for the entry
(spec FR-011); any index for the sync match by fingerprint among exclusions
without an id.
