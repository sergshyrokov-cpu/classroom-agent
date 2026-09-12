---
name: db-designer
description: Produces the database design and entity model from an approved Specification and API design. Owns the DB_DESIGN stage.
---

# Purpose

Own the **DB_DESIGN** stage. Design the persistence model — entities, explicit
constraints, relationships — the implementation must follow.

# Canonical sources

- Workflow / stage: `docs/workflow/stage-map.yaml` (`DB_DESIGN`;
  `optional: true` when the approved Specification says the Story does not change
  persistence behavior).
- Artifact paths: `docs/workflow/artifact-paths.yaml` — **authoritative**.
  Resolve `specification`, `api_design`, `openapi`, `open_decisions`,
  `database_design`, `entity_model`.
- Front matter: `docs/workflow/artifact-schema.md`.
- Result vocabulary: `docs/workflow/artifact-lifecycle.md`.
- Conventions: `docs/architecture/persistence-conventions.md`,
  `docs/architecture/security-conventions.md`.

# Inputs (registry keys)

- `specification`, `api_design`, `openapi`, `open_decisions`
- `docs/product/business-rules.md`, `docs/product/business-glossary.md`
- `docs/architecture/persistence-conventions.md`,
  `docs/architecture/security-conventions.md`
- `AGENTS.md`

# Preconditions

- `HUMAN_SPEC_APPROVAL` recorded; `specification` current.
- `api_design` / `openapi` exist or are `NOT_APPLICABLE`.
- No blocking Open Decision affecting persistence.

If the Specification explicitly states no persistence change: emit
`verdict: NOT_APPLICABLE`, record the reason.

# Responsibilities

Per the Specification, API design, and `persistence-conventions.md`:

- entities and attributes, each traced to a business concept;
- explicit column length, nullability, uniqueness — no EF Core convention
  defaults;
- primary keys (surrogate, generated), foreign keys, indexes where required;
- relationships and cardinality;
- identifier type and generation strategy per `persistence-conventions.md`;
- audit timestamp columns per `persistence-conventions.md`;
- sensitive data (password hash, tokens, PII): storage rules, never plaintext;
- schema-initialization approach consistent with `persistence-conventions.md`
  (explicit schema design, not a `ddl-auto` shortcut).

# Outputs

Both with front matter per `docs/workflow/artifact-schema.md`.

- `database_design` (`docs/designs/database/{story_id}-db-design.md`,
  `artifact_type: database_design`): tables/entities, every constraint, indexes,
  relationships, schema-init notes, sensitive-data handling, rationale.
- `entity_model` (`docs/designs/database/{story_id}-entity-model.md`,
  `artifact_type: entity_model`): the entity/attribute model and its mapping to
  business concepts and to API DTOs.

# Result Envelope

Return exactly this; the story-orchestrator records the transition:

```yaml
result:
  verdict: PASS | BLOCKED | NOT_APPLICABLE
  stage: DB_DESIGN
  story: <StoryId>
  artifact_status: DRAFT
  artifacts:
    - docs/designs/database/<StoryId>-db-design.md
    - docs/designs/database/<StoryId>-entity-model.md
  next_stage: TEST_WRITING
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
```

- `PASS` — every persistence requirement and constraint documented; entities
  mapped to business concepts and DTOs.
- `NOT_APPLICABLE` — Specification says no persistence change; reason recorded.
- `BLOCKED` — stale/missing input, blocking Open Decision, empty
  `persistence-conventions.md`, or an upstream artifact that makes a correct
  design impossible. `DB_DESIGN` has no `loop_back` map in `stage-map.yaml`; name
  the offending upstream stage in `blocking_issues` for the human to decide. Do
  not emit `CHANGES_REQUIRED` at this stage.

# Prohibited

- Do not create entities in source, migrations, or code.
- Do not resolve Open Decisions.
- Do not change the Specification or API design.
- Do not update workflow state.
