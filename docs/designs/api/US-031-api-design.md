---
artifact_type: api_design
story: US-031
version: 1
status: DRAFT
created_at: 2026-10-10T05:48:47Z
updated_at: 2026-10-10T05:48:47Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 87
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
supersedes: null
---

# US-031 API Design — Pull Meet events and keep history beyond 180 days

**Verdict: PASS.** Contract: `docs/designs/api/US-031-openapi.yaml`.

Delegation: none — the earlier contract (US-017) and the existing
`LastSynchronizationView` / `SchoolTimeZone` types were read directly.

## 1. Why a contract and not NOT_APPLICABLE

The Story adds no path, method, status code, policy, parameter or antiforgery
rule (spec FR-011, VR-004). As in US-017, it **changes the view model of an
existing operation**: `getWorkspaceConnectionSettings` documents
`LastSynchronizationView`, and that DTO gains two properties. A documented view
model that silently gains a property is a contract change, so it is recorded as
a delta.

## 2. The operation

`GET /settings/workspace-connection` — unchanged in path, policy
(`ConfigureWorkspaceConnection`, Admin), status codes (`200` / `302` / `403`)
and read-only behaviour. The block is built by the controller's one
page-building method (US-017 §2), so the re-rendered page of a rejected US-009
save carries the new line too.

## 3. `LastSynchronizationView` additions

| Property | Meaning | Spec |
|---|---|---|
| `meetLoadedUpTo` | the Meet watermark as a local date-time in the school's time zone; null = "not loaded yet" | FR-007, FR-011, OD-010 a |
| `failedStep` | `Classroom` or `Meet` for a `Failed` run; null otherwise and for pre-Story failed rows | FR-010, I-5 |

**Time zone in `Application`.** The conversion uses the existing
`SchoolTimeZone` (US-025 FR-010), injected into `GetLastSynchronizationQuery`;
the DTO carries the local value, so the view does no time-zone arithmetic and
holds no business rule (AD-3). The block's other times stay UTC instants with
the "UTC" suffix (OD-010 a). The new line's label states it is the school's
local time only through its wording; exact text is a translation detail.

`meetLoadedUpTo` is independent of `status`: a running or failed run leaves the
previous watermark visible, since a failed Meet step does not move it (FR-007).

Translation keys (FR-014): the watermark label, the "not loaded yet" text and the
two step names, in `uk` and `en`. The diagnosis texts are reused unchanged
(`AccessCheck.Outcome.<Code>`, `LastSync.Diagnosis.*`). Key names are an
implementation detail.

## 4. Auth model

Unchanged. Admin-only by `ConfigureWorkspaceConnection` (US-017 FR-008). TC-5:
an Admin test sees the Meet line; a Dean test gets `403` and the response holds
no synchronization or Meet data.

## 5. Error model

No new error. The page has no failure of its own: no watermark is "not loaded
yet"; no step is shown when none is stored. Google failures of the Meet step
never reach HTTP (AD-5).

## 6. Acceptance Criterion → operation

| AC | Covered by |
|---|---|
| AC-007 | `GET /settings/workspace-connection` → `200`, `failedStep = Meet` with the "Check access" diagnosis text |
| AC-009 | same → `200`, `meetLoadedUpTo` in the school's time zone, or "not loaded yet"; Dean → `403` |
| AC-014 | same → the new line and step names in `uk` and `en` |
| AC-001 … AC-006, AC-008, AC-010 … AC-013, AC-015 | no HTTP surface — synchronization, adapter and purge internals |

## 7. Open questions

None. No Open Decision affects the contract.
