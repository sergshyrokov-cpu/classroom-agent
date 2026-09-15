---
name: dotnet-implementor
description: >
  Implements the active User Story in the classroom-agent .NET solution by
  following the approved Specification, API and database designs, the
  story-level tests, the architecture rules and the security constraints.
  Use only at the IMPLEMENTATION stage, after HUMAN_SPEC_APPROVAL and
  TEST_WRITING.
---

# Purpose

Implement the active User Story in the classroom-agent solution
(`ClassroomAgent.sln`).

The Skill converts approved delivery artifacts into a minimal, scoped, and
reviewable set of code and configuration changes.

In this workflow variant there is no implementation plan: **the approved
Specification plus the approved designs are the plan**, and `trebovaniya.md` is
the authority behind them.

The Skill must not redesign the Story, silently resolve Open Decisions,
introduce unrelated improvements, or reinterpret Acceptance Criteria.

The Skill produces an implementation candidate.

The Skill does not approve its own work and does not declare the Story
complete.

---

# Technology Context

The stack is defined in `AGENTS.md` (Technology Stack) and
`docs/architecture/`. In short:

- .NET 10 (LTS), C# (NFR-062)
- ASP.NET Core MVC / Razor Pages + REST API
- EF Core with the Npgsql provider, PostgreSQL (PC-1);
  `EFCore.NamingConventions` for snake_case mapping (PC-5)
- ASP.NET Core Identity — local login for Dean and Owner, Google OAuth external
  login for Admin (SC-2)
- xUnit, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers (TC-2, TC-6)

Always verify the packages actually referenced in the target `.csproj` before
implementation. A package that is not referenced requires an approved Open
Decision — do not add it because it is common in ASP.NET Core projects.

---

# When To Use

Use this Skill when:

- an active User Story is configured;
- `HUMAN_SPEC_APPROVAL` is recorded;
- relevant API and database designs exist or are recorded `NOT_APPLICABLE`;
- `TEST_WRITING` has completed and the story-level tests exist;
- implementation work has not yet been completed;
- the workflow state allows implementation.

Typical requests:

- Implement the active User Story.
- Execute the implementation for US-001.
- Continue implementation from the current workflow state.

---

# When Not To Use

Do not use this Skill:

- directly from a User Story without an approved Specification;
- when the Specification is missing or rejected;
- when blocking Open Decisions remain unresolved;
- before API or database design is completed when relevant;
- before `TEST_WRITING` has completed;
- to create or revise product requirements;
- to create system-level architecture;
- to create speculative abstractions;
- to perform unrelated refactoring;
- to approve implementation;
- to commit, push or create branches;
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
  `blocked_by_specification` → `SPECIFICATION`,
  `blocked_by_api_design` → `API_DESIGN`,
  `blocked_by_database_design` → `DB_DESIGN`).
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
- `requirements` — `trebovaniya.md`
- `test_strategy`, `ac_test_matrix`  (+ the executable tests under
  `tests/ClassroomAgent.Tests/`)
- `open_decisions`

Read architecture references:

- docs/architecture/architecture.md
- docs/architecture/package-map.md
- docs/architecture/api-conventions.md
- docs/architecture/persistence-conventions.md
- docs/architecture/security-conventions.md
- docs/architecture/testing-conventions.md

Read product constraints:

- docs/product/business-rules.md
- docs/product/business-glossary.md
- docs/product/non-functional-requirements.md

Do not load unrelated product or historical artifacts unless needed to resolve
a concrete dependency.

---

# Artifact Authority

Use the order of authority from `AGENTS.md`:

1. `trebovaniya.md`
2. Active User Story and Acceptance Criteria
3. Approved Specification
4. Resolved Open Decisions
5. Approved API and database designs

Below them: architecture and project conventions (`docs/architecture/`), then
existing implementation patterns.

Existing code does not override approved requirements.

The Specification does not override the original Acceptance Criteria unless
the change was explicitly approved and traceable.

If authoritative artifacts conflict, stop and report the conflict.

Do not choose one interpretation silently.

---

# Preconditions

## User Story

The active User Story must exist.

Acceptance Criteria must be present and identifiable (`AC-001`, …).

