---
name: dotnet-implementor
description: >
  Implements an approved User Story in the Customer Portal ASP.NET Core
  application by following the approved Specification, API and database
  designs, Impact Analysis, Implementation Plan, tests, architecture rules,
  and security constraints. Use only after the plan is approved and required
  tests or test specifications are available.
---

# Purpose

Implement the active User Story in the Customer Portal ASP.NET Core project.

The Skill converts approved delivery artifacts into a minimal, scoped, and
reviewable set of code and configuration changes.

The Skill must follow the approved Implementation Plan.

The Skill must not redesign the Story, silently resolve Open Decisions,
introduce unrelated improvements, or reinterpret Acceptance Criteria.

The Skill produces an implementation candidate.

The Skill does not approve its own work and does not declare the Story
complete.

---

# Technology Context

The project uses:

- .NET 8 (LTS), C#
- ASP.NET Core Web API (MVC controllers)
- EF Core 8, SQLite provider
- `EFCore.NamingConventions` (snake_case column mapping)
- ASP.NET Core Cookie Authentication + custom authorization policies
- `BCrypt.Net-Next` for password hashing
- xUnit, `Microsoft.AspNetCore.Mvc.Testing`

Always verify the actual project dependencies and configuration before
implementation.

Do not assume that a library is available only because it is common in
ASP.NET Core projects.

---

# When To Use

Use this Skill when:

- an active User Story is configured;
- the User Story has an approved Specification;
- relevant API and persistence designs exist;
- Impact Analysis is ready for planning;
- the Implementation Plan is approved;
- required test artifacts or failing tests are available;
- implementation work has not yet been completed;
- the workflow state allows implementation.

Typical requests:

- Implement the approved plan for the active User Story.
- Execute the implementation for US-001.
- Implement the current Story using the approved artifacts.
- Continue implementation from the current workflow state.
- Apply the approved ASP.NET Core implementation plan.

---

# When Not To Use

Do not use this Skill:

- directly from an unclarified User Story;
- when the Specification is missing or rejected;
- when Open Decisions remain unresolved;
- before API or database design is completed when relevant;
- before Impact Analysis;
- before Plan Review approval;
- to create or revise product requirements;
- to create system-level architecture;
- to create speculative abstractions;
- to perform unrelated refactoring;
- to approve implementation;
- to create or merge a Pull Request;
- to change GitHub Issue status automatically;
- to bypass failing tests or validation gates.

---

# Active Scope

Read:

- docs/workflow/active-story.yaml
- docs/workflow/workflow-state.yaml

Determine:

- active Story ID;
- current workflow stage;
- approved artifact versions;
- current implementation attempt;
- current branch when recorded;
- expected next workflow stage.

Work only on the active User Story.

If no active Story is configured, stop and report:

DOTNET_IMPLEMENTATION_BLOCKED: No active User Story is configured.

If the workflow stage does not permit implementation, stop and report:

DOTNET_IMPLEMENTATION_BLOCKED: Current workflow stage does not allow implementation.

Do not select another Story automatically.

---

# Canonical Sources

- Workflow / stage / loop-back keys: `docs/workflow/stage-map.yaml`
  (`IMPLEMENTATION`; loop_back keys `partial` → `IMPLEMENTATION`,
  `blocked_by_plan` → `IMPLEMENTATION_PLANNING`).
- Artifact paths: `docs/workflow/artifact-paths.yaml` — **authoritative**.
  Resolve every path from its registry key. Paths shown are illustrative.
- Status vocabulary: `docs/workflow/artifact-lifecycle.md`.
- Front matter: `docs/workflow/artifact-schema.md`.

# Canonical Input Artifacts

Read AGENTS.md first. Read `docs/workflow/active-story.yaml` and
`docs/workflow/workflow-state.yaml` (read only — never write them).

Read (registry keys, resolved via `artifact-paths.yaml`):

- `story`
- `specification`
- `api_design`, `openapi`  (or their `NOT_APPLICABLE` record)
- `database_design`, `entity_model`  (or their `NOT_APPLICABLE` record)
- `requirements` — `trebovaniya.md`. In this workflow variant there is no
  implementation plan: **the Specification plus the approved designs are the
  plan**, and `trebovaniya.md` is the authority behind them.
