---
artifact_type: database_design
story: US-007
version: 1
status: DRAFT
created_at: 2026-09-19T12:29:42Z
updated_at: 2026-09-19T12:29:42Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 2
  - path: docs/designs/api/US-007-api-design.md
    version: 1
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-007 Database Design — Read-only mode enforcement

**Verdict: NOT_APPLICABLE.** This Story changes no persistence structure: no
table, no column, no key, no index, no relationship, no migration.

**There is deliberately no `docs/designs/database/US-007-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact: no entity is
added or altered, so there is no entity-to-business-concept mapping to state and
no DTO to map to (the `api_design` of this Story is itself `NOT_APPLICABLE` and
defines no DTO). Downstream stages that list `entity_model` among their inputs
read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when "the approved Specification
explicitly states the Story does not change persistence behavior". The approved
Specification (v2) states it twice, in terms:

- **Section 1** — "It adds no endpoint, no screen, no entity and no migration."
- **Section 9** — "**No migration, no schema change.** This Story adds no entity
  and no column (PC-2 has nothing to require here)."

Every responsibility of this stage is therefore empty for US-007:

| Stage responsibility | US-007 |
|---|---|
| Entities and attributes | none added, none altered |
| Column length, nullability, uniqueness | no column touched |
| Primary keys, foreign keys, indexes | none |
| Relationships and cardinality | none |
| Identifier type and generation | unchanged (US-005 `LegitimacyState`) |
| Audit timestamp columns | unchanged; `TimestampInterceptor` untouched |
| Sensitive data storage | none introduced; see section 3 |
| Schema initialization | unchanged; no migration in this Story |

## 2. The persistence-adjacent change, recorded so it is not missed

The Story touches the **commit path** without touching the **schema**, and that
distinction is the reason this stage can be `NOT_APPLICABLE` while the Story still
matters to persistence:

- Specification FR-006 adds `ReadOnlyModeUnitOfWork`, a decorator of the
  `IUnitOfWork` **port** registered in front of the `Infrastructure`
  implementation. In read-only mode it refuses a commit that no use case declared
  a permitted service write, so `SaveChangesAsync` is never reached and nothing is
  written.
- The decorator lives in `ClassroomAgent.Application`. It holds no `DbContext`,
  no entity type and no persistence knowledge — it wraps the port (AD-3, AD-6,
  AD-7). Specification I-7 is explicit that inspecting the staged entities was
  rejected precisely because it would need the `DbContext` and would move the rule
  into `Infrastructure`.
- The transaction-boundary policy of AD-7 is unchanged: repositories still stage
  and never save; the owning use case still commits. What changes is that a commit
  can now be refused before it starts.
- `Infrastructure.Persistence.UnitOfWork` itself is **not** modified. It stays
  registered as the inner instance (Specification FR-011).

Implication for `TEST_WRITING` and `IMPLEMENTATION`: the behaviour to prove here
is that **nothing is committed** on a refusal and that the permitted write
(`LegitimacyState`, US-005) still commits — which needs a real PostgreSQL
integration test (TC-2, Testcontainers), not a schema assertion.

## 3. Reads and data this Story touches

- `LegitimacyState` — the single row of US-005 — is **read** by
  `GetLegitimacyModeQuery` through `ILegitimacyStateRepository.GetForReadAsync`
  (untracked) each time the guard or the backstop runs (Specification FR-001,
  I-5). No new query shape, no new index: it is a one-row table.
- No personal data is read, written or logged by this Story. The refusal carries
  an operation constant and a reason enum only (Specification S-03, SC-10).
- No `AuditEvent` row is written — a refused write is not an audited action
  (SC-11, Specification S-04), and the table itself arrives with US-008.
- The retention purge and its audit event exist as a member of the permitted list
  (`PermittedServiceWrite.RetentionPurge`) but have no use case and no persistence
  work in this Story (EPIC-10, PC-11).

## 4. Schema stability check

Confirmed unchanged by this Story:

- the installation database and its migrations — the last is
  `InitialLegitimacyState` (US-005); this Story adds none, and PC-2 ("every entity
  change ships with its EF Core migration in the same Story") has nothing to
  require;
- the Control Plane database and its five migrations, the last being
  `AddInstallationPushAddress` (US-006) — untouched: US-007 is entirely on the
  installation side;
- `ClassroomAgentDbContext`, its entity configurations and `TimestampInterceptor`;
- `EnsureCreated()` is not used anywhere, as before.

## 5. Open questions

None raised by this stage. OD-001 and OD-002 are both resolved (open-decisions
artifact v2); neither leaves a persistence question open.
