---
artifact_type: api_design
story: US-017
version: 1
status: DRAFT
created_at: 2026-10-03T15:02:09Z
updated_at: 2026-10-03T15:02:09Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/decisions/US-017-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 80
supersedes: null
---

# US-017 API Design — Retry, backoff and permission-error handling

**Verdict: PASS.** Contract: `docs/designs/api/US-017-openapi.yaml`.

## 1. Why a contract and not NOT_APPLICABLE

US-013 … US-015 recorded NOT_APPLICABLE because they had no HTTP surface at all.
US-017 has none of its own either — no new path, method, status code, policy or
antiforgery rule (spec FR-008, FR-013) — but it **changes the response of an
existing operation**: US-009's `getWorkspaceConnectionSettings` documents its
view model (`x-view-model: WorkspaceConnectionPageModel`), and that model gains
the "Last synchronization" block. A documented view model that silently gains a
property is a contract change, so it is recorded here as a delta.

## 2. The operation

`GET /settings/workspace-connection` — unchanged in path, policy
(`ConfigureWorkspaceConnection`, Admin), status codes (`200` / `302` / `403`)
and read-only behaviour. Its view model gains `lastSynchronization`
(`LastSynchronizationView`).

The block belongs to the **page model**, not to the GET handler, so every
response that renders the page — the GET and the re-rendered page after a
rejected US-009 save, both built by the controller's one page-building method —
shows it. Building it in one place prevents either rendering from forgetting
it. "Check access" (US-011) has its own page and is not changed.

## 3. `LastSynchronizationView`

| Property | Meaning |
|---|---|
| `status` | `NeverRun` (no `SyncState` row), `Running`, `Completed`, `Failed` |
| `startedAt` | start of the last run |
| `finishedAt` | end of the last run (null while running) |
| `lastSuccessfulRunAt` | end of the last completed run, or null |
| `diagnosis` | for `Failed` only: one code of `SyncDiagnosis`; unknown stored values → `Unexpected` |

UTC instants in the DTO; the Razor view formats them in the request culture
(US-039 FR-009). `Application` holds no user-visible string: the diagnosis
travels as a code and the view maps it to a translation key (NFR-073, AD-6
precedent of `readOnlyReasonKey`).

Translation keys: the six configuration codes render
`AccessCheck.Outcome.<Code>` — the same text "Check access" shows (OD-006); new
keys only for the block's labels, the four statuses, `GoogleUnavailable` in its
synchronization sense and `Unexpected` (spec I-7). Exact key names are an
implementation detail, in both `uk` and `en`.

## 4. Auth model

Unchanged. The page is Admin-only by `ConfigureWorkspaceConnection`; the v80
permission-matrix row "Просмотр состояния синхронизации … Admin ✔, Dean ✘" is
satisfied by that policy. TC-5: an Admin test sees the block; a Dean test gets
`403` and the response contains no synchronization data.

## 5. Error model

No new error. The page has no failure of its own: a missing `SyncState` row is
`NeverRun`, an unknown diagnosis is `Unexpected`. Google's `429`/`5xx` are
handled inside synchronization and never reach any HTTP response (AD-5,
api-conventions `429` row).

## 6. Acceptance Criterion → operation

| AC | Covered by |
|---|---|
| AC-006 | `GET /settings/workspace-connection` → `200`, `lastSynchronization` |
| AC-007 | same → `403` for a Dean |
| AC-008 | same → `200` in read-only mode, block present |
| AC-013 | `diagnosis` maps unknown values to `Unexpected` |
| AC-001 … AC-005, AC-009 … AC-012, AC-014 | no HTTP surface — synchronization internals |

## 7. Open questions

None. No Open Decision affects the contract.
