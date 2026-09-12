# Workflow State Schema

Authoritative schema for the two mutable workflow-state files. Field semantics
and allowed values are defined here.

- `docs/workflow/active-story.yaml` — identity + activation metadata of the
  active Story. Written by **`story-orchestrator`** (on activation; see the
  orchestrator during activation).
- `docs/workflow/workflow-state.yaml` — execution state of the active delivery
  workflow. Written by **`story-orchestrator` only**.

Stage Skills never write either file. They return a result envelope; the
orchestrator records the transition.

---

## `active-story.yaml`

```yaml
version: 1
active_story: US-001                                    # canonical Story id, or null when idle
story_path: docs/stories/US-001-register-customer.md    # resolved from artifact-paths.yaml `story`
source:
  type: github_issue | local_only                      # local_only when no GitHub source
  repository: null                                      # "owner/repo" or null
  issue_number: null                                    # integer or null
  issue_url: null                                       # string or null
activated_at: null                                      # ISO-8601 or null
activated_by: null                                      # actor string or null
status: NOT_STARTED | IN_PROGRESS | COMPLETED | ARCHIVED
```

Rules:
- Exactly one Story may be active. Multiple `active_story` values, or a value
  that disagrees with `workflow-state.yaml.story`, is an INCONSISTENT state and
  blocks `continue`.
- `source.*` stays `null` when `.mcp.json` defines no GitHub repository. Do not
  fabricate repository names or issue numbers.
- `status` here is the Story's participation status, mirrored into
  `docs/catalog/stories.yaml` atomically by the orchestrator.

---

## `workflow-state.yaml`

```yaml
version: 1
story: US-001                          # must equal active-story.yaml.active_story
workflow: story-delivery               # matches stage-map.yaml `workflow`
current_stage: SPECIFICATION           # a canonical id from stage-map.yaml stage_order
previous_stage: null                   # canonical id or null
last_completed_stage: null             # canonical id or null
status: IN_PROGRESS                    # workflow status (artifact-lifecycle.md §3)
attempt: 1                             # attempt counter for current_stage, >=1
last_invoked_skill: null               # skill name or null
last_result:                           # last stage result envelope summary, or null
  verdict: null                        # PASS | CHANGES_REQUIRED | BLOCKED | NOT_APPLICABLE
  stage: null
  recorded_at: null
last_artifacts: []                     # list of {type, path, version} produced by last stage
pending_human_gate: null               # see below; null unless current_stage is a human_gate
blocking_issues: []                    # list of strings; non-empty only when status == BLOCKED
non_blocking_findings: []              # carried-forward advisory findings
started_at: null                       # ISO-8601 when first automated stage ran
updated_at: null                       # ISO-8601 of last orchestrator write
completed_at: null                     # ISO-8601 when stage COMPLETED was reached
archived_at: null                      # ISO-8601 when stage ARCHIVED was reached
```

### `pending_human_gate` sub-object

Present (non-null) exactly when `current_stage` is a `human_gate` stage.

```yaml
pending_human_gate:
  stage: HUMAN_SPEC_APPROVAL           # the human_gate stage id
  status: PENDING | APPROVED | REJECTED
  required_artifacts:                  # copied from stage-map.yaml, with resolved paths + versions
    - type: implementation_plan
      path: docs/plans/US-001-implementation-plan.md
      version: 1
    - type: plan_review
      path: docs/reviews/plans/US-001-plan-review.md
      version: 1
  automated_verdict: PASS              # verdict of the review stage that fed this gate
  blocking_findings: []                # from that review
  requested_at: <ISO-8601>
  decided_at: null
  decided_by: null                     # actor string, set by /so:approve|/so:reject
  comment: null
```

### Transition rules

1. Only `story-orchestrator` mutates this file, and only after a stage returns a
   result or a human gate is decided.
2. Every mutation also appends one event to `docs/workflow/history.jsonl`
   (schema below). History is append-only; never rewritten.
3. `current_stage` must always be a member of `stage-map.yaml` `stage_order`.
4. On `verdict: PASS` → `current_stage := stage.next`, `attempt := 1`.
   On `verdict: CHANGES_REQUIRED` → `current_stage := stage.loop_back[key]`,
   and if the target equals the previous occurrence of that stage,
   `attempt += 1`.
   On `verdict: BLOCKED` → `current_stage` unchanged, `status := BLOCKED`.
5. Entering a `human_gate` stage sets `status := WAITING_FOR_HUMAN` and builds
   `pending_human_gate`. `/so:approve` sets it `APPROVED` and advances to
   `on_approve`. `/so:reject` sets it `REJECTED` and advances to `on_reject`.
6. The orchestrator MUST NOT infer human approval from a review Skill's `PASS`.

---

## `history.jsonl` event schema

One JSON object per line, append-only. Owned by `story-orchestrator`
(registry key `workflow_history`). Separate from `docs/hooks/tool-usage.jsonl`
(tool telemetry).

```json
{
  "timestamp": "<ISO-8601 runtime>",
  "story": "US-001",
  "from_stage": "SPECIFICATION",
  "to_stage": "HUMAN_SPEC_APPROVAL",
  "skill": "spec-writer",
  "verdict": "PASS",
  "artifacts": ["docs/specifications/US-001-spec.md"],
  "attempt": 1
}
```

`verdict` in a history event is one of the review verdicts
(`PASS` / `CHANGES_REQUIRED` / `BLOCKED` / `NOT_APPLICABLE`) or one of the
lifecycle-event markers: `ACTIVATED` (Story activation, `from_stage: null`
or `null`), `HUMAN_APPROVED` / `HUMAN_REJECTED` (a human gate, `skill: null`),
`ARCHIVED` (archive mode).