## Specification

`specification` must exist (current, not `SUPERSEDED`) and
**`HUMAN_SPEC_APPROVAL` must be recorded** in `workflow-state.yaml`. There is no
automated spec reviewer in this variant — the human gate is the check.

Do not proceed when the gate is not recorded.

## Design

Relevant design artifacts (`api_design` / `openapi` / `database_design` /
`entity_model`) must exist or be recorded `NOT_APPLICABLE`. There is no
automated design reviewer in this variant. Explicit security requirements are
required when the Story changes authentication, authorization, credentials,
roles, account state, or sensitive data.

## Plan

There is no `implementation_plan` artifact and no `HUMAN_PLAN_APPROVAL` gate in
this workflow variant. The approved `specification` and the design artifacts
are the plan; follow them in the order the Specification's Acceptance Criteria
imply. If they are not specific enough to implement without guessing, that is a
gap upstream — return `CHANGES_REQUIRED` with `loop_back_stage: SPECIFICATION`
(key `blocked_by_specification`), or `API_DESIGN` / `DB_DESIGN` when the gap is
in a design. Do not invent behaviour.

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

## Artifact-Guided Implementation

Implement the Specification and designs in the order their Acceptance Criteria
imply.

Do not improvise alternative architecture without approval.

If the approved artifacts become infeasible because of repository reality:

1. Stop the affected work.
2. Record the discovered conflict.
3. Recommend returning to SPECIFICATION, API_DESIGN or DB_DESIGN.
4. Do not silently redesign the implementation.

## Minimal Change

Implement only what is in the active Story's scope as defined in `AGENTS.md`
(Git Policy): every change traces to a Specification requirement or Acceptance
Criterion, an element of the approved API or database design, a test in the
`ac_test_matrix`, or a supporting change the Story cannot work without (project
scaffolding, DI registration, migration, translation entries).

Avoid:

- unrelated formatting changes;
- broad renaming;
- opportunistic refactoring;
- dependency upgrades;
- new frameworks;
- generic abstractions without immediate need;
- new namespaces or folders not listed in `package-map.md` (AD-11).

## Existing Patterns First

Inspect existing project patterns before creating new components.

Reuse established:

- namespace/folder structure (`package-map.md`);
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

Do not rely on EF Core convention defaults for important constraints (PC-4).

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

# Tooling

Use the built-in file reading, search and edit tools, and the `dotnet` CLI.
Text search gives no semantic certainty: when a conclusion depends on symbol
usage or call paths, confirm it with the compiler or a test rather than a grep.

Commands (from the repository root, see `AGENTS.md`):

- `dotnet build ClassroomAgent.sln`
- `dotnet test ClassroomAgent.sln` — integration tests need a running Docker
  daemon (Testcontainers, TC-2)
- `dotnet test --filter FullyQualifiedName~<ClassName>`
- `dotnet format --verify-no-changes`
- `dotnet ef migrations add <Name> --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web`
  (when the database design requires a migration; the Control Plane uses its
  own context and startup project)

Do not assume that a command passed unless its actual exit status and output
were observed.

Do not start long-lived application processes unless a validation step
requires it.

Do not connect to any database other than the Testcontainers instances the
tests start.

---

# Implementation Workflow

## Step 1: Resolve Active Story

Read workflow state.

Record:

- Story ID;
- workflow stage;
- artifact versions;
- implementation attempt number.

Confirm that implementation is the currently permitted stage.

---

## Step 2: Validate Preconditions

Check:

- Story exists;
- `HUMAN_SPEC_APPROVAL` is recorded;
- required designs exist or are `NOT_APPLICABLE`;
- `test_strategy`, `ac_test_matrix` and the executable tests exist;
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
- design artifact element;
- expected production component (project, namespace, type);
- expected test from the `ac_test_matrix`.

Keep this map available throughout implementation.

Do not implement anything that cannot be traced to an approved requirement or a
necessary supporting change.

---

## Step 4: Inspect Current Repository

Inspect the components the traceability map names, and their direct
dependencies.

Confirm:

- existing projects, namespaces and folders;
- relevant symbols;
- extension points;
- current security configuration;
- current persistence configuration and migrations;
- existing tests;
- existing error handling.

