---
artifact_type: database_design
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T05:48:00Z
updated_at: 2026-10-06T05:48:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/designs/api/US-028-api-design.md
    version: 1
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
  - path: docs/decisions/US-028-open-decisions.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/designs/database/US-027-db-design.md
    version: 1
supersedes: null
---

# US-028 Database Design — The journal-export audit row

Entity and type names: `docs/designs/database/US-028-entity-model.md`.

## 1. Decisions in one table

| Id | Decision | Source |
|---|---|---|
| D-1 | The export's details are **typed nullable columns on `audit_event`**, the US-037 pattern — no JSON, no side table | spec FR-010; US-037 db-design §2.1 |
| D-2 | New action `journal_exported`; new target type `course`, target id = the course's internal id | spec FR-010 |
| D-3 | Template recorded as `export_template_id` (created) **or** `export_template_built_in = true` (built-in) — the "marker of the built-in template" | spec FR-010 |
| D-4 | `export_format` `varchar(8)`, values `xlsx` only (US-029 adds `docx`) | spec FR-010 "the format" |
| D-5 | No new table, no change to any other table; the workbook, the orientation and the file are never stored | spec FR-002, FR-006, S-07 |
| D-6 | No new index | §5 |
| D-7 | One migration `AddJournalExportAudit` | PC-2 |

## 2. Tables touched

| Table | Change |
|---|---|
| `audit_event` | 6 nullable columns; 2 enum values; 4 new and 3 re-created check constraints |

Nothing else. `report_template`, `course`, `app_user`: read only, as by the report
page. No foreign key changes (PC-8).

## 3. `audit_event`

### 3.1 Why columns (D-1)

§5 and SC-11 put "the period, the template, the number of rows, the format" in
the audit event of an export (spec FR-010); the six SC-11 row fields do not hold
them. As US-037 decided for the purge counts: a free-text or JSON details column
is what PC-9 and SC-10 forbid (no check constraint can prove it holds no
personal datum), and a side table splits one event in two. Every new column is a
date, an id, a boolean, an integer or an enumerated code — none can carry a
name, email, grade, title or template text.

### 3.2 Columns

| Column | Type | Null | Meaning (spec FR-010) |
|---|---|---|---|
| `export_period_from` | `date` | yes | first day of the exported period (school calendar date, as entered) |
| `export_period_to` | `date` | yes | last day, inclusive |
| `export_template_id` | `bigint` | yes | id of the created template; null for the built-in. A bare id — no foreign key (PC-9: audit rows outlive what they name) |
| `export_template_built_in` | `boolean` | yes | `true` for the built-in "Academic journal", `false` for a created template |
| `export_rows` | `integer` | yes | student rows of "Grading" (spec I-9); 0 for an empty report |
| `export_format` | `varchar(8)` | yes | `xlsx` |

Nullable because every other action has none of them (US-037 §2.2: a default
would make a sign-in row claim an export). `date` rather than `timestamptz`: the
period is a pair of calendar days in the school's time zone, exactly what the
user chose; no instant is implied. `integer` is ample for one course's students.

The existing columns carry the rest:

| Column | Value |
|---|---|
| `action` | `journal_exported` |
| `actor_type` / `actor_id` / `actor_role` | `app_user` / the session's account id / `admin` or `dean` (spec VR-002) |
| `target_type` / `target_id` | `course` / the course's internal id |
| `outcome` | `succeeded` (only successful exports are audited — spec FR-005, FR-010) |
| `refusal_category` | null |
| `request_id` | the host's request id |
| `occurred_at`, `created_at`, `updated_at` | clock, as every row (PC-6, `ck_audit_event_immutable`) |

### 3.3 Enumerated codes

| Enum | New member | Code | Constraint re-created |
|---|---|---|---|
| `AuditAction` | `JournalExported` | `journal_exported` | `ck_audit_event_action` |
| `AuditTargetType` | `Course` | `course` | `ck_audit_event_target_type_value` |
| `ExportFormat` (new) | `Xlsx` | `xlsx` | `ck_audit_event_export_shape` (new) |

