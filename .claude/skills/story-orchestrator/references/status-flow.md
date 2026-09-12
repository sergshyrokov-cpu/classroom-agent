# Status Flow

Report the current workflow state. **Read-only.** No Skill invocation, no state
change, no artifact generation, no write-capable GitHub calls.

## Inputs

- `docs/workflow/active-story.yaml`, `docs/workflow/workflow-state.yaml`
- `docs/workflow/stage-map.yaml`, `docs/workflow/artifact-paths.yaml`
- `docs/workflow/artifact-lifecycle.md`
- current stage output + relevant review artifact (read only)
- `docs/workflow/history.jsonl` (tail)
- Git status

## Report

- active Story id and catalog `state`;
- workflow `status`;
- `current_stage`, `previous_stage`, `last_completed_stage`, `attempt`;
- `last_invoked_skill`, `last_result`;
- for `current_stage`: resolved input paths (+ versions) and output paths;
  whether each exists and its `status`;
- stale inputs (downstream recorded an older upstream version);
- pending Open Decisions; blocking `TODO`/`TBD` markers in APPROVED artifacts;
- `pending_human_gate` (stage, status, required artifacts, exact command);
- blocking issue count; carried non-blocking findings;
- current branch; unrelated working-tree changes;
- next stage (from `stage-map.yaml`) and recommended command.

## Health value

One of: `HEALTHY`, `WAITING_FOR_HUMAN`, `BLOCKED`, `INCONSISTENT`, `IDLE`,
`COMPLETED`.

`INCONSISTENT` when: state files disagree; `current_stage` not in
`stage_order`; multiple current artifacts for one type; a downstream artifact
references a stale upstream version; recorded state contradicts the artifacts on
disk.

## Result

Concise console report. No durable file unless explicitly requested. Suggest one
of: `/so:next`, `/so:start <StoryId>`, `/so:approve`, `/so:reject`,
or a specific blocker resolution.
