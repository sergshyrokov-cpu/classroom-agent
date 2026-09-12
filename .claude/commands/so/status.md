---
description: Show the current User Story workflow status.
argument-hint: ""
---

Invoke the story-orchestrator Skill in status mode.

Requirements:

- use read-only operations;
- do not invoke a stage Skill;
- do not modify workflow state or any artifact;
- resolve stage + paths from docs/workflow/stage-map.yaml and
  docs/workflow/artifact-paths.yaml;
- report workflow health, current stage inputs/outputs and their status, stale
  inputs, blockers, the pending Human Gate with its exact approval command, and
  the recommended next command.
