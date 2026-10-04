---
artifact_type: api_design
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T06:33:30Z
updated_at: 2026-10-04T06:33:30Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 81
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
supersedes: null
---

# US-019 API Design — Trigger a synchronization from the UI

Companion to `docs/designs/api/US-019-openapi.yaml`.

## 1. Scope of the contract

**One new** operation on the installation's public port, and two existing pages
changed additively:

| Operation | Purpose | Calls Google | Writes |
|---|---|---|---|
| `POST /synchronization/requests` (new) | request a run now | no | one audit row; one in-process request |
| `GET /settings/workspace-connection` (US-009) | gains the Admin's button and the one-time message | no | nothing |
| `GET /` (US-008) | gains the Dean's button and the one-time message | no | nothing |

No `/api/v1` path, no Control Plane change, no private-port change;
`ClassroomAgent.Contracts` gains nothing and `ContractVersion.Current` stays **1**.

## 2. Decisions this stage made

### 2.1 One path for both roles, outside `/settings`

The matrix row is granted to both roles; `/settings/*` is the Admin's settings
section. A Dean posting to a `/settings` path would blur that boundary, and two
paths would be two endpoints for one matrix row. The action therefore lives at
`/synchronization/requests` — a noun naming what is created, in the style of
API-3, though Razor routes are outside the versioned contract.

### 2.2 Post-Redirect-Get on success

Unlike US-011 (whose result is not stored and would be lost by a redirect), the
outcome here is a single message code. PRG with TempData (as US-012 does, under
its own key) gives:

- no "resubmit?" prompt — a reload never enqueues and audits a second request;
- the user lands back on their own page, which renders its usual content plus the
  message once.

The TempData key is new (`SynchronizationMessage`) so it never collides with
`DeanAccountMessage`. The value is a code from a closed enum, never data.

### 2.3 The return page comes from the role

`x-return-page-by-role`: Admin → `/settings/workspace-connection`, Dean → `/`.
No `returnUrl` is read (spec VR-002), so the endpoint is not an open redirect.
The role is read from the session, as `HomeController` already does.

### 2.4 Two kinds of `409`, as US-011

| Case | Category (audit) | Rendered by |
|---|---|---|
| read-only mode (BR-025, any cause) | `ReadOnlyMode` (existing) | the host-wide US-008 handler — the translated error page naming the reason |
| no usable connection (`NotConfigured`, `DomainMismatch`) | `ConnectionNotUsable` (existing, US-011) | the role's page re-rendered with `synchronizationMessageKey` |

`DomainUnknown` never reaches the second case: it means no legitimacy check has
ever succeeded, which is a read-only cause, and the guard runs first. A school
that is both read-only and unconfigured answers with the read-only reason.

The `409` re-render is not a redirect, because a refusal is not a success and
API-5 gives it `409`; the page builders of the two existing `GET`s are reused so
the re-rendered page is identical to the `GET` plus the message.

### 2.5 The message codes

`SynchronizationMessageKey` is a closed list of four codes; each maps to a
translation key in both resource files (spec FR-008). The Admin and Dean texts
for an unusable connection differ (OD-009 a): the Admin is told to save the
connection, the Dean to ask the Admin.

### 2.6 Policy name

`StartSynchronization` — its own policy for its own matrix row (spec FR-007,
I-5), granted to Admin and Dean.

### 2.7 The port

The synchronization-request port of spec FR-002 is an `Application` interface
whose adapter wraps `SyncRunCoordinator` (`Request`, `IsRunning`, `IsPurging`).
The coordinator lives in `ClassroomAgent.Web`, so the adapter lives there too,
registered in the installation's composition root next to the coordinator; it
is not a Google port and does not belong in `Infrastructure` (AD-4 concerns what
is outside the process). `Application` references only the interface (AD-3).
The read of "in progress" and the request are made under the coordinator's
lock or in immediate succession; spec I-1 accepts either.

## 3. Operation notes

### `POST /synchronization/requests`

