---
description: Advance the active User Story by one workflow stage.
argument-hint: ""
---

Invoke the story-orchestrator Skill in continue mode.

Requirements:

- process only the active User Story;
- perform at most one workflow transition;
- invoke at most one stage Skill;
- resolve stage routing from docs/workflow/stage-map.yaml and artifact paths
  from docs/workflow/artifact-paths.yaml;
- at a human gate (HUMAN_SPEC_APPROVAL, HUMAN_PR_APPROVAL):
  invoke no Skill, set WAITING_FOR_HUMAN, list the
  artifacts to review with versions and the automated verdict, and report that
  /so:approve or /so:reject records the decision;
- respect all Human Gates and configured hooks;
- record the transition in docs/workflow/workflow-state.yaml and append
  docs/workflow/history.jsonl;
- finish with the Orchestration Result;
- do not recursively continue to another stage.
