---
name: test-writer
description: >
   Designs and implements story-level automated tests from an approved User
   Story, Acceptance Criteria, Specification, API design, database design,
   Impact Analysis, and Implementation Plan. Use after Human Plan Approval
   and before production implementation.
---

# Test Writer

## Purpose

Create executable evidence that will verify whether the future implementation
satisfies the approved User Story and Acceptance Criteria.

The Skill must design tests from approved requirements and contracts, not from
implementation assumptions.

Tests should be created before production implementation whenever technically
practical.

## Scope

This Skill is responsible for:

- creating an Acceptance Criteria to Test mapping;
- selecting appropriate test levels;
- documenting the test strategy for the active story;
- creating automated test source files;
- running the relevant tests;
- confirming the expected pre-implementation test state;
- producing test-generation evidence.

This Skill is not responsible for:

- implementing production code;
- changing the User Story;
- changing Acceptance Criteria;
- changing approved API or database designs;
- resolving product or architectural decisions;
- weakening tests to obtain a passing build.

## Canonical Sources

- Workflow / stage / loop-back keys: `docs/workflow/stage-map.yaml`
  (`TEST_WRITING`; loop_back keys `changes_required_tests`,
  `invalid_specification`, `invalid_api_design`, `invalid_database_design`).
- Artifact paths: `docs/workflow/artifact-paths.yaml` — **authoritative**.
  Resolve every path from its registry key. Paths shown are illustrative.
- Status vocabulary: `docs/workflow/artifact-lifecycle.md`.
- Front matter: `docs/workflow/artifact-schema.md`.

## Required Context

Read `docs/workflow/active-story.yaml` and `docs/workflow/workflow-state.yaml`
(read only — never write them), and `AGENTS.md`.

Read (registry keys, resolved via `artifact-paths.yaml`):

- `story`
- `specification`
- `api_design`, `openapi`  (or their `NOT_APPLICABLE` record)
- `database_design`, `entity_model`  (or their `NOT_APPLICABLE` record)
- `requirements` — `trebovaniya.md`, for anything the Specification leaves
  implicit (roles and the permission matrix in section 2, entities in
  section 3, Control Plane behaviour in section 9)

Read relevant project conventions from:

- `docs/architecture/architecture.md`
- `docs/architecture/testing-conventions.md`
- `docs/architecture/api-conventions.md`
- `docs/architecture/persistence-conventions.md`
- `docs/architecture/security-conventions.md`
- `docs/product/business-rules.md`
- `docs/product/non-functional-requirements.md`

Load only the context required for the active story.

Do not load unrelated Story artifacts.

## Preconditions

Before creating tests, verify that:

- exactly one active story is defined and `workflow-state.yaml` references it;
- `specification` exists;
- **`HUMAN_SPEC_APPROVAL` is recorded** in `workflow-state.yaml`
  (`pending_human_gate.stage == HUMAN_SPEC_APPROVAL` with `status == APPROVED`,
  or the workflow has already advanced past it). This is the only human gate
  before TEST_WRITING in this workflow variant — there is no
  `HUMAN_PLAN_APPROVAL`. If it is not recorded, the orchestrator routed here
  wrongly: return `BLOCKED`;
- `api_design` exists for API-related behavior (or is `NOT_APPLICABLE`);
- `database_design` exists for persistence-related behavior (or is
  `NOT_APPLICABLE`);
- no blocking Open Decisions remain;
- no unresolved `TODO`, `TBD`, or placeholder values affect test behavior;
- every consumed artifact is current (non-`SUPERSEDED`); record versions in the
  test artifacts' `inputs` front matter.

If required inputs are missing or stale, return `verdict: BLOCKED`.

If an approved artifact contains contradictions that prevent reliable test
creation, create a finding and return `BLOCKED`.

Do not silently resolve missing business, security, API, persistence, or
architecture decisions.

## Testing Principles

