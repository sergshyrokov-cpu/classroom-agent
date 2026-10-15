---
artifact_type: database_design
story: US-051
version: 1
status: DRAFT
created_at: 2026-10-15T20:45:00Z
updated_at: 2026-10-15T20:45:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/designs/api/US-051-api-design.md
    version: 1
  - path: docs/decisions/US-051-open-decisions.md
    version: 1
supersedes: null
---

# US-051 Database Design — The Google sign-in admits only Admin accounts

**Verdict: PASS.** One persistence change: the audit table's closed list of
refusal categories gains one value (spec FR-008). No table, column, index or
other constraint changes; no data is migrated.

**There is deliberately no `docs/designs/database/US-051-entity-model.md`.** No
entity gains or loses an attribute; the only model change is one enum member,
specified in §2 below.

## 1. What changes

| Object | Change | Source |
|---|---|---|
| `audit_event.refusal_category` | new allowed code `not_an_admin_account` | spec FR-003, FR-008; `trebovaniya.md` §5 v96; SC-11 |
| `ck_audit_event_refusal_category_value` | dropped and re-added with the new code appended | spec FR-008, PC-2 |

The column itself is unchanged: `character varying(32)`, nullable, governed by
`ck_audit_event_refusal_category` (`(outcome = 'refused') = (refusal_category IS
NOT NULL)`). The new code is 20 characters, within the existing length.

## 2. Domain and mapping

- `ClassroomAgent.Domain.Enums.AuditRefusalCategory` gains one member,
  **`NotAnAdminAccount`**, appended after `LockedOut`. The enum is stored as a
  string code, not as its ordinal, so the position does not affect stored rows.
- `AuditEventConfiguration.RefusalCategoryCode` /
  `RefusalCategoryFromCode` gain the pair
  `AuditRefusalCategory.NotAnAdminAccount ⇄ "not_an_admin_account"`. An unknown
  code still throws on read, as today.
- The row is built by the existing factory `AuditEvent.AdminSignInRefused(appUserId,
  role, category, occurredAt, requestId)`; no new factory. Its row shape:

| `actor_type` / `actor_id` / `actor_role` | `action` | `target_type` / `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|
| `app_user` / the Dean account / `dean` | `admin_sign_in` | null / null (as every `admin_sign_in` row today) | `refused` | `not_an_admin_account` |

`ck_audit_event_actor_id`, `ck_audit_event_actor_role` and
`ck_audit_event_actor_role_value` already accept this shape (spec FR-003, I-5).
No email or name column exists to fill (SC-10, SC-11).

## 3. The constraint after the change

```sql
refusal_category IS NULL OR refusal_category IN (
  'not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation',
  'callback_failed', 'account_disabled', 'domain_mismatch',
  'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode',
  'connection_not_usable', 'unknown_login', 'wrong_password', 'locked_out',
  'not_an_admin_account')
```

The name `ck_audit_event_refusal_category_value` is kept. The existing order of
the codes is kept, the new one is appended, so the constraint text in the
configuration, the migration and the model snapshot differs only by the new
value.

## 4. The migration

**Name:** `AddNotAnAdminAccountRefusal`. Generated with the command in
`AGENTS.md` after the configuration change, so the model snapshot is updated in
the same migration (PC-2).

- `Up`: drop `ck_audit_event_refusal_category_value`; add it with the list of §3.
- `Down`: drop it; add it with the previous list (without
  `not_an_admin_account`). `Down` fails if a row with the new code exists — the
  correct outcome, because audit rows are never updated or deleted outside the
  retention purge (SC-11, PC-11).

No `EnsureCreated()`, no data step, no index.

## 5. Existing rows and retention

Existing rows all carry codes of the old list, so the new constraint accepts
them; the migration rewrites nothing. `ck_audit_event_immutable` is untouched;
the new rows are purged by their own timestamp like every other installation
audit row (PC-11).

## 6. Tests that follow from this design (for TEST_WRITING)

- AC-008: against PostgreSQL (Testcontainers, TC-2), a row with
  `not_an_admin_account` is accepted; a row with an unknown code is still
  rejected by `ck_audit_event_refusal_category_value`.
- The code round-trips: written as `NotAnAdminAccount`, read back as the same
  member.

## 7. Delegation

Nothing was delegated. Read directly: spec v1 FR-003, FR-008, I-5;
`AuditEventConfiguration.cs` (constraint list, `HasMaxLength(32)`, the code
mapping); `AuditEvent.AdminSignInRefused`; the model snapshot's
`refusal_category` column; the US-012 db-design precedent for adding a category;
the latest migration `20261015091505_AddParticipantErasure`.
