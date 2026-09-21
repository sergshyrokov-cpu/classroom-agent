---
artifact_type: database_design
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-20T20:05:21Z
updated_at: 2026-09-20T20:05:21Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-010-spec.md
    version: 1
  - path: docs/decisions/US-010-open-decisions.md
    version: 1
  - path: docs/designs/api/US-010-api-design.md
    version: 1
  - path: docs/designs/api/US-010-openapi.yaml
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-010 Database Design — Connection instructions for the school super-admin

**Verdict: NOT_APPLICABLE.** This Story changes no persistence structure: no
table, no column, no key, no index, no constraint, no relationship, no migration.
It reads `legitimacy_state`, which US-005 created, and writes nothing at all.

**There is deliberately no `docs/designs/database/US-010-entity-model.md`**, as in
US-007: no entity is added or altered, so there is no entity-to-business-concept
mapping to state. The Story *does* have a DTO, so the column-to-field trace that
would have lived there is in §3 of this document instead. Downstream stages that
list `entity_model` among their inputs read this document.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when "the approved Specification
explicitly states the Story does not change persistence behavior". The approved
Specification (v1) states it in terms, in its own numbered requirement:

- **FR-015 Persistence** — "**This Story changes no schema.** It adds no table, no
  column, no constraint and therefore **no EF Core migration** — it reads
  `legitimacy_state`, which US-005 created. A migration appearing in this Story is
  a defect (PC-2). The Control Plane database gains nothing (AD-1)."
- **FR-010** — rendering writes nothing: no audit row, no table, in any mode.
- The `api_design` (§8) asked this stage to record the conclusion in writing rather
  than let the Story reach `IMPLEMENTATION` with the statement only in prose.

This stage verified the claim independently against the existing schema rather
than taking it from the Specification; §2 is that check.

Every responsibility of this stage is therefore empty for US-010:

| Stage responsibility | US-010 |
|---|---|
| Entities and attributes | none added, none altered |
| Column length, nullability, uniqueness | no column touched |
| Primary keys, foreign keys, indexes | none; §4 explains why no index is needed |
| Relationships and cardinality | none |
| Identifier type and generation | unchanged (US-005 `LegitimacyState`) |
| Audit timestamp columns | unchanged; `TimestampInterceptor` untouched |
| Sensitive data storage | none introduced; §5 |
| Schema initialization | unchanged; no migration in this Story |

## 2. The independent check: every value the page needs already exists

The page needs two stored values — the school's domain and the service account's
client ID (spec FR-003). Both are columns of `legitimacy_state` as US-005 built
it, verified in `LegitimacyStateConfiguration`:

| Value | Column | Type / length | Null? | Constraint |
|---|---|---|---|---|
| the school's Workspace domain | `domain` | `varchar(253)` | `NOT NULL` | `ck_legitimacy_state_domain_length`: `char_length(domain) BETWEEN 3 AND 253` |
| the service account's client ID | `client_id` | `varchar(32)` | `NOT NULL` | `ck_legitimacy_state_client_id_format`: `client_id ~ '^[0-9]{10,32}$'` |
| whether a check ever succeeded | `last_successful_check_at` | `timestamptz` | nullable — null until a check succeeds | — |

Nothing else is required, so nothing is added. `AuditTargetType` and
`AuditAction` gain **no member**, which matters: a new member would have forced a
migration amending `ck_audit_event_action` or
`ck_audit_event_target_type_value`, exactly as US-009 had to. Because this Story
audits nothing (spec I-6), those constraints and their enumerated value lists stay
as US-009 left them.

### 2.1 A finding for TEST_WRITING: one state combination is unreachable in the database

Specification FR-002 defines `InstallationNotConfirmed` as "no check has ever
succeeded, **or** the stored domain or client ID is empty", and the `api_design`
(§2.4) tabulates the combination `InstallationNotConfirmed` + `readOnly = false`
as defensive. This stage can now say something stronger, from the constraints
above:

**The "empty stored value" branch cannot be produced in the database.** A row of
`legitimacy_state` physically cannot hold an empty `domain` (the length check
requires at least 3 characters) or an empty `client_id` (the format check requires
10–32 digits), and neither column is nullable. So through the migrated schema the
only cause of `InstallationNotConfirmed` is **no row at all, or a row whose
`last_successful_check_at` is null** — and both of those are read-only causes
(BR-025), which makes `readOnly = false` unreachable with them.

Consequences, and this is the actionable part:

- an integration test against real PostgreSQL (TC-2) **cannot construct** the
  fourth combination; attempting it by inserting an empty value fails on a check
  constraint, not on the code under test;
- the branch must therefore be proven by a **unit test with the repository port
  substituted**, returning a state whose domain or client ID is empty — the same
  shape of limitation US-009 recorded for its `DomainNotConfirmed` branch;
- the branch must nevertheless **exist** in the code: the guard is against a
  hand-edited database or a future migration that relaxes a constraint, and spec
  FR-002 requires the page not to crash. Deleting it because "the database
  prevents it" would be relying on a constraint to carry an application rule,
  which this project does the other way round (US-009 db-design: a constraint is a
  last line of defence, never the control).

### 2.2 The client ID's shape is enforced, and that supports spec I-9

