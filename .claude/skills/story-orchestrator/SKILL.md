---
name: story-orchestrator
description: >
  Coordinates the artifact-driven delivery workflow for the active User Story.
  Reads workflow state, validates stage invariants, selects exactly one next
  workflow Skill, evaluates its result envelope, records the state transition,
  and stops at every human gate. Supports start, continue and status modes.
  Use when starting, resuming or inspecting a Story delivery flow.
argument-hint: <start|continue|status> [StoryId]
---

# Purpose

Coordinate the end-to-end story-delivery workflow for one active User Story.

The Orchestrator is a **state-machine coordinator**. It answers:

- which Story is active;
- which workflow stage is current;
- whether the current stage is complete;
- what the next permitted transition is;
- which Skill owns that transition;
- whether a review requires a loop-back;
- whether a human gate blocks progression;
- whether the Story is complete.

The Orchestrator does not perform stage work (specification, design, test
writing, implementation, security review). It routes to the responsible Skill
and records the outcome.

---

# Canonical Sources (READ, never duplicate)

| Concern | Authoritative file | Orchestrator use |
|---|---|---|
| Workflow, stage ids, ownership, transitions, loop-backs, human gates | `docs/workflow/stage-map.yaml` | the ONLY source of stage order and routing |
| Artifact locations + owners | `docs/workflow/artifact-paths.yaml` | resolve every artifact path from a registry key |
| Status vocabularies | `docs/workflow/artifact-lifecycle.md` | interpret verdicts, artifact status, workflow status |
| Workflow-state / active-story schema | `docs/workflow/state-schema.md` | shape of the two state files + `history.jsonl` |
| Artifact front matter | `docs/workflow/artifact-schema.md` | staleness / version checks |
| Project rules | `AGENTS.md` | stable behavioral constraints |

This Skill MUST NOT contain its own stage list, its own stage→skill map, or its
own artifact paths. If this document ever appears to disagree with
`stage-map.yaml` or `artifact-paths.yaml`, those files win — stop and report the
drift.

Mutable state:

- `docs/workflow/active-story.yaml` — identity of the active Story.
- `docs/workflow/workflow-state.yaml` — execution state. **Only this Skill writes it.**
- `docs/workflow/history.jsonl` — append-only transition log (registry key
  `workflow_history`). **Only this Skill appends to it.** There is no other
  workflow event log; never create `orchestrator-events.jsonl`.

---

# Supported Modes

- `start` — activate one eligible Story and initialize state. Load `references/start-flow.md`.
- `continue` (default; also invoked as `/so:next`) — advance by at most one stage. Load `references/continue-flow.md`.
- `status` — read-only report. Load `references/status-flow.md`.

Load only the reference file for the requested mode. If no mode is given, use
`continue`. There is no `archive` mode in this workflow variant — `COMPLETED`
is terminal.

---

# Single-Step Rule

One `continue` invocation performs **at most one** stage transition:

1. resolve active Story and current stage;
2. validate workflow-level invariants;
3. either invoke the one responsible Skill, or (at a human gate) stop;
4. read the Skill's result envelope;
5. record exactly one transition (or hold);
6. stop with a concise Orchestration Result.

Do not run multiple stage Skills, do not recurse, do not run the workflow to
completion in one call.

---

# The Stage Result Envelope

Every automated stage Skill returns exactly this (see
`docs/workflow/artifact-lifecycle.md`):

```yaml
result:
  verdict: PASS | CHANGES_REQUIRED | BLOCKED | NOT_APPLICABLE
  stage: <canonical-stage>
  story: <story-id>
  artifact_status: DRAFT | IN_REVIEW | APPROVED | SUPERSEDED | ARCHIVED
  artifacts: [<relative paths produced>]
  next_stage: <canonical-stage-or-null>       # advisory; orchestrator derives the real next from stage-map.yaml
  loop_back_stage: <canonical-stage-or-null>  # set only for CHANGES_REQUIRED
  blocking_issues: []
  non_blocking_findings: []
```