Codes are stored through the explicit converters of `AuditEventConfiguration`,
never as integers (existing pattern).

### 3.4 Constraints (new)

| Name | Expression | Why |
|---|---|---|
| `ck_audit_event_export_columns` | `(action = 'journal_exported') = (export_period_from IS NOT NULL AND export_period_to IS NOT NULL AND export_template_built_in IS NOT NULL AND export_rows IS NOT NULL AND export_format IS NOT NULL)` | every required detail on an export row… |
| `ck_audit_event_export_columns_absent` | `action = 'journal_exported' OR (export_period_from IS NULL AND export_period_to IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL AND export_rows IS NULL AND export_format IS NULL)` | …and none on any other row (both directions, US-037 §2.4) |
| `ck_audit_event_export_shape` | `action <> 'journal_exported' OR (actor_type = 'app_user' AND target_type = 'course' AND target_id IS NOT NULL AND outcome = 'succeeded' AND export_period_from <= export_period_to AND export_rows >= 0 AND export_format IN ('xlsx') AND export_template_built_in = (export_template_id IS NULL))` | FR-010: a user's, names the course, succeeded only, a valid period, a non-negative count, a known format, exactly one template form |
| `ck_audit_event_export_template_id` | `export_template_id IS NULL OR export_template_id > 0` | a template id is a generated positive id |

Existing constraints already force a non-null `actor_role` for `app_user` and a
null `refusal_category` for `succeeded`; not repeated.

### 3.5 Constraints (re-created with a value added)

| Name | Change |
|---|---|
| `ck_audit_event_action` | `+ 'journal_exported'` |
| `ck_audit_event_target_type_value` | `+ 'course'` |

`ck_audit_event_report_template_shape`, `…_sync_request_shape`,
`…_purge_*` are guarded by `action` and unaffected.

## 4. Migration `AddJournalExportAudit`

One migration (PC-2), generated by `dotnet ef migrations add`, never
`EnsureCreated()`:

1. `ADD COLUMN` × 6 as §3.2 (all nullable — existing rows need no value).
2. Drop and re-create `ck_audit_event_action` and
   `ck_audit_event_target_type_value` with the new values.
3. Add the four constraints of §3.4.

Every existing row satisfies the new constraints (no row has the action, all new
columns are null). `Down` reverses in the opposite order. No data migration.

## 5. Queries

- **Write**: one `INSERT` per successful export through
  `IAuditEventRepository.Add` and `IUnitOfWork.SaveChangesAsync`, committed
  before the response starts (api-design §2.8).
- **Read for the report**: unchanged from US-027 / US-042 — the export reuses
  the report's queries through the extracted reader and builder (entity model
  §3.3); a fixed number of round trips (spec §9).
- No new index (D-6): `audit_event` has no reader by action yet (EPIC-9), and the
  purge's `ix_audit_event_occurred_at` (US-037) covers export rows too.

## 6. Retention

Export rows are ordinary audit rows: deleted only by the retention purge after
N years (PC-11, US-037), never updated (PC-9). The purge needs no change.

## 7. Sensitive data

- No column can hold personal data: dates, ids, a boolean, an integer and a
  closed code (PC-9, SC-10, SC-11, spec S-06).
- The template **name** is not stored — only its id (spec FR-010).
- The file, the workbook and the orientation are never persisted (D-5, S-07).

## 8. Tests the design implies

- The migration applies to a database at the US-042 schema; existing rows stay
  valid (Testcontainers, TC-2).
- Each §3.4 constraint rejects its violating row by direct insert: export row
  missing a column; a sign-in row with an export column; `from > to`; negative
  rows; `built_in = true` with an id; `built_in = false` without an id; target
  type other than `course`; outcome `refused`.
- `AuditEvent.JournalExported` round-trips through EF Core with every column.

## 9. Traceability

| Spec | Here |
|---|---|
| FR-010, AC-007, S-06 | §3 |
| FR-009, AC-006 (audit in read-only mode) | §5 write path — no guard |
| FR-006, S-07 | D-5, §7 |
| PC-2 | §4 |
