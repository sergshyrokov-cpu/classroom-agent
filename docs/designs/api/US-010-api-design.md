---
artifact_type: api_design
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-20T19:59:53Z
updated_at: 2026-09-20T19:59:53Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-010-spec.md
    version: 1
  - path: docs/decisions/US-010-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
supersedes: null
---

# US-010 API Design — Connection instructions for the school super-admin

Companion to `docs/designs/api/US-010-openapi.yaml`.

## 1. Scope of the contract

**One** operation, on the installation's public port, server-rendered:

| Operation | Purpose |
|---|---|
| `GET /settings/connection-instruction` | the instruction for the school's super-admin |

Nothing else changes. There is no `POST`, no download endpoint and no
`/api/v1` path. The Control Plane gains no endpoint, the private port of US-005
and US-006 is untouched, `ClassroomAgent.Contracts` gains no type, and
`ContractVersion.Current` stays **1**.

The settings section US-009 created gains a second entry leading here (spec
FR-013). A navigation entry is not an operation: `GET /` keeps the contract
US-008 gave it.

This is the smallest contract any Story in the project has produced, and that is
the point rather than an accident — see §2.1.

## 2. Decisions this stage made

### 2.1 One `GET`, and the absence of everything else is a decision, not an omission

OD-002 resolved that the instruction is handed over **from the page**, with a
copy-to-clipboard affordance. Three consequences are contract-level and are
recorded in `x-operations-deliberately-absent` so a later reader does not mistake
them for gaps:

- **no download operation.** A `.txt` or `.pdf` would need a content type, a file
  name and a place in the SC-4 and antiforgery rules — and, worse, a stored
  document outlives a rotated client ID, which is exactly what AC-006 exists to
  prevent. The current instruction is always the page.
- **no email.** The program has no mail channel and SC-13 lists exactly two
  outbound destinations; a third is a requirements change.
- **no `POST` of any kind.** Nothing on this page is submitted.