Routing:

- `PASS` → advance `current_stage` to `stages.<current>.next` from `stage-map.yaml`.
- `NOT_APPLICABLE` → allowed only where `stage-map.yaml` marks the stage
  `optional: true` and the Skill recorded the `optional_when` reason; then
  advance to `.next`.
- `CHANGES_REQUIRED` → the Skill's `loop_back_stage` MUST match a value in
  `stages.<current>.loop_back`. Route there; increment `attempt` if returning to
  a stage already attempted.
- `BLOCKED` → keep `current_stage`; set workflow `status: BLOCKED`; surface
  `blocking_issues`; recommend a human decision.

Never convert `CHANGES_REQUIRED` or `BLOCKED` into a successful transition.
Never manufacture a result the Skill did not return.
If the Skill produced no canonical output artifact for its stage, treat the
stage as failed (see Failure Handling) regardless of chat text.

---

# Human Gates

`stage-map.yaml` stages with `type: human_gate` (`HUMAN_SPEC_APPROVAL`,
`HUMAN_PR_APPROVAL`) are **not executable by a Skill**. `COMPLETED` is a
terminal stage reached by approving `HUMAN_PR_APPROVAL`.

When `current_stage` is a human gate:

1. Do not invoke any Skill.
2. Set workflow `status: WAITING_FOR_HUMAN`.
3. Build `pending_human_gate` in `workflow-state.yaml` per `state-schema.md`:
   - `stage`, `status: PENDING`, `requested_at` (runtime timestamp);
   - `required_artifacts`: the gate's `required_artifacts` from `stage-map.yaml`,
     each resolved to `{type, path, version}` via `artifact-paths.yaml` +
     the artifact's front matter;
   - `automated_verdict`: the verdict of the review stage that fed this gate;
   - `blocking_findings`: that review's blocking findings (should be empty to
     reach the gate).
4. Report, verbatim and exact:
   - which artifacts require human review (paths + versions);
   - the current automated verdict;
   - any blocking findings;
   - the exact command to record approval: `/so:approve` (or `/so:reject`).
5. Stop.

The Orchestrator MUST NOT infer approval from a review Skill's `PASS`, from chat
history, or from a GitHub state. Only `/so:approve` / `/so:reject` (which write
`pending_human_gate.status` and `decided_by`/`decided_at`) move a gate.

On `/so:approve`: set `pending_human_gate.status: APPROVED`, append a
`history.jsonl` event (`skill: null`, `verdict: "HUMAN_APPROVED"`), advance
`current_stage` to the gate's `on_approve`, clear `pending_human_gate`, set
`status: IN_PROGRESS` (or `COMPLETED`/`ARCHIVED` when entering those stages).

On `/so:reject`: set `status: REJECTED`, record the human `comment`, append a
`history.jsonl` event (`verdict: "HUMAN_REJECTED"`), route to the gate's
`on_reject`.

Auto Mode never bypasses a human gate.

---

# Workflow-Level Invariants

Before invoking any automated stage Skill, verify:

- exactly one active Story exists (`active-story.yaml`), and
  `workflow-state.yaml.story` names the same Story;
- `workflow-state.yaml` parses and conforms to `state-schema.md`;
- `current_stage` is a member of `stage-map.yaml` `stage_order`;
- every registry key in `stages.<current>.inputs` resolves to an artifact that
  exists and whose `status` is `APPROVED` (for artifacts gated by a review) or
  `DRAFT`/`APPROVED` (for un-reviewed inputs), and is not `SUPERSEDED`/`ARCHIVED`;
- no downstream input records a superseded upstream version (staleness);
- no unresolved **blocking** Open Decision in `open_decisions` affects the next
  stage;
- no blocking `TODO`/`TBD`/`FIXME`/`???`/`unresolved` marker in an `APPROVED`
  input artifact;
- the routed Skill exists in the environment and matches
  `stages.<current>.skill`;