Tests must validate externally observable behavior wherever possible.

Prefer tests that remain valid after internal refactoring.

Do not assert internal implementation structure unless the structure itself is
an approved architectural requirement.

Every Acceptance Criterion must map to at least one test scenario.

Security-sensitive Acceptance Criteria should include negative scenarios.

Validation rules should include boundary scenarios.

Error responses should be validated against the approved API contract.

Persistence behavior should be validated against the approved database design.

Tests must be deterministic and repeatable.

Do not make tests dependent on execution order.

Do not use external production services.

## Test Levels

Select test levels according to the behavior being verified.

### Contract Tests

Use contract tests for:

- HTTP methods and paths;
- request payloads;
- response payloads;
- HTTP status codes;
- validation responses;
- error response structure;
- authentication and authorization behavior;
- conformance with the approved OpenAPI contract.

### Integration Tests

Use integration tests for:

- ASP.NET Core MVC request handling (`WebApplicationFactory<Program>`);
- authentication/authorization behavior;
- service and repository integration;
- EF Core entity mappings;
- database constraints;
- transaction behavior;
- serialization and deserialization;
- application configuration relevant to the story.

For the training project, integration tests may use an isolated in-memory
SQLite database.

Tests must not depend on data from previous test runs.

### Unit Tests

Use unit tests for:

- isolated business rules;
- validation logic;
- transformations;
- deterministic service behavior;
- branching logic that can be tested without an ASP.NET Core host.

Do not create unit tests that only verify framework behavior.

Do not mock every dependency when an integration test provides stronger and
more maintainable evidence.

### Security Tests

Create security-focused tests for applicable behavior, including:

- unauthenticated access;
- unauthorized access;
- sensitive data exposure;
- password handling;
- invalid credentials;
- role restrictions;
- unsafe error responses.

## Workflow

1. Read the active story and confirm the current workflow stage.

2. Read all approved input artifacts.

3. Extract every Acceptance Criterion and assign a stable identifier if one is
   not already present.

4. Build an Acceptance Criteria to Test matrix.

5. For each Acceptance Criterion, identify:

   - positive scenarios;
   - negative scenarios;
   - boundary scenarios;
   - validation scenarios;
   - security-relevant scenarios;
   - persistence-relevant scenarios.

6. Select the minimum appropriate test level for each scenario:

   - contract;
   - integration;
   - unit;
   - security.

7. Identify shared test fixtures and setup requirements.

8. Define expected observable behavior for every test.

9. Record any requirement that cannot be tested reliably.

10. If a test cannot be defined because of missing requirements:

   - create or update an Open Decision under `docs/decisions/`;
   - do not invent the expected behavior;
   - return `BLOCKED` if the missing decision affects mandatory coverage.

11. Create the story-level test strategy artifact.

12. Create the Acceptance Criteria to Test traceability matrix.

13. Implement automated tests under `tests/ClassroomAgent.Tests/`.

14. Keep test namespaces aligned with the production namespace structure.

15. Run the story-specific tests.

16. Classify the result of each test:

   - fails because production behavior is not implemented yet;
   - passes because existing behavior already satisfies the requirement;
   - fails because the test or environment is invalid;
   - cannot run because infrastructure or configuration is missing.

17. Correct invalid tests or test setup problems.

18. Do not modify production code.

19. Run the relevant test set again.

20. Create a test-generation report containing actual execution evidence.

21. Return the required result envelope.

## Red-Phase Verification

Before implementation begins, newly introduced behavior tests are normally
expected to fail because production behavior does not yet exist.

A failing test is acceptable only when:

- the test compiles;
- the application test context is valid, where applicable;
- the failure is caused by missing or incorrect production behavior;
- the failure message is consistent with the scenario being tested.

A failing test is not acceptable when caused by:

- syntax errors;
- invalid imports;
- missing test dependencies that should already exist;
- incorrect ASP.NET Core test host configuration created by the test;
- invalid fixtures;
- incorrect assertions;
- a contradiction with approved requirements.