The third one removes two status codes that every other admin screen in this
project has: there is **no `400`** (no request to malform, no antiforgery token to
be missing — antiforgery never validates a `GET`, SC-4 v64) and **no `409`** (no
action for the installation's state to refuse). The only failure answers are the
sign-in redirect for an anonymous visitor and `403` for a Dean.

### 2.2 The path is singular

`/settings/connection-instruction`, not `…/connection-instructions`. API-3's
plural rule is about collections, and there is exactly one instruction per
installation — it is generated for one `Installation` (§6: "одной бумажки на всех
быть не может"). This follows US-009's reasoning for
`/settings/workspace-connection` and keeps the section's two paths consistent.

The Story's English title says "instructions"; that is prose about the document a
school receives, not a resource name.

### 2.3 Read-only mode does not change the status code

Every other write screen in this project maps read-only mode to `409` through the
host-wide handler US-008 built. Here it maps to **nothing**: BR-026 names the
connection instruction as viewable in read-only mode, so all three causes of
BR-025 answer `200` with the reason named in the response body
(`readOnlyReasonKey`).

This is worth stating explicitly because it is the first operation in the project
where read-only mode is *visible in the response but absent from the status
line*. `readOnly` is a field, not a gate. Nothing is written, so the read-only
guard is never invoked and the `409` path is never reached (spec FR-009, FR-010).

### 2.4 Read-only-ness and completeness are two independent facts

`state` has exactly two members — `Complete` and `InstallationNotConfirmed` — and
read-only mode is **not** one of them. All four combinations occur and each must
render correctly:

| `state` | `readOnly` | When |
|---|---|---|
| `Complete` | `false` | the normal case (flow 3.1) |
| `Complete` | `true` | a school suspended or past its grace period whose last check recorded both values (flow 3.3) |
| `InstallationNotConfirmed` | `true` | no check has ever succeeded — which is itself a read-only cause (BR-025) |
| `InstallationNotConfirmed` | `false` | a check succeeded but the stored domain or client ID is empty — defensive, and the page must not crash on it |

Folding the two into one enum would have made the third row unrepresentable
without merging two unrelated ideas, and the fourth undetectable.

### 2.5 The scope list is a contract constant, not just a documented example

The `scopes` array carries a JSON Schema `const` with all six URIs and
`minItems`/`maxItems` of 6. The list is a **requirement** (§6, fixed in v25), not
a configuration: an installation cannot change it and no school may authorise a
different set. Pinning it in the contract means TEST_WRITING has a machine-checkable
list to assert the rendered page against, which is what AC-003 asks for.

The contract also records what must be **absent**: `drive.file` and
`classroom.profile.photos` (the two the prototype requested, which no Epic uses)
and the identity scopes `openid`, `email`, `profile` (the Admin's own sign-in is a
different mechanism, §6 v78). An absence is testable and this contract states it
so the test is not left to invention.

### 2.6 The view model carries what varies; the instruction's prose does not

The model has six fields plus the state. The technical-account statements, what
the super-admin actually does, and every label are **translated text the view
renders from named keys** — one key per paragraph or statement (spec FR-012, I-8)
— and are recorded in `x-static-text-not-in-the-model` rather than as properties.

Two reasons, both binding: `Application` must hold no user-visible sentence
(AD-6), and a model field invites a caller to substitute what the requirements
fix.

### 2.7 The model is defined as much by what it cannot carry

`ConnectionInstructionPageModel` documents three absences explicitly, because on
this page each of them would be a defect with a real consequence:

- **no secret** — not the service-account key, not the reference to it, not the
  OAuth client secret, not a session identifier (SC-7, a Hard Stop; PC-9);
- **not the OAuth web client id** and **not the `Installation` UUID** — §6 (v78)
  names three identifiers that must not be confused, and printing the wrong one
  would send a school to authorise the wrong client, after which delegation
  silently does not work (spec FR-006);
- **not the service account's own `…@….iam.gserviceaccount.com` address** — §9
  (v53) keeps it a machine account known from the key in the secret store, and the
  console step needs the client ID, not the address.

The first is a Hard Stop; the second is the trap this Story exists inside. A test
asserting that the configured OAuth client id does not appear in the response is
part of the contract's meaning, not an extra.

### 2.8 The copy affordance adds no endpoint and no anonymous entry

The affordance is a static script file from the application's static files
directory, assembling the copied text from values already in the response. Two
contract consequences:

- the SC-4 anonymous closed list gains **nothing**: its existing "Static files —
  CSS, JS, images" entry already covers the file (`trebovaniya.md` §8, v68), which
  carries no data and nothing user-specific;
- there is no second request, so nothing new to authorise and nothing new to log.

The page is complete without the script (spec FR-008), so the contract's `200` is
correct with scripting disabled — the affordance is enhancement over a response
that already satisfies AC-009.

## 3. Operation notes

### `GET /settings/connection-instruction`

- **Policy `ViewConnectionInstruction`** — the matrix row "Просмотр инструкции по
  подключению", `✔` Admin, `✘` Dean (§2, v39). Deliberately **not**
  `ConfigureWorkspaceConnection`: §2 split the rows because one is a write
  read-only mode blocks and the other a read it permits (spec I-7). Both the
  allowed-role and the forbidden-role case are tested (TC-5).
- **No parameters.** No route parameter, no query parameter, no body. A query
  string is ignored and never echoed into the response or a log (spec VR-001), so
  there is no input by which a caller can influence the rendered text — which is
  what makes this page safe to serve to an Admin in any installation state.
- **Reads, per request:** `LegitimacyState` (the domain and the client ID) and the
  read-only mode decision. Two database reads, no network call. The values are
  re-read every time, which is what makes AC-006 true: a rotated client ID appears
  with no restart and no cache to clear.
- **Writes: none.** No audit row (§5 does not list viewing this page, spec I-6),
  no table, in any mode. This is a `GET` that changes nothing, as API-4 and SC-4
  require of every `GET` but the OAuth callback.
- **`200` in every installation state**, including all three read-only causes and
  the never-legitimated case.

## 4. Authentication and authorization model

| Aspect | Rule |
|---|---|
| Authentication | the installation's own session cookie (US-008), checked against the account's security stamp on every request. No Google token is presented to this host. |
| Authorization | `ViewConnectionInstruction`, declared on the operation; deny by default catches a forgotten attribute (SC-4, API-9). |
| Anonymous | impossible. The visitor is challenged to `/sign-in`; the SC-4 closed list gains nothing. |
| Dean | `403` and the translated error page, revealing no part of the instruction. |
| Antiforgery | not applicable — no state-changing request exists, and antiforgery never validates a `GET` (SC-4, v64). |
| Enforcement location | the policy is defined in `Application/Authorization` and registered in the host's `Security` namespace, next to US-009's (SC-4). |

## 5. Error model

| Status | When | Body |
|---|---|---|
| `302` → `/sign-in` | not signed in | — |
| `403` | signed in as a Dean | the translated error page |
| `404` | a wrong HTTP method or an unmatched path | the host's existing rules; this Story adds no method and changes no handler |
| `500` | an unexpected failure | the translated error page, with no exception text and no stack trace (AD-9, API-10, SC-10) |

**No `400` and no `409`** — see §2.1. And three situations that are *not* errors
and answer `200`: the domain or client ID not yet known (spec FR-002), read-only
mode (spec FR-009), and an unreachable Control Plane (nothing is fetched at render
time, spec FR-003).

The US-008 security-review finding F-5 — a wrong method on an *anonymous* route
answering `404` where the contract documents `405`, because the anonymous
catch-all matches first — is not re-opened here and is not inherited: this route
is not anonymous, and this contract documents no `405`.

## 6. Acceptance Criterion → operation map

| Story AC | Operation | Covered by |
|---|---|---|
| AC-001 Only an Admin sees the instruction | `GET` | `x-policy: ViewConnectionInstruction`; `302`; `403` |
| AC-002 This school's own client ID | `GET` | `serviceAccountClientId`; §2.7 absences |
| AC-003 The scope list is exactly the one fixed | `GET` | `scopes` `const`, 6 items; the recorded absences |
| AC-004 What the technical account must be | `GET` | `x-static-text-not-in-the-model` (the five FR-005 statements as translation keys) |
| AC-005 Readable when nothing else works | `GET` | `x-host-wide-rules.read-only-mode-is-not-a-refusal`; `readOnly`, `readOnlyReasonKey` |
| AC-006 A rotated client ID appears | `GET` | the operation's description: read per request, no cache |
| AC-007 Reading changes nothing | `GET` | `x-host-wide-rules.no-write-of-any-kind` |
| AC-008 Before the first successful check | `GET` | `state: InstallationNotConfirmed`; `notConfirmedMessageKey`; nullable values |
| AC-009 Ready to hand over | `GET` | §2.8; the `200` is complete without scripting; no download operation |
| AC-010 Every string is translated | `GET` | keys, never sentences, in the model; `x-static-text-not-in-the-model` |

## 7. Compatibility

- **No existing contract changes.** US-008's and US-009's operations, request and
  response shapes are untouched; this contract only inherits their host-wide
  rules through `x-extends`.
- **No wire type, no contract version change.** `ContractVersion.Current` stays 1
  (DC-12).
- **Forward compatibility:** a later Story that needs the instruction as data adds
  `GET /api/v1/connection-instruction`, and `ConnectionInstructionPageModel` is
  the shape to reuse — the field names here are chosen so that they could be
  serialised as JSON unchanged. Reopening OD-002 for a download would add its own
  operation; this contract reserves no path for one.
- **US-011 and US-012** add their own pages to the same settings section. Nothing
  in this contract constrains their paths.

## 8. Open questions for later stages

- **DB_DESIGN should return `NOT_APPLICABLE` or an explicitly empty design.**
  This Story changes no schema and ships **no migration** (spec FR-015): it reads
  `legitimacy_state`, which US-005 created. A migration appearing in US-010 is a
  defect. DB_DESIGN's own artifact should say so rather than being skipped
  silently, so IMPLEMENTATION has the statement in writing.
- **TEST_WRITING owns three spellings this contract fixes**: the path
  `/settings/connection-instruction`, the policy name
  `ViewConnectionInstruction`, and the six scope URIs in their fixed order. The
  translation keys themselves are fixed by TEST_WRITING, as US-009 established —
  the Specification fixes that keys exist, not their spelling.
- **The absence tests are part of the contract, not extras**: that the configured
  OAuth client id, the `Installation` UUID, `drive.file`,
  `classroom.profile.photos` and `openid`/`email`/`profile` appear nowhere in the
  response, and that no Workspace admin-role name appears in either translation
  file (spec VR-005, OD-001).
- **The forbidden-role test** still uses a synthetic Dean principal rather than a
  signed-in Dean, because US-012 has not arrived — the same limitation US-009
  finding F-1 records, carried forward unchanged.