- `test_strategy`, `ac_test_matrix`  (+ the executable tests under
  `tests/ClassroomAgent.Tests/`)
- `open_decisions`

Read architecture references:

- docs/architecture/architecture.md
- docs/architecture/package-map.md
- docs/architecture/api-conventions.md
- docs/architecture/persistence-conventions.md
- docs/architecture/security-conventions.md

Read product constraints:

- docs/product/business-rules.md
- docs/product/business-glossary.md
- docs/product/non-functional-requirements.md

Read Story decisions when present:

- docs/decisions/<StoryId>*.md

Do not load unrelated product or historical artifacts unless needed to resolve
a concrete dependency.

---

# Artifact Authority

Use the following authority order:

1. Active User Story and Acceptance Criteria
2. Approved Specification
3. Approved API and persistence designs
4. Resolved Story decisions
5. Approved Impact Analysis
6. Approved Implementation Plan
7. Architecture and project conventions
8. Existing implementation patterns

Existing code does not override approved requirements.

The Implementation Plan does not override the Specification.

The Specification does not override the original Acceptance Criteria unless
the change was explicitly approved and traceable.

If authoritative artifacts conflict, stop and report the conflict.

Do not choose one interpretation silently.

---

# Preconditions

## User Story

The active User Story must exist.

Acceptance Criteria must be present and identifiable.

## Specification

`specification` must exist (current, not `SUPERSEDED`) and
**`HUMAN_SPEC_APPROVAL` must be recorded** in `workflow-state.yaml`. There is no
automated spec reviewer in this variant — the human gate is the check.

Do not proceed when the gate is not recorded.

## Design

Relevant design artifacts (`api_design` / `openapi` / `database_design` /
`entity_model`) must exist or be recorded `NOT_APPLICABLE`. There is no
automated design reviewer in this variant. Explicit security requirements are required when the
Story changes authentication, authorization, credentials, roles, account state,
or sensitive data.

## Plan

There is no `implementation_plan` artifact and no `HUMAN_PLAN_APPROVAL` gate in
this workflow variant. The approved `specification` and the design artifacts
are the plan; follow them in the order the Specification's Acceptance Criteria
imply. If they are not specific enough to implement without guessing, that is a
gap in the Specification — return `CHANGES_REQUIRED` with
`loop_back_stage: SPECIFICATION`, do not invent behaviour.

## Tests

`test_strategy`, `ac_test_matrix`, and the executable tests under
`tests/ClassroomAgent.Tests/` must exist (`TEST_WRITING` completed). If failing
behavior tests were not created, this is an orchestration error — return
`BLOCKED`.

## Staleness

Record every consumed artifact's version in the Implementation Report `inputs`.
If any input is `SUPERSEDED`, return `BLOCKED`.

## Open Decisions

Search required artifacts for unresolved markers:

- Open Decision
- OPEN
- TODO
- TBD
- FIXME
- ???
- unresolved
- to be decided

Do not proceed when unresolved decisions affect:

- business behavior;
- API contract;
- persistence constraints;
- security behavior;
- validation;
- exception handling;
- new dependencies;
- architecture;
- test expectations.

## Working Tree

Inspect Git status before making changes.

If unrelated uncommitted changes exist:

1. List the unrelated changes.
2. Do not overwrite them.
3. Ask for an explicit human decision if safe isolation is not possible.

---

# Implementation Principles

## Plan-Guided Implementation

Follow the approved Implementation Plan in its defined order.

Do not improvise alternative architecture without approval.

If the plan becomes infeasible because of repository reality:

1. Stop the affected step.
2. Record the discovered conflict.
3. Recommend returning to SPECIFICATION, API_DESIGN or DB_DESIGN.
4. Do not silently redesign the implementation.

## Minimal Change

Implement only what is required by the active Story.

Avoid:

