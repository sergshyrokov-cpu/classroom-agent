---
artifact_type: api_design
story: US-037
version: 1
status: DRAFT
created_at: 2026-10-03T16:56:50Z
updated_at: 2026-10-03T16:56:50Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 80
supersedes: null
---

# US-037 API Design — Retention purge

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-037-openapi.yaml`.** Its absence
is this stage's recorded decision, as for US-007, US-013, US-014 and US-015.
Downstream stages that list `openapi` among their inputs read this document
instead.

## 1. Why the stage does not apply

`stage-map.yaml` makes `API_DESIGN` optional when the approved Specification
explicitly states the Story does not change public API behaviour. The approved
Specification (v1) states it:

| Where | What it states |
|---|---|
| **§7 Security, Authorization** | "no endpoint, page or user action is added; nothing is exposed anonymously (SC-4). The purge has no user trigger (OD-004)" |
| **FR-013** | The purge is a background service in the Web host, driven by `TimeProvider` |
| **§9** | "No user-visible string is added" |
| **§10 Out of Scope** | Any "last purge" screen (OD-004) |

The Story says the same (OD-004: no screen; Scope: a background job).

## 2. What the Story delivers, and why none of it is a contract

| Delivered | Why it has no API dimension |
|---|---|
| `RetentionPurgeBackgroundService` and the purge use case | Background work with no HTTP caller (AD-5) |
| Deletion of courses, leavers, participants, accounts, audit rows | Persistence — `DB_DESIGN` |
| The run's audit event with counts | An audit row; no screen reads audit in v1 (§5, Epic 9) |
| The shared purge/synchronization gate (FR-014) | In-process coordination |
| Indexes and migration (FR-017) | Persistence |

## 3. Existing contracts this Story must not change

- **Readiness and liveness on the private port** (US-005, DC-11). Checked against
  `GetReadinessQuery`: readiness depends on the synchronization *service* being
  alive (`SynchronizationServiceMemory.IsRunning`), not on a synchronization
  *run* being in progress. FR-014's gate concerns runs, so readiness states do
  not change. **Binding for the design and implementation:** a purge holding the
  shared gate must not make readiness report anything different, and the purge
  service's liveness is not added to readiness in this Story.
- **The Admin Google sign-in** (US-008). AC-006 — a deleted Admin signing in
  again — goes through the existing `CompleteGoogleSignInUseCase` path unchanged;
  the sign-in pages' responses do not change.
- **A session of a deleted account** is refused by the existing per-request
  account check (spec §7); no new status code or page.
- **The Control Plane, its push receiver and the service channel**
  (`ClassroomAgent.Contracts`) — untouched; `ContractVersion` stays 1.
- **The out-of-schedule synchronization request** (US-019 entry point,
  `SyncRunCoordinator.Request`) keeps its "always accepted" behaviour: a request
  during a purge is remembered, not refused (FR-014). When US-019 exposes it over
  HTTP, its answer does not depend on a purge in progress.

## 4. Acceptance Criterion → operation map

No Acceptance Criterion maps to an HTTP operation.

| AC | Surface |
|---|---|
| AC-001 … AC-005, AC-007, AC-014 … AC-017 | Purge use case + persistence — none |
| AC-006 | Existing sign-in path, unchanged — none new |
| AC-008 | Audit row — none |
| AC-009 | Read-only enforcement in `Application` (AD-6) — none |
| AC-010 … AC-013 | Background service, gate, logging — none |

## 5. Auth model

Unchanged. No endpoint, page, policy or route; SC-4's anonymous list does not
grow; the US-008 endpoint enumeration test gains no row.

## 6. Error model

Unchanged. Purge failures are logged (FR-012) and reach no HTTP client.

## 7. Compatibility

No contract changes. No existing page or test needs amendment on API grounds.

## 8. Open questions

None from this stage.