If repository reality contradicts the approved designs (a type, table or
endpoint the design assumes is missing or different), stop and recommend the
matching loop-back stage.

---

## Step 5: Establish Test Baseline

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

Run the new story-level tests and confirm that they fail for the expected
reason before implementation.

Do not modify tests merely to make an unjustified implementation pass. If a test
itself contradicts the approved artifacts, stop and return `BLOCKED` naming
`TEST_WRITING` in `blocking_issues`.

---

## Step 6: Implement Persistence Changes

When required by the approved database design:

- create or update entities in `Domain.Entities`;
- define explicit entity configurations in `Infrastructure.Persistence`
  (PC-4, PC-5, PC-7, PC-8);
- create or update repositories in `Infrastructure.Persistence.Repositories` —
  they stage changes and never call `SaveChangesAsync()` (AD-7);
- add the corresponding EF Core migration in the same Story (PC-2);
- add persistence tests.

The implementation must not:

- call `Database.EnsureCreated()` / `EnsureDeleted()`, or apply schema changes
  outside a committed EF Core migration (PC-2);
- fall back to the EF Core InMemory provider or SQLite anywhere (TC-2);
- let one database serve several schools, or share a database between the
  Control Plane and an installation (AD-1);
- expose a database admin or diagnostic UI (SC-6);
- commit generated database files;
- weaken uniqueness, nullability, or length constraints.

Follow `persistence-conventions.md` and the approved database design.

Run the relevant persistence tests after this step.

---

## Step 7: Implement Application Behavior

Implement approved business behavior in `Application.UseCases` (in the Control
Plane: `ControlPlane.Services`).

Requirements:

- keep business logic out of Controllers and Razor (AD-3);
- reach every external system only through a port in `Application.Ports`
  (AD-4); no Google SDK type crosses into `Application` or `Domain`;
- open and commit transactions in the use case (AD-7);
- enforce read-only mode in the use case, never by hiding UI (AD-6); only the
  service writes listed in BR-026 run in read-only mode;
- map entities to DTOs in the Application layer (AD-8);
- avoid coupling use cases to `HttpContext` or EF Core types;
- keep asynchronous methods `…Async` with a `CancellationToken` (AGENTS.md
  Coding Conventions);
- avoid duplicated business logic.

Run relevant unit tests after this step.

---

## Step 8: Implement Validation

Implement validation defined by:

- Acceptance Criteria;
- Specification;
- API design;
- business rules;
- security conventions.

Validation uses Data Annotations on request types in
`Application.Models.Requests`; custom rules go in `Application.Validation`
(`trebovaniya.md` §8). Rules that need domain state are checked in the use case.

Validation must not depend solely on UI clients.

Confirm that `[ApiController]` is applied so `ModelState` validation runs
automatically, and that a rejection becomes a `400` with `fieldErrors` (API-6).

The rejected payload is never written to a log (SC-10).

Do not add a new dependency without explicit approval.

---

## Step 9: Implement API and Presentation Layer

When required:

- create or modify request types (`Application.Models.Requests`);
- create or modify response DTOs (`Application.Models.Dtos`);
- implement Controller actions and Razor pages — HTTP mapping only, no
  `DbContext` (AD-3);
- declare authorization with a named policy (API-9, SC-4); anonymous access only
  for endpoints on the SC-4 list;
- map use case outcomes to approved HTTP responses;
- preserve the OpenAPI contract;
- never expose domain entities (AD-8);
- never expose password hashes, credentials or service-account material.

Every user-visible string — screens, error messages, export labels — comes from
translation files in both Ukrainian and English (`Application.Localization`,
`ControlPlane.Localization`; NFR-073). Add every new key to both languages.

Run web-layer and contract tests after this step.

---

## Step 10: Implement Exception Handling

Use the single `IExceptionHandler` of the host project (AD-9, API-10).

Map errors as AD-9 defines: validation → 400, authn → 401, authz → 403,
not found → 404, conflict → 409, read-only mode → 409, unmapped → 500.

Do not leak:

- stack traces;
- SQL details;
- connection strings or file paths;
- internal class or namespace names;
- credentials or service-account identifiers;
- password hashes.