- Order of evaluation, each step observable in tests: authentication (`302
  /sign-in`) → restricted session of a Dean with a temporary password (`302
  /sign-in/change-password`) → authorization → antiforgery (`400`) → read-only
  guard (`409`, error page, audit `Refused`/`ReadOnlyMode`) → connection state
  (`409`, page, audit `Refused`/`ConnectionNotUsable`) → enqueue → audit
  `Succeeded` → `302` to the role's page with the message.
- No field is bound from the body (spec VR-001).
- Returns at once; never waits for the run (BR-040).
- An Admin and a Dean are both allowed; there is no forbidden *signed-in* role
  in v1. The forbidden-role tests are the anonymous visitor and the restricted
  session (spec S-02).

### `GET /settings/workspace-connection`, `GET /`

- Additive view-model fields only; policies, status codes and every existing
  field unchanged.
- The button is rendered in read-only mode too (OD-006). The home page renders
  it for a Dean only (I-6).
- The one-time message is read from TempData and shown once.

## 4. Authentication and authorization model

| Operation | Anonymous | Dean (temporary password) | Dean | Admin | Antiforgery |
|---|---|---|---|---|---|
| `POST /synchronization/requests` | `302 /sign-in` | `302 /sign-in/change-password` | `302 /` / `409` | `302 /settings/workspace-connection` / `409` | required |
| `GET /settings/workspace-connection` | unchanged | unchanged | unchanged (`403`) | unchanged | — |
| `GET /` | unchanged | unchanged | unchanged | unchanged | — |

The SC-4 anonymous list and the antiforgery exemption list are unchanged.

## 5. Error model

All responses are HTML; there is no API-6 JSON body in this Story.

| Status | When | Body |
|---|---|---|
| `302` | accepted; not signed in; restricted session | redirect (see contract) |
| `400` | missing/invalid antiforgery token | translated error page |
| `409` | read-only mode | translated error page naming the BR-025 reason |
| `409` | no usable connection | the role's page with `synchronizationMessageKey` |
| `500` | unexpected failure | the single error page, no detail (API-10, SC-10) |

There is no `403` for a signed-in user (both roles are granted) and no `429`
(OD-004).

## 6. Acceptance Criterion → operation map

| AC | Operation(s) | Evidence in the contract |
|---|---|---|
| AC-001 | `POST`, `GET /settings/workspace-connection` | `302` case 1 → Admin page, `Requested`; `x-writes`, `x-enqueues` |
| AC-002 | `POST`, `GET /` | `302` case 1 → `/`, `LandingPageModelAddition` carries no run data |
| AC-003 | `POST` | `RequestedAfterCurrentWork`; `x-enqueues` |
| AC-004 | `POST` | each press answered and audited; one remembered request (coordinator) |
| AC-005 | `POST` | `409` case 1; nothing enqueued |
| AC-006 | `POST` | `StartSynchronization`; `302 /sign-in`; restricted session; `400` |
| AC-007 | all three | every message by `SynchronizationMessageKey` → translation key |
| AC-008 | `POST` | `x-calls-google: false`; the port is substitutable |
| AC-009 | `POST` | `409` case 2, `ConnectionNotUsable`, role-specific message |

## 7. Compatibility

- Additive only: one new path; two existing view models gain fields.
- The US-008 endpoint enumeration test must classify the new endpoint as
  protected and not anonymous.

## 8. Open questions for later stages

None blocking. Notes:

- **DB_DESIGN** confirms the `AuditAction` name (`SynchronizationRequested`
  proposed by spec FR-005) and names the migration amending
  `ck_audit_event_action`. `AuditRefusalCategory` reuses `ReadOnlyMode` and
  `ConnectionNotUsable`; `AuditTargetType` gains nothing (target null, spec I-3).
- **TEST_WRITING** asserts the evaluation order of §3, including that a
  read-only *and* unconfigured school answers with the read-only reason and
  records zero port calls, and that a submitted `returnUrl` is ignored.
- `api-conventions.md` API-4 still lists `POST /api/v1/sync`; whether to keep it
  as a reservation is a documentation decision outside this Story (spec I-4).