- unrelated formatting changes;
- broad renaming;
- opportunistic refactoring;
- dependency upgrades;
- new frameworks;
- generic abstractions without immediate need;
- changes outside the identified impact surface.

## Existing Patterns First

Inspect existing project patterns before creating new components.

Reuse established:

- namespace/folder structure;
- naming conventions;
- DTO patterns;
- validation patterns;
- exception handling;
- security configuration;
- test conventions.

Do not copy an existing pattern when the pattern violates current approved
architecture or security requirements.

## Contract-First Implementation

When API behavior is defined by OpenAPI:

- implement the approved contract;
- preserve documented status codes;
- preserve request and response schemas;
- preserve validation behavior;
- preserve error behavior;
- do not expose additional fields.

## Explicit Persistence Design

Do not rely on EF Core convention defaults for important constraints.

Define explicitly when required by design:

- column length;
- nullability;
- uniqueness;
- identifiers;
- relationships;
- indexes;
- navigation-loading behavior;
- cascade behavior.

## Security-First Defaults

Prefer secure behavior when approved requirements leave an implementation
choice, but do not invent new business policy.

Security-sensitive ambiguity must become an Open Decision.

---

# IDEA MCP Tooling Strategy

Prefer JetBrains Rider (IntelliJ-platform) MCP capabilities when they are
available and appropriate.

## Repository and Project Inspection

Preferred capabilities:

- mcp__idea__get_project_modules
- mcp__idea__get_project_dependencies
- mcp__idea__list_directory_tree
- mcp__idea__git_status

## Semantic Analysis

Preferred capabilities:

- mcp__idea__search_symbol
- mcp__idea__get_symbol_info
- mcp__idea__analyze_calls
- mcp__idea__generate_psi_tree

Use semantic tools before plain-text search when investigating symbols,
dependencies, calls, or namespace ownership.

## File Changes

Preferred capabilities:

- mcp__idea__apply_patch
- mcp__idea__create_new_file
- mcp__idea__reformat_file
- mcp__idea__rename_refactoring

Use semantic rename refactoring instead of global text replacement when
renaming C# symbols.

## Validation

Preferred capabilities:

- mcp__idea__build_project
- mcp__idea__get_file_problems
- mcp__idea__lint_files
- mcp__ide__getDiagnostics

## Execution

Use run configurations when an approved validation step requires application
execution:

- mcp__idea__get_run_configurations
- mcp__idea__execute_run_configuration

Do not start long-lived application processes unless required by the approved
test or validation plan.

## Database Inspection

When a Rider database connection is already configured and inspection is
required, available capabilities may include:

- mcp__idea__list_database_connections
- mcp__idea__test_database_connection
- mcp__idea__list_database_schemas
- mcp__idea__list_schema_objects
- mcp__idea__get_database_object_description
- mcp__idea__introspect_schema
- mcp__idea__execute_sql_query

Database access must remain read-only unless the approved plan explicitly
requires a controlled write operation.

Do not create database connections automatically without human approval.

---

# Built-In Tool Fallback

If the Rider MCP is unavailable:

1. Use built-in file reading and search.
2. Use built-in edit or write operations.
3. Use `dotnet` CLI commands through an approved shell capability.
4. Record that Rider semantic analysis and diagnostics were unavailable.
5. Do not claim semantic certainty based only on text search.

Suggested `dotnet` CLI commands for this project include:

- `dotnet build`
- `dotnet test`
- `dotnet format --verify-no-changes`
- `dotnet ef migrations add <Name>` / `dotnet ef database update` (when a
  migration is required by the approved plan)

Use the command appropriate to the current environment.

Do not assume that a command passed unless its actual exit status and output
were observed.

---

# Implementation Workflow

## Step 1: Resolve Active Story

Read workflow state.

Record:

- Story ID;
- workflow stage;
- artifact versions;
- plan version;
- implementation attempt number.

Confirm that implementation is the currently permitted stage.

---

## Step 2: Validate Preconditions

Check:

- Story exists;
- Specification is approved;
- required designs exist;
- Impact Analysis is ready;
- Plan Review is approved;
- required tests or test specifications exist;
- no blocking Open Decisions remain;
- architecture documents are populated;
- working tree is safe.