---

## Step 11: Implement Security Behavior

When the Story handles credentials, identity, roles, account state, Google
access or school data, follow `security-conventions.md`, in particular:

- passwords are hashed by ASP.NET Core Identity; never store or return
  plaintext passwords or hashes, never log credentials (SC-2);
- an Admin has no local password — no password column, no reset flow (SC-2);
- `AllowedAdmin` is checked on every Admin login (SC-3);
- authorization is deny-by-default with declared policies (SC-4);
- antiforgery validation is global for POST, PUT, PATCH and DELETE on both hosts,
  anonymous forms included; REST calls from the UI send the token in the
  `RequestVerificationToken` header; only endpoints on the SC-4 exemption list
  skip it (SC-4, API-7);
- session cookie `httpOnly` and `Secure`, `SameSite=Lax` in the installation and
  `Strict` in the Control Plane; antiforgery cookie `Strict`; HTTPS redirection
  and HSTS in the installation (SC-2);
- GET changes nothing — sign-out, language choice, synchronization and export
  are POST; the Google OAuth callback is the only GET that writes (API-4);
- the service-account key is never stored in a database, accepted through a UI,
  or committed (SC-7);
- every Google scope is read-only; the program never writes to Google
  Workspace (SC-8);
- logs carry internal identifiers only — no names, emails, grades, keys or raw
  Google errors (SC-10);
- actions SC-11 lists write an audit event;
- school data goes only to Google and the Control Plane (SC-13).

If a required policy (for example a password policy) is not approved, stop and
create an Open Decision.

Do not invent password complexity or lockout requirements during
implementation.

---

## Step 12: Update Configuration

Change application configuration only when the approved artifacts require it.

- Deployment configuration lives in `appsettings.json` /
  `appsettings.{Environment}.json` / environment variables (AD-10, DC-3).
- Startup wiring lives in `Program.cs` and `Configuration` extension methods,
  with no business logic (AD-10).
- Nothing school-specific is hard-coded (AD-10).
- Do not embed secrets or the service-account key in repository configuration
  (SC-7).

Document every configuration change in the Implementation Report.

---

## Step 13: Update Documentation

Update only documentation required by approved artifacts and actual changes.

Do not rewrite approved source requirements to match implementation behavior.

When implementation reveals a requirement or design problem, return to the
appropriate earlier stage.

---

## Step 14: Reformat Changed Files

Run `dotnet format` on the changed files only.

Avoid repository-wide formatting changes.

---

## Step 15: Run Incremental Validation

After every meaningful implementation group:

1. build;
2. run relevant tests;
3. address failures caused by current changes;
4. record evidence.

Do not postpone all validation until the end.

If three consecutive correction attempts fail for the same issue:

1. stop implementation;
2. summarize attempted fixes;
3. identify the likely root cause;
4. recommend returning to SPECIFICATION, API_DESIGN, or DB_DESIGN;
5. request human review.

---

## Step 16: Run Full Required Validation

Run all validation required by `AGENTS.md` (Definition of Done) and
`testing-conventions.md`:

- `dotnet build ClassroomAgent.sln` — no errors, no warnings
  (`TreatWarningsAsErrors`);
- `dotnet test ClassroomAgent.sln` — green, with no skipped, ignored or
  commented-out tests;
- `dotnet format --verify-no-changes`.

Record actual commands, exit codes, and results.

Do not claim PASS for any check that was not executed.

---

## Step 17: Inspect Git Change Set

Inspect the working tree.

For every created, modified or deleted file record its trace per the scope rule
in `AGENTS.md`: the Acceptance Criterion, Specification requirement, design
element or `ac_test_matrix` test it serves, or the supporting change it is.

A file with no trace is out of scope: do not include it in the work; record an
Open Decision if the Story seems to need it.

Confirm that no secret, generated database file or IDE-local config is in the
change set.

---

## Step 18: Create Implementation Report

Create the `implementation_report` artifact at its registry path
(`docs/evidence/{story_id}-implementation-report.md`).

Do not update workflow state. Do not commit, push or create a branch.

---

# Implementation Report Format

## Front Matter

