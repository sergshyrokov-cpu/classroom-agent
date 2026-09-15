# Workflow Stages (Explanatory)

> **Normative workflow definition:** `docs/workflow/stage-map.yaml`
>
> This document is explanatory and **non-normative**. If it ever disagrees with
> `stage-map.yaml`, `stage-map.yaml` wins. Do not define alternative stage
> identifiers here.

The story-delivery workflow moves one active User Story from a written Story to
a committed, security-reviewed implementation. Automated stages are executed by
exactly one Skill. Human gates stop the workflow until a person records a
decision with `/so:approve` or `/so:reject`.

This project runs the **lightweight variant** of the harness: 6 automated
stages, 2 human gates, 1 terminal stage. See the SCOPE NOTE in
`stage-map.yaml` for the full list of stages that were dropped and why.

## Stage sequence

| # | Stage | Type | Owner | Produces (registry keys) |
|---|---|---|---|---|
| 1 | `SPECIFICATION` | skill | `spec-writer` | `specification`, `open_decisions` |
| 2 | `HUMAN_SPEC_APPROVAL` | human gate | human | — |
| 3 | `API_DESIGN` | skill (optional) | `openapi-designer` | `api_design`, `openapi` |
| 4 | `DB_DESIGN` | skill (optional) | `db-designer` | `database_design`, `entity_model` |
| 5 | `TEST_WRITING` | skill | `test-writer` | `test_strategy`, `ac_test_matrix`, `test_generation_report` |
| 6 | `IMPLEMENTATION` | skill | `dotnet-implementor` | `implementation_report` |
| 7 | `SECURITY_REVIEW` | skill | `security-reviewer` | `security_review` |
| 8 | `HUMAN_PR_APPROVAL` | human gate | human | — |
| 9 | `COMPLETED` | terminal | `story-orchestrator` | — |

The Story itself is **not** produced by the workflow: a human writes it into
`docs/stories/` and registers it in `docs/catalog/stories.yaml`. There is no
backlog-sync stage and no GitHub-Issue source.

## What the two gates carry

- **`HUMAN_SPEC_APPROVAL`** — the heaviest gate. Because there is no automated
  spec reviewer and no separate design review, this is where a person checks
  that the Specification actually reflects `trebovaniya.md` and that any Open
  Decisions are resolved. Everything downstream trusts it.
- **`HUMAN_PR_APPROVAL`** — the person reads `implementation_report` and
  `security_review` and approves; the final commit follows at once, made by the
  person or by an agent on their explicit request. Despite the name there is no
  Pull Request in this project: commits land on `master`.
- **`COMPLETED`** — terminal. There is no ARCHIVED stage and no archive mode.

## Where the dropped stages' work went

| Dropped stage | Who covers it now |
|---|---|
| `CLARIFICATION` | `trebovaniya.md` (frozen requirements, v12); its section 7 lists what is still open |
| `SPEC_REVIEW` | `HUMAN_SPEC_APPROVAL` |
| `DESIGN_REVIEW` | `HUMAN_SPEC_APPROVAL` covers the spec; designs are checked by the person at `HUMAN_PR_APPROVAL` |
| `IMPACT_ANALYSIS` | the Story scope — the codebase is new |
| `IMPLEMENTATION_PLANNING` / `PLAN_REVIEW` | the approved Specification plus the design artifacts **are** the plan |
| `IMPLEMENTATION_VERIFICATION` | the tests, written at `TEST_WRITING` before implementation |
| `RECONCILIATION` | single author; drift is visible directly in the diff |
| `PR_PREPARATION` / `READY_FOR_PR` | no PR flow |
| `ARCHIVED` | `COMPLETED` is terminal |

## Loop-backs

A stage can send the Story back to an earlier stage when it finds a correctable
problem (`verdict: CHANGES_REQUIRED`). The allowed targets per stage are defined
in `stage-map.yaml` under each stage's `loop_back:` map. A Skill may only name a
loop-back key that exists there.

Because the review stages are gone, most loop-backs now target `SPECIFICATION`,
`API_DESIGN` or `DB_DESIGN` directly: if a downstream stage cannot proceed
without guessing, the gap is in the Specification or the design, and that is
where it gets fixed.

## Retired identifiers

`CLARIFY`, `SPEC_WRITE`, `DESIGN`, `PLANNING`, `TESTING`, `VERIFICATION`,
`IMPLEMENTATION_VERIFY`, `HUMAN_REVIEW`, `PULL_REQUEST`, `DONE` are **retired**,
as are the identifiers of every stage dropped in this variant. See
`stage-map.yaml` `retired_identifiers` for the full mapping.
