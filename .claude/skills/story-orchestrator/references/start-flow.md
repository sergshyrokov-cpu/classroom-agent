# Start Flow

Activate one eligible User Story and initialize its workflow state. Explicit and
infrequent; not used during normal continuation.

## Required input

Story id, e.g. `/so:start US-001`.

## Preconditions

- no other Story is active (`active-story.yaml.active_story` is null, or the
  request names the already-active Story);
- the requested Story exists locally at the `story` registry path
  (`artifact-paths.yaml`), or can be synced (see below);
- the Story's catalog `state` in `docs/catalog/stories.yaml` is `READY` or
  `BACKLOG` (not `IN_PROGRESS` for a different active flow, not `COMPLETED`);
- no conflicting workflow lock;
- current Git branch and working tree are known.

If another Story is active: do not replace it; require explicit human resolution.

## Story source

The local `story` file under `docs/stories/` is always authoritative — there is
no `backlog-sync` stage and no GitHub-Issue source in this variant.
`active-story.yaml.source` fields stay `null`. Do not activate an ambiguous or
incomplete Story.

## Initialize state

Write `docs/workflow/active-story.yaml` and `docs/workflow/workflow-state.yaml`
per `docs/workflow/state-schema.md`:

- `active-story.yaml`: `active_story`, resolved `story_path`, `source` (nulls if
  no GitHub), `status: IN_PROGRESS`, `activated_at` (runtime), `activated_by`.
- `workflow-state.yaml`: `story`, `workflow: story-delivery`,
  `current_stage: SPECIFICATION`, `previous_stage`/`last_completed_stage`
  accordingly, `status: IN_PROGRESS`, `attempt: 1`, remaining fields
  null/empty.

Set the catalog `state` for this Story to `IN_PROGRESS` directly in
`docs/catalog/stories.yaml`.

Append a `history.jsonl` event: `from_stage: null`, `to_stage:` the initial
stage, `skill: null`, `verdict: "ACTIVATED"`.

Initial stage is always `SPECIFICATION`. Never initialize at `IMPLEMENTATION`.

## Branch policy

Follow `AGENTS.md` Git policy: there are no Story branches — work happens on
`master`. Do not create or switch branches. If the current branch is not
`master`, report it as a blocker.

## Start Result

Return: activated Story; source (Issue if any); initial stage; branch; detected
blockers; recommended next command `/so:next`.