Shared block from `docs/workflow/artifact-schema.md`
(`artifact_type: implementation_report`), plus:
`tests_status`, `build_status`, `format_status` (each `PASS` / `FAIL` /
`NOT_RUN`), `security_sensitive` (bool). `created_at` / `updated_at` are runtime
timestamps. `attempt` mirrors `workflow-state.yaml.attempt`.

Illustrative:

    ---
    artifact_type: implementation_report
    story: US-001
    version: 1
    status: DRAFT
    created_at: <runtime>
    updated_at: <runtime>
    produced_by: dotnet-implementor
    inputs:
      - path: docs/specifications/US-001-spec.md
        version: 1
      - path: docs/designs/database/US-001-db-design.md
        version: 1
      - path: docs/tests/US-001-ac-test-matrix.md
        version: 1
    supersedes: null
    tests_status: PASS
    build_status: PASS
    format_status: PASS
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
- Open Decisions;
- designs;
- test artifacts.

## 3. Implemented Acceptance Criteria

For each Acceptance Criterion provide:

- AC identifier;
- implementation location (file + symbol);
- relevant test (class + method);
- current status.

## 4. Change Set

Every created / modified / deleted file with its trace (Acceptance Criterion,
Specification requirement, design element, `ac_test_matrix` test, or the named
supporting change). No file without a trace.

## 5. Validation Evidence

Actual commands run, exit status, and results for: build, tests (unit,
integration, contract, security), format check. Do not claim `PASS` for a check
that was not executed.

## 6. Configuration Changes

Every configuration change, with the artifact that requires it.

## 7. Deviations and Discovered Problems

Anything where repository reality diverged from the approved artifacts, and
what was done about it.

## 8. Open Decisions

Any Open Decision touched or newly required. If a security-sensitive decision is
missing, the implementation must stop and this report returns `BLOCKED`.

---

# Result Envelope

Return exactly this; the story-orchestrator records the transition — this Skill
does not update `workflow-state.yaml` and does not commit:

```yaml
result:
  verdict: PASS | CHANGES_REQUIRED | BLOCKED
  stage: IMPLEMENTATION
  story: <StoryId>
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/<StoryId>-implementation-report.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
```

- `PASS` — every Acceptance Criterion is implemented; build, tests and the format
  check pass with recorded evidence; every changed file is traced; no
  undisclosed security-sensitive change. The orchestrator advances to
  `SECURITY_REVIEW`.
- `CHANGES_REQUIRED` — either implementation is incomplete but progressing and
  no upstream artifact is at fault → `loop_back_stage: IMPLEMENTATION`
  (key `partial`); or an upstream artifact cannot be implemented as written →
  `SPECIFICATION` (key `blocked_by_specification`), `API_DESIGN`
  (key `blocked_by_api_design`) or `DB_DESIGN`
  (key `blocked_by_database_design`).
- `BLOCKED` — a precondition failed, an authoritative artifact conflict exists,
  a story-level test contradicts the approved artifacts (name `TEST_WRITING` in
  `blocking_issues`), a security-sensitive Open Decision is unresolved, or three
  correction attempts failed on the same issue. Record the likely root cause and
  recommend a human review.

---

# Prohibited

- Do not redesign the Story or reinterpret Acceptance Criteria.
- Do not resolve Open Decisions or invent business / security policy.
- Do not perform unrelated refactoring, renames, dependency upgrades, or
  formatting outside changed files.
- Do not add a dependency without an approved Open Decision.
- Do not weaken, disable, or delete tests; do not weaken assertions.
- Do not expose a database admin/diagnostic UI or call
  `EnsureCreated()`/`EnsureDeleted()`.
- Do not write to Google Workspace or call a live Google API.
- Do not commit generated database files, secrets or the service-account key.
- Do not edit the Python prototype.
- Do not update workflow state, commit, push or create a branch.
- Do not mark the Story complete.

---

# Completion Criteria

Complete only when: the active Story and stage are resolved; preconditions
validated; a traceability map was established; the Specification's Acceptance
Criteria were implemented (or a deviation recorded); incremental and full
validation were run with recorded evidence; every changed file was traced; the
`implementation_report` was written with real evidence; and the result envelope
was returned with an explicit `verdict`.