If a precondition fails:

1. Create a blocked Implementation Report.
2. Identify the missing or invalid prerequisite.
3. Recommend the correct loop-back stage.
4. Stop before modifying code.

---

## Step 3: Establish Traceability Map

Before editing code, map:

- Acceptance Criterion;
- Specification requirement;
- design artifact;
- approved plan step;
- expected production component;
- expected test.

Keep this map available throughout implementation.

Do not implement a plan step that cannot be traced to an approved requirement
or necessary supporting infrastructure.

---

## Step 4: Inspect Current Repository

Inspect only the bounded change surface identified by Impact Analysis and the
Implementation Plan.

Confirm:

- existing namespaces/folders;
- relevant symbols;
- extension points;
- current security configuration;
- current persistence configuration;
- existing tests;
- existing error handling;
- current SQLite and EF Core settings.

Compare repository reality with predicted Impact Analysis.

If material differences exist, stop and recommend re-running Impact Analysis
or Planning.

---

## Step 5: Confirm Execution Sequence

Read the approved plan steps.

For every step identify:

- required input;
- intended file or symbol;
- expected output;
- validation method;
- dependencies on earlier steps.

Do not reorder steps without recording why.

If a different order is necessary for technical correctness, stop and request
plan revision or human approval.

---

## Step 6: Establish Test Baseline

Run the existing relevant tests before production changes.

Record:

- command;
- exit status;
- passing tests;
- failing tests;
- unrelated baseline failures.

If the baseline already fails:

1. Record the failure.
2. Determine whether the failure is related to the active Story.
3. Do not attribute pre-existing failures to the new implementation.
4. Ask for a human decision when the failure prevents reliable validation.

When test-first artifacts exist, run the new tests and confirm that expected
tests fail for the expected reason before implementation.

Do not modify tests merely to make an unjustified implementation pass.

---

## Step 7: Implement Persistence Changes

When required by the approved plan:

- create or update entities;
- define explicit Fluent API constraints in `OnModelCreating`;
- create or update repositories;
- add or update the corresponding EF Core migration;
- add persistence validation;
- add persistence tests.

For the training project, SQLite is configured as a file-based database.

The implementation must not:

- silently change the SQLite connection string to `:memory:` outside tests;
- call `Database.EnsureCreated()` / `EnsureDeleted()` against the file
  database;
- apply schema changes outside a committed EF Core migration;
- expose a database browser/admin UI without an approved development-only
  decision;
- commit generated database files;
- weaken uniqueness, nullability, or length constraints.

Follow persistence conventions and the approved DB design.

Run the relevant persistence tests after this step.

---

## Step 8: Implement Domain and Service Behavior

Implement approved business behavior in the Service layer.

Requirements:

- keep business logic out of Controllers;
- keep persistence logic behind Repositories;
- preserve transaction boundaries defined by project conventions;
- map persistence results to business outcomes;
- avoid coupling service behavior to HTTP-specific (`HttpContext`) types;
- keep methods focused;
- avoid duplicated business logic.

Run relevant unit and service tests after this step.

---

## Step 9: Implement Validation

Implement validation defined by:

- Acceptance Criteria;
- Specification;
- API design;
- business rules;
- security conventions.

Validation must not depend solely on UI clients.

When Data Annotation attributes are used, confirm that `[ApiController]` is
applied to the Controller so `ModelState` validation runs automatically.

Do not add a new dependency without explicit approval.

Validation messages and error representation must follow API conventions.

---

## Step 10: Implement API Layer

When required:

- create or modify request DTOs;
- create or modify response DTOs;
- implement Controller actions;
- map service outcomes to approved HTTP responses;
- preserve the OpenAPI contract;
- avoid exposing persistence entities;
- avoid exposing password hashes or sensitive internal fields.

Do not return sensitive values merely because the entity contains them.

Run web-layer and contract tests after this step.

---

## Step 11: Implement Exception Handling

Use the project-wide exception handling strategy (`IExceptionHandler`).

Map errors consistently.

When applicable, distinguish:

- invalid request;
- authentication failure;
- authorization failure;
- missing resource;
- duplicate or conflicting resource;
- internal failure.

Do not leak:

- stack traces;
- SQL details;
- database paths / connection strings;
- internal class names;
- credentials;
- password hashes.

---

## Step 12: Implement Security Behavior

When the Story handles registration, credentials, user identity, roles, or
account state:

- hash passwords using the approved `IPasswordHasher` (BCrypt.Net-Next);
- never store plaintext passwords;
- never return password or password hash;
- avoid logging credentials;
- enforce documented authorization boundaries;
- avoid exposing a database admin/diagnostic UI;
- preserve secure default behavior.

For password registration:

- apply the approved password policy;
- validate before persistence;
- hash before persistence;
- ensure response DTOs exclude credential fields.

If no approved password policy exists, stop and create an Open Decision.

Do not invent password complexity requirements during implementation.

---

## Step 13: Update Configuration

Change application configuration only when listed in the approved plan.

For SQLite file persistence:

- use the approved project-relative database location;
- keep generated data files outside version control;
- separate environment-specific settings via `appsettings.{Environment}.json`
  where project conventions require it;
- do not expose a database admin/diagnostic UI by default;
- do not enable unsafe schema behavior (`EnsureCreated`/`EnsureDeleted`
  against the file database) without explicit approval.

Document every configuration change in the Implementation Report.

Do not embed secrets in repository configuration.

---

## Step 14: Update Documentation

Update only documentation required by approved artifacts and actual changes.

Possible updates include:

- OpenAPI contract;
- architecture references;
- persistence documentation;
- configuration documentation;
- README instructions;
- Story traceability.

Do not rewrite approved source requirements to match implementation behavior.

When implementation reveals a requirement or design problem, return to the
appropriate earlier stage.

---

## Step 15: Reformat Changed Files

Use Rider reformatting capability when available, or `dotnet format` on the
changed files.

Reformat only changed files.

Avoid repository-wide formatting changes.

---

## Step 16: Run Incremental Validation

After every meaningful implementation group:

1. build or compile;
2. collect diagnostics;
3. run relevant tests;
4. address failures caused by current changes;
5. record evidence.

Do not postpone all validation until the end.

If three consecutive correction attempts fail for the same issue:

1. stop implementation;
2. summarize attempted fixes;
3. identify the likely root cause;
4. recommend returning to SPECIFICATION, API_DESIGN, or DB_DESIGN;
5. request human review.

---

## Step 17: Run Full Required Validation

Run all validation commands required by:

- AGENTS.md;
- Implementation Plan;
- project conventions;
- test plan.

At minimum, when available:

- build (`dotnet build`);
- unit tests;
- relevant web-layer tests;
- persistence tests;
- security tests;
- contract tests;
- Rider diagnostics;
- lint / analyzers (`dotnet format --verify-no-changes`).

Record actual commands, tools, exit codes, and results.

Do not claim PASS for any check that was not executed.

---

## Step 18: Inspect Git Change Set

Inspect the working tree.

Classify changed files as:

- Planned;
- Required Supporting Change;
- Unexpected;
- Unrelated.

Unexpected changes require explanation.

Unrelated changes must not be silently included.

Compare the change set with:

- Impact Analysis;
- Implementation Plan.

Do not perform final Reconciliation in this Skill, but identify differences for
the later Reconciliation stage.

---

## Step 19: Create Implementation Report

Create the `implementation_report` artifact at its registry path
(`docs/evidence/{story_id}-implementation-report.md`).

Do not update workflow state. Do not create a commit or Pull Request.

---

# Implementation Report Format

## Front Matter

Shared block from `docs/workflow/artifact-schema.md`
(`artifact_type: implementation_report`), plus:
`tests_status`, `build_status`, `diagnostics_status` (each `PASS` / `FAIL` /
`NOT_RUN`), `security_sensitive` (bool). `created_at` / `updated_at` are runtime
timestamps. `attempt` mirrors `workflow-state.yaml.attempt`.

