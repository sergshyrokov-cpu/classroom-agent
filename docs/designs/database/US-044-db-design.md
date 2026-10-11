---
artifact_type: database_design
story: US-044
version: 1
status: DRAFT
created_at: 2026-10-11T07:10:42Z
updated_at: 2026-10-11T07:10:42Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/designs/api/US-044-api-design.md
    version: 1
  - path: docs/designs/api/US-044-openapi.yaml
    version: 1
  - path: docs/decisions/US-044-open-decisions.md
    version: 1
supersedes: null
---

# US-044 Database Design — Export the built-in Meet reports to Excel

## 1. Scope

The exports read only what US-033 already reads (`meet_session`,
`meet_participation`, `meeting_code_link`, `classroom_participant`,
`course_membership`, `course`, `sync_state`) through the existing queries. No
table, index or column of those changes; no new query surface (spec FR-003,
API design §3).

The only schema change is in **`audit_event`**: two new nullable columns, two
new action codes and the constraints that tie the export columns to the three
export actions (spec FR-013, SC-11 as amended at the gate). One migration,
**`AddMeetExportAudit`** (PC-2), after `20261010162545_AddMeetingCodeLinks`.

Delegated: none. The current `AuditEvent` entity, its configuration
(`Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`), the
migration list, the workbook model (`Application/Models/Export`) and
`ClosedXmlReportRenderer` were read directly.

## 2. `audit_event` — what exists

US-028 db-design §3 added six export columns for `journal_exported`:
`export_period_from date`, `export_period_to date`, `export_template_id bigint`,
`export_template_built_in boolean`, `export_rows integer`,
`export_format varchar(8)` (`'xlsx'`), all nullable, with:

- `ck_audit_event_export_columns` — `(action = 'journal_exported') = (period,
  built_in, rows, format all NOT NULL)`;
- `ck_audit_event_export_columns_absent` — any other action has all six NULL;
- `ck_audit_event_export_shape` — the journal row's shape;
- `ck_audit_event_export_template_id` — `> 0`.

`ck_audit_event_export_columns_absent` would refuse a Meet export row, so the
export constraints are rewritten for three actions.

## 3. Changes

### 3.1 Two action codes

| `AuditAction` | Code (`action`, varchar(64)) | Spec |
|---|---|---|
| `MeetReportsExported` | `meet_reports_exported` | FR-013, course workbook |
| `MeetMeetingExported` | `meet_meeting_exported` | FR-013, meeting workbook |

`ck_audit_event_action` is recreated with both codes appended. Both value
converters in `AuditEventConfiguration` gain the two arms.

### 3.2 Two columns

| Column | Type | Null | Meaning | Rule |
|---|---|---|---|---|
| `export_meeting_id` | `bigint` | yes | internal id of the exported meeting (`meet_session.id`) | bare id, **no foreign key** — audit outlives the meeting it names, as `export_template_id` and `meet_previous_course_id` (PC-9, PC-11) |
| `export_meetings` | `integer` | yes | number of meeting rows of report 2 in the course workbook | count, `>= 0` |

Existing columns reused:

| Column | `meet_reports_exported` | `meet_meeting_exported` |
|---|---|---|
| `target_type`, `target_id` | `course`, course id | `course`, course id |
| `export_period_from`, `export_period_to` | the period | NULL |
| `export_rows` | student rows of report 3 (spec I-9) | identified-person rows (spec I-9) |
| `export_meetings` | meeting rows of report 2 | NULL |
| `export_meeting_id` | NULL | the meeting id |
| `export_template_id`, `export_template_built_in` | NULL | NULL |
| `export_format` | `xlsx` | `xlsx` |

No column carries a name, email, meeting code or file content (SC-10, SC-11).
`meet_code` (US-032) stays NULL on both: the meeting code is Google data
written into the file, and the audit row must not carry it (spec FR-013).

### 3.3 Constraints

Dropped and recreated (same names where the meaning is kept):