- MCP capabilities the stage depends on are available (else follow the Skill's
  fallback rules or hold).

The Orchestrator performs only workflow-level validation. Each Skill runs its
own detailed preconditions.

If an invariant fails: do not route; set `status: BLOCKED` or
`WAITING_FOR_HUMAN` as appropriate; report the specific failure; recommend the
earliest responsible stage or a human action.

---

# Skill Invocation

Before invoking `stages.<current>.skill`:

1. Confirm the Skill exists and equals the stage owner.
2. Pass: the active Story id; the current canonical stage; resolved paths for
   `stages.<current>.inputs` (from `artifact-paths.yaml`); an instruction to
   stay within its documented responsibility and to return the standard result
   envelope.
3. Do not paste full artifact bodies; let the Skill load its own context.
4. Invoke no other stage Skill in the same `continue`.
5. Wait for completion. Read the result envelope and inspect the produced
   artifact(s) at their registry paths.
6. Determine exactly one transition.

There is no `BACKLOG_SYNC` stage in this variant: Stories are authored directly
in `docs/stories/` by a human and listed in `docs/catalog/stories.yaml`. The
first stage of every Story is `SPECIFICATION`.

---

# Recording a Transition

After a valid result, update `workflow-state.yaml` (per `state-schema.md`):

- `previous_stage` := old `current_stage`;
- `current_stage` := the derived next / loop-back / unchanged;
- `last_completed_stage` := old stage when verdict was `PASS`/`NOT_APPLICABLE`;
- `status` := `IN_PROGRESS` | `WAITING_FOR_HUMAN` | `BLOCKED` | `COMPLETED` | `ARCHIVED`;
- `attempt` := reset to 1 on forward move; +1 on loop-back to an attempted stage;
- `last_invoked_skill`, `last_result` (verdict/stage/recorded_at),
  `last_artifacts` ([{type, path, version}]);
- `pending_human_gate` per the Human Gates section;
- `blocking_issues`, `non_blocking_findings`;
- `started_at` (first automated stage), `updated_at` (always),
  `completed_at` / `archived_at` when reaching those stages.

Then append one line to `docs/workflow/history.jsonl`:

```json
{"timestamp":"<runtime>","story":"US-001","from_stage":"<old>","to_stage":"<new>","skill":"<skill-or-null>","verdict":"<verdict>","artifacts":[],"attempt":<n>}
```

Preserve all prior history. Never rewrite `history.jsonl`.

Do not claim a transition occurred unless the state file was successfully
written.

---

# Idempotency

Repeated invocation must be safe.

Before invoking a Skill, check whether the current stage already has a current
successful output artifact (exists, correct `story`, `status` not
`SUPERSEDED`/`ARCHIVED`, inputs not stale):

- if complete and valid → do not regenerate; validate and advance;
- if the output has an unresolved `CHANGES_REQUIRED`/`BLOCKED` record → follow
  that, do not overwrite;
- if a fresh attempt is required → increment `attempt`, preserve the prior
  artifact via `version`/`supersedes`, do not discard review history.

---

# Loop-Back Precedence

When a review returns `CHANGES_REQUIRED`, use its `loop_back_stage` (which must
be a key under `stages.<current>.loop_back` in `stage-map.yaml`).

When multiple findings imply different targets, select the **earliest**
responsible stage in `stage_order`, explain why later fixes are insufficient,
and preserve all findings. Do not pick the cheapest correction.

If a Skill names a `loop_back_stage` not present in the stage's `loop_back` map,
reject it: hold at the current stage as `BLOCKED` and require a human decision.

---

# Auto Mode Boundary

Auto Mode may: read artifacts and repo state; run non-destructive analysis;
run approved build/test commands; create Story-scoped documentation; apply
approved implementation changes via the routed Skill; invoke configured Skills;
collect diagnostics; write `workflow-state.yaml` and append `history.jsonl`.