Illustrative (dates are examples only):

    ---
    artifact_type: implementation_report
    story: US-001
    version: 1
    status: DRAFT
    created_at: <runtime>
    updated_at: <runtime>
    produced_by: dotnet-implementor
    inputs:
      - path: docs/plans/US-001-implementation-plan.md
        version: 1
      - path: docs/reviews/plans/US-001-plan-review.md
        version: 1
      - path: docs/tests/US-001-ac-test-matrix.md
        version: 1
    supersedes: null
    tests_status: PASS
    build_status: PASS
    diagnostics_status: PASS
    security_sensitive: true
    ---

## 1. Summary

Describe:

- implemented capability;
- implementation status;
- validation status;
- important limitations.

## 2. Source Artifacts

List the exact paths and versions of:

- User Story;
- Specification;
- designs;
- Impact Analysis;
- Implementation Plan;
- Plan Review;
- test artifacts.

## 3. Implemented Acceptance Criteria

For each Acceptance Criterion provide:

- AC identifier;
- implementation location (file + symbol);
- relevant test (class + method);
- current status.

## 4. Change Set

Every created / modified file, each classified `Planned` /
`Required Supporting Change` / `Unexpected` / `Unrelated`, with the plan step or
justification. Unrelated changes must not be included in the work.

## 5. Validation Evidence

Actual commands / Rider operations run, exit status, and results for: build,
unit tests, web-layer tests, persistence tests, security tests, contract tests,
diagnostics, lint. Do not claim `PASS` for a check that was not executed.

## 6. Configuration Changes

Every configuration change, with the approving plan step.

## 7. Deviations and Discovered Problems

Anything where repository reality diverged from the plan / impact analysis, and
what was done about it.

## 8. Open Decisions

Any Open Decision touched or newly required. If a security-sensitive decision is
missing, the implementation must stop and this report returns `BLOCKED`.

---

# Result Envelope

Return exactly this; the story-orchestrator records the transition — this Skill
does not update `workflow-state.yaml`, create commits, or open a Pull Request:

```yaml
result:
  verdict: PASS | CHANGES_REQUIRED | BLOCKED
  stage: IMPLEMENTATION
  story: <StoryId>
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/<StoryId>-implementation-report.md
  next_stage: IMPLEMENTATION_VERIFICATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
```

- `PASS` — the plan is fully implemented; build, required tests, and diagnostics
  pass with recorded evidence; the change set is scoped; no undisclosed
  security-sensitive change. The orchestrator advances to
  `IMPLEMENTATION_VERIFICATION` (independent verification still happens there).
- `CHANGES_REQUIRED` — implementation is incomplete but progressing and no
  upstream artifact is at fault → `loop_back_stage: IMPLEMENTATION`
  (key `partial`); or the plan itself is infeasible as written →
  `loop_back_stage: IMPLEMENTATION_PLANNING` (key `blocked_by_plan`).
- `BLOCKED` — a precondition failed, an authoritative artifact conflict exists,
  a security-sensitive Open Decision is unresolved, or three correction attempts
  failed on the same issue. Record the likely root cause and recommend a human
  review.

---

# Prohibited

- Do not redesign the Story or reinterpret Acceptance Criteria.
- Do not resolve Open Decisions or invent business / security policy.
- Do not perform unrelated refactoring, renames, dependency upgrades, or
  formatting outside changed files.
- Do not add a dependency without explicit human approval.
- Do not weaken, disable, or delete tests; do not weaken assertions.
- Do not expose a database admin/diagnostic UI or call
  `EnsureCreated()`/`EnsureDeleted()` against the file database without an
  approved decision.
- Do not commit generated database files.
- Do not update workflow state, create a branch/commit, or open/merge a Pull
  Request.
- Do not mark the Story complete.

---

# Completion Criteria

Complete only when: the active Story and stage are resolved; preconditions
validated; a traceability map was established; the Specification's Acceptance
Criteria were implemented in order (or a deviation recorded); incremental and
full validation
were run with recorded evidence; the change set was inspected and classified;
the `implementation_report` was written with real evidence; and the result
envelope was returned with an explicit `verdict`.
