---
artifact_type: api_design
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-21T07:58:59Z
updated_at: 2026-09-21T07:58:59Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
supersedes: null
---

# US-011 API Design — Check access diagnostic

Companion to `docs/designs/api/US-011-openapi.yaml`.

## 1. Scope of the contract

**Two** operations on the installation's public port, server-rendered:

| Operation | Purpose | Calls Google | Writes |
|---|---|---|---|
| `GET /settings/access-check` | the page: what the check does, what it would check | no | nothing |
| `POST /settings/access-check` | run the check, render the result | yes (read-only) | one audit row |

The startup self-check (spec FR-010) has **no HTTP surface**. There is no `/api/v1`
path, the Control Plane gains no endpoint, the private port is untouched,
`ClassroomAgent.Contracts` gains no type and `ContractVersion.Current` stays **1**.

The settings section of US-009 gains its third entry, next to the connection
settings and the instruction (spec FR-007).

## 2. Decisions this stage made

### 2.1 A Razor form, not `POST /api/v1/workspace-connection/test`

Spec I-10 left the shape to this stage. `api-conventions.md` API-3 reserves
`POST /api/v1/workspace-connection/test` for the check, and the conventions
themselves say `/api/v1` is the REST API *serving* the UI, while Razor routes are
not part of the versioned contract. The admin panel is server-rendered, US-009 and
US-010 are plain Razor pages, and nothing in this Story calls the check by script.
Shipping the `/api/v1` path anyway would add an endpoint with no caller — an
untested-in-practice surface with its own error body (API-6) and its own
antiforgery header path.

So the check is **one** `POST`, a Razor form on the same path as its page. The
API-3 shape stays a reservation: a later Story that adds a JSON client adds it
there, under API-5 and API-6. This is recorded in `x-operations-deliberately-absent`
so a reader does not mistake it for a gap.

### 2.2 No Post-Redirect-Get

US-009's save redirects to its page and shows a confirmation from TempData. That
works because the saved values are in the database. Here the result is **not
stored** (OD-003), so a redirect would lose it — unless it were put into TempData,
which is a cookie: the result would then travel through the browser and back, for
no gain. The `POST` therefore answers `200` with the page that carries the result.
The cost is the browser's "resubmit?" prompt on reload, which re-runs the check —
harmless, since OD-005 sets no limit and every run is audited.

### 2.3 A failed step is `200`, not an error

The check succeeds as an *action* when it runs, whatever it finds (spec §8, I-4).
`NotConfigured` and `Inconclusive` are results, and the Admin needs the page that
explains them. Answering `4xx`/`5xx` for them would conflate "the program could not
do what you asked" with "the program did it and here is the diagnosis".

### 2.4 Two kinds of `409`, one status

Both refusals are state conflicts (API-5) and both make no Google call:

| Case | Category (audit) | Rendered by |
|---|---|---|
| read-only mode (BR-025, any cause) | `ReadOnlyMode` (existing) | the host-wide US-008 handler — the translated error page |
| no usable connection (`NotConfigured`, `DomainMismatch`) | `ConnectionNotUsable` (new) | the page, re-rendered with `messageKey` |

`DomainUnknown` never reaches the second case: it means no legitimacy check has
ever succeeded, which is a read-only cause, and the guard runs first (spec FR-006
step 1). The order is therefore observable and tested: a school that is both
read-only and unconfigured answers with the read-only reason.

The audit category name `ConnectionNotUsable` is proposed here for DB_DESIGN to
confirm, together with the `AuditAction` member (spec FR-008 proposes
`AccessCheckRun`).

### 2.5 The view model states the step list as a contract constant

`AccessCheckResult.steps` has exactly eight entries, and the six delegation steps
carry the scope URIs in the order of `GoogleDelegationScopes.All`, listed in
`x-scopes-in-order`. The forbidden scopes are listed in `x-never`. Both are
machine-checkable, so TEST_WRITING asserts the rendered page and the substituted
port's recorded calls against them (spec VR-002, S-04).

### 2.6 What the model cannot carry

No key, no key reference, no token, no Google error text, nothing from a Google
response — the absences are in the schema descriptions and are testable (spec S-07,
S-08). A step's message is a **translation key**, never a sentence built from
Google's answer. The technical account and the domain *are* carried: they are the
values the Admin saved (US-009) and are shown on US-009's own page already.

### 2.7 Policy name

