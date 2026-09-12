---
description: Activate a User Story and initialize its workflow.
argument-hint: <StoryId>
---

Invoke the story-orchestrator Skill in start mode.

Requested Story:

$ARGUMENTS

Requirements:

- activate only the explicitly requested Story;
- do not replace another active Story;
- initialize docs/workflow/active-story.yaml and docs/workflow/workflow-state.yaml
  per docs/workflow/state-schema.md;
- set the Story's catalog state to IN_PROGRESS in docs/catalog/stories.yaml;
- append an activation event to docs/workflow/history.jsonl;
- do not start stage execution automatically;
- finish with the Start Result and recommend /so:next.
