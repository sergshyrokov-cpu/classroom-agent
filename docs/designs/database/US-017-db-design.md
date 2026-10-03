---
artifact_type: database_design
story: US-017
version: 1
status: DRAFT
created_at: 2026-10-03T15:05:52Z
updated_at: 2026-10-03T15:05:52Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
  - path: docs/designs/api/US-017-api-design.md
    version: 1
  - path: docs/decisions/US-017-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 80
supersedes: null
---

# US-017 Database Design — Retry, backoff and permission-error handling

**Verdict: PASS with no schema change and no migration.** Deliberately not
NOT_APPLICABLE: the Story changes **what is written** to an existing column
(`sync_state.last_error`) and adds a new read of `sync_state` for the
connection page — the US-039 precedent.

## 1. Tables touched

| Table | Change |
|---|---|
| `sync_state` | none in schema; new **values** in `last_error`; `processed_count` now meaningful on failure; new read path |

No other table is read or written differently. No new table, column, index,
constraint or sequence.

## 2. `sync_state.last_error`

Stays `varchar(512)`, nullable, with its existing US-013 checks unchanged — `ck_sync_state_error_length` (1 … 512 characters) and `ck_sync_state_terminal_fields` (a failed row holds an error). No constraint on the **value list** is added.

**What is written now:** exactly one of the eight names of `SyncDiagnosis`
(spec FR-006) — `ScopeNotAuthorized`, `TechnicalAccountUnknown`,
`TechnicalAccountCannotRead`, `ApiNotEnabled`, `KeyUnavailable`, `KeyRejected`,
`GoogleUnavailable`, `Unexpected` — written by the domain method only. Never an
exception type name, never Google text. Longest is 26 characters.

**Why no value-list check constraint on the eight names.** A constraint would require a
migration and would fail on any row already holding a legacy
`RunFailed:<Type>` value; the spec chose not to migrate those (I-6) — they
display as `Unexpected` and disappear with the next successful run, which sets
`last_error` to null. The closed list is enforced by the type system instead:
`SyncState.FailRun` accepts a `SyncDiagnosis`, not a string, so no caller can
write anything else.

**Why stored as a string, not converted by EF.** An EF enum-to-string value
converter would throw when materialising a legacy row. The entity property
stays `string?`; the read path parses it (section 4).

## 3. `sync_state.processed_count` on failure

Spec I-5: a failed run records the number of courses committed before it
stopped, instead of `0`. Same column, same type; `FailRun` already takes the
count — the use case passes the real one.

## 4. The read for the connection page

An `Application` query (`GetLastSynchronizationQuery` or similar; the name is
the implementor's) reads the single `sync_state` row, if any, through the
existing installation `DbContext` in `Infrastructure` behind a port, and maps
it to `LastSynchronizationView` (API design §3):

| `sync_state` | `LastSynchronizationView` |
|---|---|
| no row | `status: NeverRun`, all times null, `diagnosis: null` |
| `status` | `Running` / `Completed` / `Failed` |
| `started_at` | `startedAt` |
| `finished_at` | `finishedAt` (null while running) |
| `last_successful_run_at` | `lastSuccessfulRunAt` |
| `last_error` when `Failed` | `diagnosis`: an **exact, case-sensitive match of a declared name** of `SyncDiagnosis`; anything else (legacy text, null, a numeric string) → `Unexpected` |

`Enum.TryParse` is **not** sufficient for the mapping: it accepts numeric
strings and, with `ignoreCase`, other spellings. Match against
`Enum.GetNames<SyncDiagnosis>()` (or an equivalent explicit table).

Read-only: `AsNoTracking`, no write, permitted in read-only mode (viewing,
BR-026). No index needed — one row.

## 5. Transactions

Unchanged. One transaction per course (US-015 FR-013); the final state write
after a stop is its own `SaveChanges`, as today. A course skipped as gone or
blank-named writes nothing. Retries happen before any transaction opens (reads
precede each course's transaction), so a retry pause never holds a database
transaction or connection open.

## 6. Sensitive data

None added. `last_error` holds a code from a closed list; no personal data, no
Google text, no exception message (SC-10, §5).

## 7. Tests the design implies

- The EF model snapshot is unchanged (no migration in this Story).
- Against PostgreSQL (TC-2): a failed run stores the code and the committed
  count; a later completed run nulls `last_error`.
- The read maps a legacy `RunFailed:HttpRequestException` row, a `"3"` row and
  a differently cased name to `Unexpected`.