Auto Mode must NOT: pass a human gate; resolve an Open Decision; accept security
risk; modify requirements; push to a protected branch; merge or create a Pull
Request; delete historical artifacts; expose secrets;
run destructive database operations.

Deny-and-ask rules and hooks remain authoritative. The Orchestrator must not
disable, bypass, or reconfigure hooks.

---

# Tooling Strategy

Use built-in tools for local file and repo operations.

Use IDEA MCP for project structure, semantic analysis, diagnostics, build, test
execution, and schema inspection when a stage needs them.

Use GitHub MCP only for reading source Issues, backlog synchronization, and
branch/PR metadata. Never use write-capable remote tools before the current
stage allows it.

---

# Failure Handling

- Routed Skill fails to run: do not advance; record the failed Skill and
  evidence; keep the stage; increment `attempt`; recommend retry or human help.
- Skill runs but produces no canonical output artifact at its registry path:
  treat as failed; keep state; report the missing path.
- Output artifact cannot be parsed / has no valid result envelope: treat as
  `BLOCKED`; preserve it; do not infer approval from prose.
- MCP unavailable: use the Skill's documented fallback, else hold without a
  state change.
- Hook blocks an action: respect it; do not bypass; report the guardrail; keep
  or return to the appropriate stage.
- Two active Stories / state files disagree: `status: BLOCKED`; report both
  identifiers; require human resolution; do not pick one.

---

# Observability

For every invocation, ensure a `history.jsonl` event is appended (or, for
`status` mode, none — it is read-only).

Do not log secrets: no tokens, authorization headers, passwords, password
hashes, database credentials, private keys, secret env values.

Tool-usage telemetry (`docs/hooks/tool-usage.jsonl`) is separate and not managed
by this Skill.

Telemetry is execution evidence, never requirement authority.

---

# Output Format

Every invocation finishes with a concise Orchestration Result:

```
Mode:                continue
Active Story:        US-001
Starting Stage:      SPECIFICATION
Routed Skill:        spec-writer
Verdict:             PASS
Output Artifact(s):  docs/specifications/US-001-spec.md (v1)
Transition:          SPECIFICATION → HUMAN_SPEC_APPROVAL
Ending Stage:        HUMAN_SPEC_APPROVAL
Workflow Status:     WAITING_FOR_HUMAN
Human Gate:          HUMAN_SPEC_APPROVAL — review docs/specifications/US-001-spec.md (v1)
                     and docs/reviews/specifications/US-001-spec-review.md (v1);
                     automated verdict PASS; run /so:approve or /so:reject
Blocking Issues:     none
Recommended Command: /so:approve   (after human review)
```

Paths shown must be resolved from `artifact-paths.yaml`. Do not claim a
transition unless `workflow-state.yaml` was updated.

---

# Prohibited Actions

The Orchestrator must not: perform stage work; generate code, tests, specs,
designs, or reviews; resolve Open Decisions; fabricate a successful result;
overwrite a `CHANGES_REQUIRED`/`BLOCKED` artifact; skip a mandatory stage
silently; pass a human gate; invoke multiple stage Skills per call; recurse to
completion; delete historical
artifacts; bypass hooks; weaken permissions; expose secrets; stage or commit
unrelated files; create or merge a Pull Request; mark a Story `COMPLETED`
without recorded human confirmation; treat chat history as workflow state;
embed its own stage list or artifact paths.

---

# Completion Criteria

An invocation is complete only when:

- the mode is resolved and its reference file loaded;
- the active Story and current stage are resolved from the state files;
- workflow invariants were checked against `stage-map.yaml` /
  `artifact-paths.yaml`;
- at most one responsible Skill was invoked (or a human gate stopped the flow);
- the result envelope was read and the produced artifact(s) inspected at their
  registry paths;
- exactly one transition (or hold) was determined and, when permitted, written
  to `workflow-state.yaml` with a `history.jsonl` event;
- the human-gate status is explicit;
- a concise Orchestration Result was returned.
