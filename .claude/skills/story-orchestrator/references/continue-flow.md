# Continue Flow

Advance the active Story by **at most one** workflow stage. Primary mode
(also invoked as `/so:next`).

## Inputs

- `docs/workflow/active-story.yaml`
- `docs/workflow/workflow-state.yaml`
- `docs/workflow/stage-map.yaml`      (workflow + routing authority)
- `docs/workflow/artifact-paths.yaml` (path authority)
- `docs/workflow/artifact-lifecycle.md`, `docs/workflow/state-schema.md`
- `AGENTS.md`

## Algorithm

### 1. Resolve active Story
Read `active-story.yaml`. Confirm exactly one `active_story` and that
`workflow-state.yaml.story` matches. If none: stop with
`BLOCKED — run /so:start <StoryId>`.

### 2. Resolve current stage
Read `current_stage`, `status`, `attempt`, `pending_human_gate`.
`current_stage` must be in `stage-map.yaml` `stage_order`; if not, stop
`INCONSISTENT`.

### 3. If current stage is a human gate (`type: human_gate`)
Do not invoke a Skill. If `pending_human_gate.status`:
- `PENDING` → re-report the gate (artifacts + versions, automated verdict,
  blocking findings, `/so:approve` | `/so:reject`) and stop.
- `APPROVED` → advance `current_stage` to the gate's `on_approve`, clear
  `pending_human_gate`, set `status` appropriately, append history, stop.
- `REJECTED` → route to the gate's `on_reject`, clear `pending_human_gate`,
  append history, stop.
If `pending_human_gate` is null, build it (see SKILL.md Human Gates) and stop
`WAITING_FOR_HUMAN`.

### 4. Validate workflow invariants
See SKILL.md "Workflow-Level Invariants". On failure: hold; report; recommend
the earliest responsible stage. Do not route.

### 5. Check existing stage output
Resolve `stages.<current>.outputs` via `artifact-paths.yaml`. If a current,
valid output already exists (right `story`, `status` not
`SUPERSEDED`, inputs not stale, no open `CHANGES_REQUIRED`/`BLOCKED`
record): do not regenerate — validate and go to step 7 using its recorded
verdict. Otherwise continue.

### 6. Invoke the one responsible Skill
`skill := stages.<current>.skill`. Confirm it exists. Invoke it with the Story
id, the canonical stage, and resolved input paths. Wait. Read the result
envelope; inspect produced artifacts at their registry paths.

### 7. Apply the result
Per SKILL.md "The Stage Result Envelope":
- `PASS` / `NOT_APPLICABLE` → `current_stage := stages.<current>.next`.
- `CHANGES_REQUIRED` → verify `loop_back_stage` ∈ `stages.<current>.loop_back`
  values; route there; `attempt += 1` if that stage was attempted before.
  If the key is invalid → hold `BLOCKED`, human decision.
- `BLOCKED` → keep stage; `status: BLOCKED`; surface `blocking_issues`.

### 8. Record the transition
Update `workflow-state.yaml` per `state-schema.md`. Append one `history.jsonl`
event. Stop.

## Continue Result

Return: active Story; processed stage; routed Skill; verdict; produced
artifact(s) with versions; transition; ending stage; workflow status; human
gate (if any) with the exact approval command; blocking issues; recommended
next command (normally `/so:next`, or `/so:approve` at a gate).