Existing regression tests must continue to pass.

If all new behavior tests pass before implementation, investigate whether:

- the functionality already exists;
- the test does not verify the intended behavior;
- the assertion is too weak;
- the test bypasses the relevant application layer.

Document the conclusion in the test-generation report.

## Test Design Artifact

Create the `test_strategy` artifact at its registry path
(`docs/tests/{story_id}-test-strategy.md`), front matter per
`docs/workflow/artifact-schema.md` (`artifact_type: test_strategy`).

The document must contain:

- story identifier;
- test scope;
- selected test levels;
- positive scenarios;
- negative scenarios;
- boundary scenarios;
- validation scenarios;
- security scenarios;
- persistence scenarios;
- required fixtures;
- excluded scenarios with justification;
- known limitations;
- Open Decisions affecting testing.

## Traceability Artifact

Create the `ac_test_matrix` artifact at its registry path
(`docs/tests/{story_id}-ac-test-matrix.md`), front matter per
`docs/workflow/artifact-schema.md` (`artifact_type: ac_test_matrix`).

**This Skill owns the Acceptance-Criteria → test matrix.** Downstream Skills
(`dotnet-implementor`, `security-reviewer`) read it; they do not rebuild it.

Note: in this workflow variant TEST_WRITING runs immediately before
IMPLEMENTATION and there is no separate IMPLEMENTATION_VERIFICATION stage — the
tests produced here **are** the verification. Write them accordingly: a green
suite must be sufficient evidence that the Acceptance Criteria hold.

Use a structure equivalent to:

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|

Every Acceptance Criterion must have at least one mapped scenario.

A mandatory Acceptance Criterion without a mapped test is a blocking finding.

## Test Source Files

Create executable tests under:

- `tests/ClassroomAgent.Tests/`

Use the same root namespace (`ClassroomAgent.Tests`) mirroring the
application's namespace tree.

Follow `docs/architecture/testing-conventions.md` (TC-*) and existing test
patterns.

Prefer descriptive test names that express behavior and expected outcome.

Examples of naming intent:

- valid registration creates a customer account;
- duplicate email returns conflict;
- invalid email returns bad request;
- response does not expose password data.

Do not rely on comments to explain unclear test names.

## Test Isolation

Each test must control its own initial state.

Where database cleanup is required, use a deterministic mechanism supported by
the test environment.

Do not assume that the embedded database is empty unless the test setup
guarantees it.

Do not depend on test execution order.

Do not use the file-backed development database as test data storage.

Use an isolated test configuration.

## Customer Portal Security Constraints

For user-registration functionality, tests must verify all approved security
requirements.

When applicable, verify that:

- plaintext passwords are never persisted;
- password hashes are never returned by the API;
- password fields are never included in response DTOs;
- invalid passwords are rejected according to the approved password policy;
- unauthenticated access is permitted or denied exactly as specified;
- unrelated protected endpoints remain protected;
- error responses do not expose internal implementation details.

Do not invent a password policy.

If password requirements are absent or unresolved, return `BLOCKED`.

## API Contract Alignment

Contract and integration tests must align with:

- the approved OpenAPI design;
- project API conventions;
- approved status codes;
- approved request and response schemas;
- approved validation and error behavior.

Do not change the OpenAPI contract from this Skill.

If the contract is inconsistent with the approved Specification, return
`BLOCKED` and identify the conflict.

## Database Design Alignment

Persistence-related tests must validate approved constraints, including:

- nullability;
- uniqueness;
- maximum lengths;
- relationships;
- default values;
- identifier behavior.

Do not infer database constraints from EF Core convention defaults.

Do not modify database design artifacts from this Skill.

## Output Artifacts

Create or update:

- `docs/tests/<StoryId>-test-strategy.md`
- `docs/tests/<StoryId>-ac-test-matrix.md`
- `docs/evidence/<StoryId>-test-generation-report.md`