`RunAccessCheck` — its own policy for its own matrix row (spec FR-011, I-9), in the
naming pattern of `ConfigureWorkspaceConnection` and `ViewConnectionInstruction`.

## 3. Operation notes

### `GET /settings/access-check`

- Builds the view model from two reads — the US-009 connection query and the
  read-only mode query — and nothing else. **No Google call, no secret-store read,
  no write** (API-4).
- `result` is null.
- In read-only mode the page renders with `readOnly: true` and the reason; the
  button stays (AD-6).

### `POST /settings/access-check`

- Order of evaluation, each step observable in tests: authentication (`302`) →
  authorization (`403`) → antiforgery (`400`) → read-only guard (`409`, error
  page) → connection state (`409`, page) → the eight steps → audit row → `200`.
- No field is bound from the body (spec VR-001).
- Bounded by 30 seconds (spec I-2): the response always arrives; steps that ran out
  of time are `GoogleUnavailable` and the verdict is `Inconclusive`.
- The request's `CancellationToken` flows to the port; a browser that disconnects
  cancels the remaining calls (spec §8).

## 4. Authentication and authorization model

| Operation | Anonymous | Dean | Admin | Antiforgery |
|---|---|---|---|---|
| `GET /settings/access-check` | `302 /sign-in` | `403` | `200` | not required (GET) |
| `POST /settings/access-check` | `302 /sign-in` | `403` | `200` / `409` | required |

Both declare `RunAccessCheck`. The SC-4 anonymous list and the antiforgery
exemption list are unchanged. The forbidden-role case uses a synthetic Dean
principal against the real policy until US-012 lets a Dean sign in (carried US-010
F-1).

## 5. Error model

All responses are HTML; there is no API-6 JSON body in this Story.

| Status | When | Body |
|---|---|---|
| `302` | not signed in | redirect to `/sign-in` |
| `400` | missing/invalid antiforgery token | translated error page |
| `403` | a Dean | translated error page |
| `409` | read-only mode | translated error page naming the BR-025 reason |
| `409` | no usable connection | the page with `messageKey` |
| `500` | unexpected failure | the single error page, no detail (API-10, SC-10) |

No status carries Google's error text, a key, a reference or a token (SC-10).
There is no `429`: nothing is rate-limited (OD-005), and Google's own `429` is a
`GoogleUnavailable` step, never forwarded (API-5).

## 6. Acceptance Criterion → operation map

| AC | Operation(s) | Evidence in the contract |
|---|---|---|
| AC-001 | both | `RunAccessCheck` policy; `302`/`403` responses; lists unchanged |
| AC-002 | `POST` | six `Delegation` steps, `x-scopes-in-order`, `x-never` |
| AC-003 | `POST` | `ClassroomRead`, `ReportsRead`; `200` for empty answers; S-08 absences |
| AC-004 | `POST` | `AccessCheckStepOutcome`, `messageKey`, `notAttemptedBecause` |
| AC-005 | `POST` | `409` case (2), `ConnectionNotUsable` |
| AC-006 | `POST` (and `GET` stays viewable) | `409` case (1), no Google call |
| AC-007 | `POST` | `x-writes`; `200` → `Succeeded` row, `409` → `Refused` row |
| AC-008 | none (no HTTP surface) | stated in §1 |
| AC-009 | `POST` | port substitution — not an HTTP concern; the contract is testable without Google |
| AC-010 | both | every sentence by `messageKey` / `readOnlyReasonKey` |

## 7. Compatibility

- Additive only: two new paths; no existing operation changes.
- The US-008 endpoint enumeration test must classify both new endpoints as
  protected and not anonymous.
- The landing page's settings section gains an entry; its existing entries are
  unchanged.

## 8. Open questions for later stages

None blocking. Notes:

- **DB_DESIGN** confirms the names `AccessCheckRun` (`AuditAction`) and
  `ConnectionNotUsable` (`AuditRefusalCategory`) and decides whether the audit
  table's check constraints need an amending migration (spec FR-013). It adds no
  table and no column.
- **TEST_WRITING** asserts the evaluation order of §3, including that a read-only
  *and* unconfigured school answers with the read-only reason and records zero port
  calls.
- `api-conventions.md` API-3 still lists `POST /api/v1/workspace-connection/test`.
  Whether to keep it as a reservation or remove it is a documentation decision
  outside this Story (§2.1).
