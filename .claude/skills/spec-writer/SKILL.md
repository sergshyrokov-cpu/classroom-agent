---
name: spec-writer
description: Creates a complete, implementation-ready Specification from a User Story, grounded in trebovaniya.md. Owns the SPECIFICATION stage.
---

# Purpose

Own the **SPECIFICATION** stage. Produce the Specification that becomes the
primary source of truth for design, planning, testing, and implementation.

# Canonical sources

- Workflow / stage: `docs/workflow/stage-map.yaml` (`SPECIFICATION`).
- Artifact paths: `docs/workflow/artifact-paths.yaml` — **authoritative**.
  Resolve `story`, `requirements`, `open_decisions`, `specification`.
- Front matter: `docs/workflow/artifact-schema.md`.
- Result vocabulary: `docs/workflow/artifact-lifecycle.md`.

# Inputs (registry keys)

- `story`
- `requirements` — `trebovaniya.md`, the frozen requirements document. **This is
  the primary source.** There is no CLARIFICATION stage in this workflow variant;
  what a clarification report would have supplied comes from here.
- `docs/product/product-vision.md`, `docs/product/business-rules.md`,
  `docs/product/business-glossary.md`,
  `docs/product/non-functional-requirements.md`
- `AGENTS.md`

# Preconditions

- `story` exists in `docs/stories/` and `trebovaniya.md` is readable.
- Check `trebovaniya.md` section 7 ("Открытые вопросы") before writing: if the
  Story depends on a question still open there, that is an Open Decision.
- Carry every Open Decision from the Story's own "Open Decisions" section into
  the `open_decisions` artifact, keeping its `OD-` id and any recorded
  resolution. A Story Open Decision missing from the artifact is a defect.
- If unresolved Open Decisions exist: do **not** guess answers. Represent each in
  the Specification's "Open Decisions" section, write them to the
  `open_decisions` artifact (this Skill owns it in this variant), and describe
  their impact on the affected requirements. They are resolved at
  `HUMAN_SPEC_APPROVAL`.

# Specification structure

Front matter per `docs/workflow/artifact-schema.md` (`artifact_type:
specification`), then:

- Overview
- Business Goal
- Business Flow
- Functional Requirements
- Acceptance Criteria (stable ids; each traceable to the Story)
- Validation Rules (required fields, lengths, formats, allowed values, invalid
  cases — no reliance on framework defaults)
- Security Requirements (authentication, authorization, credential handling,
  data-exposure restrictions — never invented; cite `security-conventions.md` or
  an Open Decision)
- Error Handling
- Non-Functional Requirements
- Out of Scope
- Open Decisions (with impact)
- Traceability (Acceptance Criterion → functional requirement / validation rule)

# Russian digest for the human gate

The Owner is the only check of the Specification in this variant and reads it
best in Russian. Alongside the Specification, write the `spec_digest` artifact:
one page of plain Russian with no technical jargon — the one exception to the
English-artifacts rule (`AGENTS.md`, Agent Behavior). Front matter per
`docs/workflow/artifact-schema.md` (`artifact_type: spec_digest`; `inputs`
names the Specification and the version summarised). Sections:

1. **Что изменится для пользователей** — per role (Owner, Admin, Dean), what
   they will see or be able to do; three to seven points.
2. **Решения, которые нужно подтвердить** — every interpretation the
   Specification makes where `trebovaniya.md` or the Story leaves room: what was
   decided, why, and the FR-/AC- id it lives in. One or two lines each.
3. **Открытые вопросы** — every Open Decision still open, the recommended
   option marked «(рекомендую)» with one line on why.
4. **Что не входит в Story** — the Out of Scope list, one line each.

The digest summarises and never adds: every statement traces to the
Specification. Whenever the Specification is revised, the digest is rewritten
from the new version.

# Delegation

The artifact is judgement work and is written here. Lookups go to `quick-look`
(read only) from the start:

- the `trebovaniya.md` sections the Story touches, found by grep and returned
  as line ranges (never the whole ~180 KB file);
- the earlier Stories' specifications and Open Decisions that the Story builds on;
- facts about the existing code the Specification must not contradict.

Each delegated prompt names the exact files or commands, says "read only" or
which files it may touch, and "make no decisions; report facts verbatim; stop on
anything unexpected". Independent jobs run in parallel. The stage's output
artifact states what was delegated.

# Output

- `specification` (`docs/specifications/{story_id}-spec.md`), `status: DRAFT`.
- `spec_digest` (`docs/specifications/{story_id}-spec-digest.ru.md`),
  `status: DRAFT`.

# Result Envelope

Return exactly this; the story-orchestrator records the transition:

```yaml
result:
  verdict: PASS | BLOCKED
  stage: SPECIFICATION
  story: <StoryId>
  artifact_status: DRAFT
  artifacts:
    - docs/specifications/<StoryId>-spec.md
    - docs/specifications/<StoryId>-spec-digest.ru.md
  next_stage: SPEC_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
```

- `PASS` — all Acceptance Criteria represented; validation, security, error
  handling, and traceability sections complete; Open Decisions listed with
  impact; the `spec_digest` covers every interpretation and every open Open
  Decision of this Specification version.
- `BLOCKED` — the Story is missing or unreadable, `trebovaniya.md` is
  unavailable, or an Open Decision makes a mandatory requirement impossible to
  state even as a documented gap.

# Prohibited

- Do not invent security or business behavior.
- Do not resolve Open Decisions.
- Do not create designs, tests, or code.
- Do not update workflow state.