Create executable test source files under:

- `tests/ClassroomAgent.Tests/`

The test-generation report must include:

- story identifier;
- test files created;
- test files modified;
- commands or IDE tools used;
- tests executed;
- passing existing tests;
- expected failing new tests;
- unexpected failures;
- untested Acceptance Criteria;
- Open Decisions;
- overall result.

## Preferred Tools

Use JetBrains Rider (IntelliJ-platform) MCP capabilities when available for:

- reading project modules;
- inspecting dependencies;
- locating existing test patterns;
- inspecting symbols;
- building the project;
- collecting diagnostics;
- executing approved test run configurations.

Use Claude Code built-in file tools for local artifact creation and targeted
file updates.

Do not use GitHub MCP for local test implementation.

Do not modify GitHub Issues or Pull Requests from this Skill.

## Constraints

- Do not modify production source files.
- Do not change the User Story.
- Do not change Acceptance Criteria.
- Do not rewrite approved Specification content.
- Do not change approved API or database designs.
- Do not add dependencies without explicit approval.
- Do not disable existing tests.
- Do not delete failing tests.
- Do not weaken assertions to obtain passing results.
- Do not use sleeps or timing-dependent behavior unless explicitly required.
- Do not hide unexpected failures.
- Do not claim implementation completion.
- Do not create or merge a Pull Request.

## Completion Criteria

The Skill returns `verdict: PASS` only when:

- every Acceptance Criterion has test coverage in the `ac_test_matrix`;
- `test_strategy` and `ac_test_matrix` artifacts exist with valid front matter;
- executable tests exist under `tests/ClassroomAgent.Tests/` and compile;
- test setup is valid; existing regression tests pass;
- new behavior tests fail only for expected missing implementation
  (red phase verified);
- security-relevant and persistence scenarios are covered where applicable;
- the `test_generation_report` contains actual execution evidence;
- no blocking Open Decisions remain.

## Output Artifacts

- `test_strategy`  (`docs/tests/{story_id}-test-strategy.md`)
- `ac_test_matrix` (`docs/tests/{story_id}-ac-test-matrix.md`)
- `test_generation_report` (`docs/evidence/{story_id}-test-generation-report.md`)
- executable test source under `tests/ClassroomAgent.Tests/`

All three Markdown artifacts carry front matter per
`docs/workflow/artifact-schema.md`.

## Result Envelope

Return exactly this; the story-orchestrator records the transition — this Skill
does not update `workflow-state.yaml`:

```yaml
result:
  verdict: PASS | CHANGES_REQUIRED | BLOCKED
  stage: TEST_WRITING
  story: <StoryId>
  artifact_status: DRAFT
  artifacts:
    - docs/tests/<StoryId>-test-strategy.md
    - docs/tests/<StoryId>-ac-test-matrix.md
    - docs/evidence/<StoryId>-test-generation-report.md
  next_stage: IMPLEMENTATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
```

(The test source file paths are listed inside the `test_generation_report`.)

- `PASS` — see Completion Criteria. The orchestrator advances to `IMPLEMENTATION`.
- `CHANGES_REQUIRED` — the tests or test artifacts need correction that stays
  within test writing; `loop_back_stage: TEST_WRITING` (key
  `changes_required_tests`).
- `BLOCKED` — test creation cannot continue: unresolved blocking Open Decision,
  a missing/stale required design or plan, conflicting approved artifacts,
  missing test infrastructure, or an unapproved dependency requirement. When the
  block is caused by an invalid upstream artifact, set `loop_back_stage` using a
  key from `stage-map.yaml` `TEST_WRITING.loop_back`:
  `invalid_specification` → `SPECIFICATION`,
  `invalid_api_design` → `API_DESIGN`,
  `invalid_database_design` → `DB_DESIGN`.
  Do not invent an expected behavior to get past a block.
