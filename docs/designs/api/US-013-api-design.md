---
artifact_type: api_design
story: US-013
version: 1
status: DRAFT
created_at: 2026-09-27T11:55:30Z
updated_at: 2026-09-27T11:55:30Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/decisions/US-013-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-013 API Design — Background synchronization service

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-013-openapi.yaml`.** Its absence is
this stage's recorded decision, not a missing artifact — the same decision US-007
recorded for the same reason. A contract file describing zero operations would
assert a surface this Story does not have. Downstream stages that list `openapi`
among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when "the approved Specification
explicitly states the Story does not change public API behavior". The approved
Specification (v1) states exactly that, in four places:

- **FR-016** — "The Story adds **no endpoint, no page, no authorization policy and
  no route**. SC-4's closed list of anonymous endpoints is unchanged, and the US-008
  endpoint enumeration test gains no row."
- **VR-005** — "This Story exposes no endpoint, no page and no form, so it accepts
  no request body, query or route parameter and no uploaded file."
- **§10** — starting a run from the UI, "the endpoint, its policy and its audit
  row", is out of scope and belongs to US-019.
- **§7 S-07** — "No endpoint, page or policy is added; SC-4's anonymous list is
  unchanged."

The cause is the Story's own scope, fixed by two resolved Open Decisions: OD-001
(host only — the service, the schedule and the state record, no data import) and
OD-007 (the trigger seam is an **in-process coordinator**, not an HTTP operation).
The whole Story lives below the presentation boundary.

## 2. What the Story does deliver, and why none of it is a contract

| Specification | Delivered artefact | Why it is not an API surface |
|---|---|---|
| FR-001, FR-002 | the run, `SyncState` | a Domain entity and one table; read by no endpoint until US-024 |
| FR-003, FR-011 | the schedule and the first-run wait | timing inside a `BackgroundService`, driven by `TimeProvider` |
| FR-004 | the one-run-at-a-time coordinator | a process-wide singleton with in-process members, the `PushCheckCoordinator` shape; **its out-of-schedule entry point is a method, not a route** (OD-007) |
| FR-005 | the run use case and its three-shape outcome | an `Application` type; the outcome is not a response body and never becomes one in this Story |
| FR-006 | the `SyncState` port and its two writes | an internal port implemented in `Infrastructure` (AD-4) |
| FR-007, FR-008 | skipped and failed runs | log lines and stored state, never a status code |
| FR-012 | four log events with the run identifier | log events, governed by DC-10 and SC-10 |
| FR-013 | `Sync:IntervalMinutes` | deployment configuration (DC-3), not a wire format |
| FR-015 | the `AddSyncState` migration | schema, owned by DB_DESIGN |
| FR-017, FR-018 | no audit row, no translation key, no package | absences, by definition not a surface |

## 3. Existing contracts this Story must not change

Confirmed against the approved Specification; each is an explicit no-change:

- **Readiness on the private port** (US-005 FR-013; DC-11). Specification FR-014
  changes **which states the existing endpoint reports**, not its route, its method,
  its media type or its status-code mapping: `Unhealthy` already answers `503` and
  `Degraded`/`Healthy` already answer `200`. The new condition — the synchronization
  background service is not running — makes an existing state reachable for a new
  reason. **Read-only mode must keep answering `Degraded` with `200`** (DC-7, DC-11),
  so a suspended school keeps viewing and exporting. This is a behaviour change
  inside an unchanged contract, and it is the one thing a reviewer should check
  here.
- **Liveness on the private port** (US-005) and the **status-change push receiver**
  (US-006): untouched, including the port filter that keeps all three off the public
  port (SC-9, DC-6).
- **The installation's public port** — the sign-in pages, the settings pages and the
  Dean-account pages of US-008 … US-012: untouched. No route is added, none is
  changed, and no page gains a field.
- **The legitimacy-check service channel** (US-005, US-006) and
  `ClassroomAgent.Contracts`: unchanged. `ContractVersion.Current` stays as US-005
  set it — this Story sends nothing to the Control Plane and receives nothing new
  from it.
- **The Control Plane's own pages and endpoints** (US-001 … US-006): untouched; the
  Story is entirely on the installation side.

## 4. What US-019 inherits

Recorded here so the next API design does not have to re-derive it:

- The coordinator's out-of-schedule entry point (FR-004) is what US-019's endpoint
  calls. AD-5 fixes the shape: the request **enqueues** the run and returns
  immediately; it never waits for the run and never reports its result.
- The permission matrix gives "Запуск синхронизации" to **both** Admin and Dean
  (`trebovaniya.md` §2, BR-004) — unlike every settings page, which is Admin-only.
- A **manual** start is an audited action (`trebovaniya.md` §5, SC-11), so US-019
  adds the `AuditAction` member, its check-constraint migration and the audit row.
  US-013 adds none of that, because a scheduled run writes no audit row
  (Specification FR-017).
- In read-only mode that endpoint must refuse like every other blocked action —
  `409` with the API-6 error body (API-5), which the guard already produces as
  `ReadOnlyModeException`; US-013 turns the same refusal into a silent skip because
  it has no caller to answer (Specification FR-007).
- A `POST` needs the antiforgery token (API-7), as every state-changing form in the
  installation does.

## 5. Acceptance Criterion → operation map

None. No Acceptance Criterion of US-013 (AC-001 … AC-009) is satisfied by an HTTP
operation. AC-001 is the closest — it asserts what the **existing** readiness
endpoint reports — and is satisfied by `GetReadinessQuery` in `Application` plus the
marker the background service sets, not by any change to the endpoint's contract
(Specification §12).

## 6. Auth model

Unchanged. The Story adds no endpoint and therefore no authorization policy; the
three policies US-012 registered and the deny-by-default fallback stand as they are.
Readiness keeps its only access rule — the Owner's private network, enforced by the
existing private-port filter (SC-9, DC-6, DC-11) — and still answers with a state
and nothing else.

A synchronization run has **no caller and no principal**: it is a background action,
which is why `trebovaniya.md` §5 describes an audit actor of `system` for background
work and why this Story writes no audit row at all (Specification FR-017).

## 7. Error model

Unchanged at the HTTP boundary. Inside the process, Specification §8 is the
authority: a read-only refusal and a missing connection become *skipped* outcomes,
not errors; a failed run is recorded in `SyncState` and logged at `Error`; an
invalid `Sync:IntervalMinutes` stops the start with `InstallationSettingException`
before any endpoint exists to answer. No new error code, no new error body, no
change to the API-6 shape.

## 8. Compatibility

No contract changes, so nothing to version and nothing for a client to adapt to.
The one behavioural change — readiness reporting `Unhealthy` when the
synchronization service is not running — is what DC-11 has required since US-005 and
was unimplementable until this Story; an operator polling readiness sees a state
that was already documented.

## 9. Open questions

None raised by this stage. All seven Open Decisions are resolved (open-decisions
artifact v1) and none leaves an API question open. DB_DESIGN owns what this stage
deliberately does not: the `SyncState` table, its singleton and status constraints,
the bound on the stored error message, and the `AddSyncState` migration
(Specification FR-015, VR-002).