```sql
-- every export action has its required columns; journal as before
ck_audit_event_export_columns:
  (action = 'journal_exported') = (export_period_from IS NOT NULL AND export_period_to IS NOT NULL
    AND export_template_built_in IS NOT NULL AND export_rows IS NOT NULL AND export_format IS NOT NULL)

-- no export column outside the three export actions
ck_audit_event_export_columns_absent:
  action IN ('journal_exported', 'meet_reports_exported', 'meet_meeting_exported')
  OR (export_period_from IS NULL AND export_period_to IS NULL AND export_template_id IS NULL
      AND export_template_built_in IS NULL AND export_rows IS NULL AND export_format IS NULL
      AND export_meeting_id IS NULL AND export_meetings IS NULL)

-- the journal row (US-028) additionally has neither Meet column
ck_audit_event_export_shape:
  action <> 'journal_exported' OR (actor_type = 'app_user' AND target_type = 'course'
    AND target_id IS NOT NULL AND outcome = 'succeeded' AND export_period_from <= export_period_to
    AND export_rows >= 0 AND export_format IN ('xlsx')
    AND export_template_built_in = (export_template_id IS NULL)
    AND export_meeting_id IS NULL AND export_meetings IS NULL)
```

`ck_audit_event_export_columns` keeps its text: a Meet row has
`export_template_built_in` NULL, so the right side is false, as the left. It is
listed so the migration's reader sees all four together; it is **not** changed.

New:

```sql
ck_audit_event_meet_reports_export_shape:
  action <> 'meet_reports_exported' OR (actor_type = 'app_user' AND target_type = 'course'
    AND target_id IS NOT NULL AND outcome = 'succeeded' AND refusal_category IS NULL
    AND export_period_from IS NOT NULL AND export_period_to IS NOT NULL
    AND export_period_from <= export_period_to
    AND export_rows IS NOT NULL AND export_rows >= 0
    AND export_meetings IS NOT NULL AND export_meetings >= 0
    AND export_format = 'xlsx'
    AND export_meeting_id IS NULL AND export_template_id IS NULL AND export_template_built_in IS NULL)

ck_audit_event_meet_meeting_export_shape:
  action <> 'meet_meeting_exported' OR (actor_type = 'app_user' AND target_type = 'course'
    AND target_id IS NOT NULL AND outcome = 'succeeded' AND refusal_category IS NULL
    AND export_meeting_id IS NOT NULL AND export_meeting_id > 0
    AND export_rows IS NOT NULL AND export_rows >= 0
    AND export_format = 'xlsx'
    AND export_period_from IS NULL AND export_period_to IS NULL AND export_meetings IS NULL
    AND export_template_id IS NULL AND export_template_built_in IS NULL)
```

`ck_audit_event_meet_code_absent` already forces `meet_code` and
`meet_previous_course_id` NULL for any non-link action, so both Meet export
rows are covered without change.

### 3.4 Migration `AddMeetExportAudit`

1. `AddColumn` `export_meeting_id bigint NULL`, `export_meetings integer NULL`.
2. Drop and re-add `ck_audit_event_action`, `ck_audit_event_export_columns_absent`
   and `ck_audit_event_export_shape` with the texts above.
3. Add `ck_audit_event_meet_reports_export_shape` and
   `ck_audit_event_meet_meeting_export_shape`.
4. `Down` reverses in the opposite order.

Existing rows satisfy every new constraint: journal rows have both new columns
NULL; other rows have all export columns NULL already. No data migration. No
index: the audit table stays write-only until EPIC-9 (PC-7); the purge's index
on `occurred_at` is unchanged.

`ck_audit_event_immutable` and the update/delete rules are untouched (SC-11).

## 4. Retention

The rows are purged with the rest of `audit_event` after N years counted from
their own `occurred_at` (PC-11). Deleting a course or a meeting never deletes
the trace of its export; the bare ids may then point at nothing.

## 5. Sensitive data

None stored. The ids are internal; the counts are numbers; the format is a
closed code. Nothing typed by a user reaches the row (spec VR-004).

## 6. Non-persistent model changes (no schema)

The workbook model in `Application/Models/Export` and the renderer gain opt-in
members for spec FR-007 — see entity model §3. They are not persisted; they are
listed here because spec FR-016 and API design §8 assigned their shape to this
stage.

## 7. Tests this design asks for (for TEST_WRITING)

- Each new CHECK refuses a crafted bad row (Testcontainers PostgreSQL, TC-2): a
  `meet_reports_exported` row without `export_meetings`; one with a template id;
  a `meet_meeting_exported` row with a period or without a meeting id; a
  `journal_exported` row with `export_meeting_id`; a non-export row with
  `export_meetings`.
- The migration applies on a database holding US-028 journal rows, and they
  stay valid.
- The model snapshot matches the configuration (the existing pending-model
  test).