Spec I-9 declines to impose a format rule on the client ID at render time, on the
ground that it is the Control Plane's value and refusing to show it could only
harm the school. That reasoning is sound and is now reinforced rather than
weakened: `ck_legitimacy_state_client_id_format` already guarantees 10–32 digits
at the moment the value is stored (US-005), so a render-time check would be a
second enforcement of a rule already proven upstream.

Implication for the view: the client ID is always a digit string, so it can be
rendered without any transformation — no formatting, no grouping, no truncation.
A super-admin must be able to paste it into the Google console exactly as shown
(spec VR-004).

## 3. The read model: column → DTO field

This replaces the entity-model mapping. Every field of
`ConnectionInstructionPageModel` (api-design §2.6) comes from one of three places,
and **none** of them is a new column:

| DTO field | Source | Kind |
|---|---|---|
| `installationDomain` | `legitimacy_state.domain`, or null when the value does not count as known | stored |
| `serviceAccountClientId` | `legitimacy_state.client_id`, or null likewise | stored |
| `state` | derived in `Application` from `last_successful_check_at` and the two values | computed, never stored (PC-3) |
| `scopes` | the immutable constant in `Domain` (spec FR-004, I-2) | code, not data — deliberately **not** a table |
| `readOnly`, `readOnlyReasonKey` | `GetLegitimacyModeQuery`, which reads the same single row (US-007) | computed |
| `notConfirmedMessageKey` | a translation key chosen from `state` | code |

Two points worth fixing here:

- **The scope list is not persisted and must not become a table.** §6 fixes it as
  a requirement; a table would make it an installation setting a school could
  edit, which spec FR-004 forbids. PC-3's rule — a value computed from fixed rules
  is not stored — applies with the same force to a value *fixed* by the
  requirements.
- **"Known" is decided in one place.** Both `installationDomain` and
  `serviceAccountClientId` are surfaced through the same rule US-009 FR-003
  already uses (a value from a check that actually **succeeded**), so an
  `upgrade_required` answer — which records both values without moving
  `last_successful_check_at` — cannot make this page look complete while the
  connection settings call the domain unknown (spec I-1).

## 4. Reads, and why no index is added

- `legitimacy_state` is read **twice per request**: once for the two values, once
  inside the read-only mode decision. Both go through
  `ILegitimacyStateRepository.GetForReadAsync` (untracked), and both are the
  singleton read the table already serves.
- **No new index.** The table holds at most one row, kept there by the shadow
  `singleton` column and its unique index `uq_legitimacy_state_singleton`. Neither
  `domain` nor `client_id` is used as a lookup key by anything (PC-7: an index is
  added for a query that needs it).
- **No tracked read.** The page loads nothing for update, so no entity is ever
  staged and the unit of work is never asked to commit — which is how spec FR-010
  and S-07 are satisfied structurally rather than by a check.
- **No new query shape**, so no `IQueryable` leaves `Infrastructure` and no
  projection is added (AD-3).

## 5. Sensitive data

- **Nothing sensitive is introduced.** The two values rendered are the two
  non-secret values §1 and BR-032 allow to cross from the Owner to a school.
- **The service-account key, the reference to it and the OAuth client secret are
  in no table in either database, and this Story adds no column that could hold
  one** (SC-7, a Hard Stop; PC-9). The reference lives in the installation's
  configuration and the key in the secret store, both placed by the Owner at
  deployment (DC-3).
- **No personal data at all.** The page carries no student, teacher, Dean or Admin
  data — not even the signed-in Admin's own address, which the landing page shows
  but this page has no reason to. PC-11's retention purge therefore gains nothing.
- **No audit row**, so no row of `audit_event` is created, updated or deleted by
  this Story (SC-11).

## 6. Schema stability check

Confirmed unchanged by this Story:

- the installation database and its **three** migrations —
  `InitialLegitimacyState` (US-005), `InitialAppUserAndAuditEvent` (US-008),
  `AddWorkspaceConnection` (US-009). US-010 adds **none**, and PC-2 ("every entity
  change ships with its EF Core migration in the same Story") has nothing to
  require. **A fourth migration appearing in this Story is a defect**, and there
  is an existing test that proves it:
  `AppUserMigrationTests` asserts `Assert.Equal(3, migrations.Count)` together
  with the three migration names and the five table names. **US-010 must leave
  that test untouched and passing** — unlike US-009, which legitimately grew both
  lists, this Story must change neither. If it starts failing, the Story has done
  something FR-015 forbids.
- the four tables of the installation database (`legitimacy_state`, `app_user`,
  `audit_event`, `workspace_connection`) and every constraint on them, including
  the `audit_event` check constraints US-009 amended;
- the Control Plane database and its migrations — untouched: US-010 is entirely on
  the installation side, and a school's instruction is never reported to the Owner
  (AD-1, spec S-06);
- `ClassroomAgentDbContext`, its entity configurations, `TimestampInterceptor`,
  and the decorated `IUnitOfWork` registration;
- `EnsureCreated()` is not used anywhere, as before (PC-2, DC-4).

## 7. Open questions

None raised by this stage. OD-001 and OD-002 are both resolved (open-decisions
artifact v1) and neither leaves a persistence question open: OD-001 concerns
translated text, OD-002 concerns the absence of a download endpoint — neither
stores anything.

`trebovaniya.md` §7 item 10, which OD-001 works around, is a Google Workspace
configuration question and has no persistence dimension: no schema would change if
it were closed tomorrow, only translation entries (OD-001's recorded
consequences).
