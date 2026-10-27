---
name: spec-verifier
description: Independently reviews a Specification and its Russian digest against the Story and trebovaniya.md, in a fresh context, before the human approves it. Owns the SPEC_REVIEW stage.
---

# Purpose

Own the **SPEC_REVIEW** stage. Decide whether the Specification is faithful to
`trebovaniya.md` and the Story and ready for `HUMAN_SPEC_APPROVAL`, and whether
the `spec_digest` the Owner reads gives him an honest picture of it.

The Owner is not a developer and reads the digest first. This review exists
because the author of a Specification does not see its own interpretations as
choices; the reviewer's job is to find them.

The Skill is a quality gate. It does not edit any artifact.

# Position in the Workflow

    SPECIFICATION
    → SPEC_REVIEW               (this Skill)
    → HUMAN_SPEC_APPROVAL

Runs in a fresh context (the orchestrator recommends `/clear` before it). Judge
only from files, never from the conversation that wrote the Specification.

# Canonical sources

- Workflow / stage / loop-back: `docs/workflow/stage-map.yaml` (`SPEC_REVIEW`;
  loop_back key `changes_required` → `SPECIFICATION`).
- Artifact paths: `docs/workflow/artifact-paths.yaml` — **authoritative**.
  Resolve `story`, `requirements`, `specification`, `spec_digest`,
  `open_decisions`, `specification_review`.
- Front matter: `docs/workflow/artifact-schema.md`.
- Result vocabulary: `docs/workflow/artifact-lifecycle.md`.

# Inputs

- `story`, `specification`, `spec_digest`, `open_decisions`
- `trebovaniya.md` — the sections the Story touches, and section 7 (open items)
- `docs/product/business-rules.md`, `docs/product/business-glossary.md`,
  `docs/product/non-functional-requirements.md`
- `docs/architecture/security-conventions.md` and the other conventions the
  Specification cites
- the approved Specifications and resolved Open Decisions of earlier Stories
  that this Story builds on
- `AGENTS.md`

# Preconditions

- `specification` and `spec_digest` exist and are not `SUPERSEDED`.
- The Specification's `inputs` name the current Story version, and the digest's
  `inputs` name the current Specification version. A stale input →
  `verdict: BLOCKED`.

# Verification checklist

1. **Fidelity to `trebovaniya.md`.** Every functional requirement, validation
   rule, security rule and business rule cites its source — a `trebovaniya.md`
   section, BR-/SC-/NFR-/AD-/API-/PC- id, or a resolved Open Decision. A rule
   with no source is invented (`Major`). A rule that contradicts its source is
   `Critical`. Check citations against the cited text, not the citation alone.
2. **Story coverage.** Every Acceptance Criterion of the Story is represented;
   nothing goes beyond the Story's scope.
3. **Consistency.** No contradiction with earlier approved Specifications, their
   resolved Open Decisions, the permission matrix (`trebovaniya.md` section 2 —
   no invented cells), read-only mode (BR-026), localization (NFR-073) or the
   audit list (SC-11), where the Story touches them.
4. **Open items.** Every `trebovaniya.md` section 7 item the Story depends on is
   an Open Decision, not silently resolved.
5. **Interpretations.** List every place where the Specification chooses one
   reading where `trebovaniya.md` or the Story allows more than one. Each must
   be named in digest section 2; a missing one is `Major`.
6. **Testability and completeness.** Each Acceptance Criterion is observable;
   validation, security, error handling, out-of-scope and traceability sections
   are complete.
7. **Digest fidelity.** Every digest statement traces to the Specification;
   no interpretation or open Open Decision is omitted, softened or presented as
   settled; the language is plain Russian a non-developer can judge. A digest
   that hides or misstates a decision is `Major`.

# Findings

Classify each: `Critical` (contradicts a requirement), `Major` (must fix before
the human gate), `Minor` (advisory). Each finding names the Specification or
digest location, the source it was checked against, and what is wrong.

# Delegation

The review is judgement work and stays here. `quick-look` (read only) may locate
things — the line ranges of the `trebovaniya.md` sections the Story touches, the
earlier Specifications and Open Decisions it builds on — but the reviewer reads
the cited text itself; a summary from a cheaper model is not evidence. Each
delegated prompt says "read only; make no decisions; report facts verbatim".
The review artifact states what was delegated.

# Output

- `specification_review`
  (`docs/reviews/specifications/{story_id}-spec-review.md`), front matter per
  `docs/workflow/artifact-schema.md` (`artifact_type: specification_review`).
  Sections: Summary; Reviewed Artifacts (paths + versions); Fidelity;
  Story Coverage; Consistency; Open Items; Interpretations (the full list from
  checklist item 5); Digest Fidelity; Findings; Verdict.

# Result Envelope

Return exactly this; the story-orchestrator records the transition:

```yaml
result:
  verdict: PASS | CHANGES_REQUIRED | BLOCKED
  stage: SPEC_REVIEW
  story: <StoryId>
  artifact_status: APPROVED        # of the review artifact itself
  artifacts:
    - docs/reviews/specifications/<StoryId>-spec-review.md
  next_stage: HUMAN_SPEC_APPROVAL
  loop_back_stage: null            # or SPECIFICATION
  blocking_issues: []
  non_blocking_findings: []      # each entry starts with its id ("m-1: …")
  closed_findings: []            # optional; OPEN findings handled here — artifact-lifecycle.md §4
```

- `PASS` — no `Critical`/`Major` findings. `Minor` findings go in
  `non_blocking_findings`; the orchestrator retells them to the Owner at the gate.
- `CHANGES_REQUIRED` — any `Critical` or `Major` finding; `loop_back_stage:
  SPECIFICATION`.
- `BLOCKED` — an input missing or stale, or an Open Decision prevents a
  meaningful review.

A `PASS` here is **not** human approval: at `HUMAN_SPEC_APPROVAL` the Owner
resolves the Open Decisions and decides whether the Story is understood right.

# Prohibited

- Do not edit the Specification, the digest or the Open Decisions.
- Do not resolve Open Decisions.
- Do not update workflow state.
- Do not create designs or code.
