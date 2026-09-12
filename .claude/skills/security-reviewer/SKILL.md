---
name: security-reviewer
description: >
  Performs an independent security review of the active User Story
  implementation in the Customer Portal ASP.NET Core application. Reviews
  authentication, authorization, sensitive data handling, input validation,
  API exposure, persistence, configuration, dependencies, logging, tests,
  and security-relevant deviations from approved artifacts. Use after
  Implementation Verification and before Reconciliation or Pull Request
  creation.
---

# Purpose

Perform an independent, evidence-based security review of the active User
Story implementation.

The Skill determines whether the implementation introduces unacceptable
security risks or violates approved security requirements.

The Skill focuses on security properties rather than general functional
correctness.

The Skill does not assume that successful compilation, passing tests, or an
approved Implementation Verification automatically imply secure behavior.

The Skill produces a Security Review artifact.

The Skill does not modify production code, rewrite tests, accept security
risk, create a Pull Request, approve merge, or mark the Story complete.

---

# Position in the Workflow

Canonical workflow: `docs/workflow/stage-map.yaml`. Relevant slice:

    IMPLEMENTATION
    → SECURITY_REVIEW            (this Skill)
    → HUMAN_PR_APPROVAL
    → COMPLETED

This is the lightweight workflow variant: SECURITY_REVIEW is the **only**
automated reviewer, and it runs directly after IMPLEMENTATION. There is no
IMPLEMENTATION_VERIFICATION stage ahead of it and no RECONCILIATION after it,
so this review is the last automated check before a human commits.

This Skill owns only the `SECURITY_REVIEW` stage. Loop-back
(`stage-map.yaml`): `changes_required` → `IMPLEMENTATION`,
`invalid_security_design` → `API_DESIGN`.

---

# Security Review Scope

The review covers security behavior introduced, modified, or affected by the
active User Story.

Review areas include:

- authentication;
- authorization;
- password and credential handling;
- sensitive data exposure;
- request validation;
- output encoding and serialization;
- exception and error handling;
- persistence constraints;
- configuration;
- logging;
- dependency changes;
- database exposure;
- insecure defaults;
- security test coverage;
- abuse and misuse scenarios;
- deviations from approved security requirements.

The review must remain scoped to the active User Story and its affected
components.

Repository-wide security assessment is outside this Skill unless explicitly
requested.

---

# When To Use

Use this Skill when:

- an active User Story is configured;
- implementation has completed;
- an Implementation Report exists;
- Implementation Verification has completed;
- security review is the current workflow stage;
- the Story affects user input, credentials, authentication, authorization,
  personal data, persistence, API exposure, configuration, or external
  integrations;
- a previous Security Review rejected the implementation and security fixes
  have been applied.

Typical requests:

- Perform Security Review for the active User Story.
- Review US-001 implementation for security issues.
- Check whether the implementation is safe to commit.
- Re-run Security Review after security fixes.
- Review authentication, password handling, and data exposure for this Story.

---

# When Not To Use

Do not use this Skill:

- before implementation exists;
- before Implementation Verification;
- to define product security policy;
- to invent missing security requirements;
- to implement security fixes;
- to generate the initial test suite;
- to perform general code style review;
- to perform final Reconciliation;
- to accept security risk on behalf of a human;
- to create, approve, or merge a Pull Request;
- to change workflow state automatically;
- as a substitute for professional penetration testing or organization-specific
  security assessment when such assessment is required.

---

# Independent Review Principle

The Security Reviewer must remain independent from the Implementor.

The Implementor answers:

    Which security requirements were implemented?

The Security Reviewer answers:

    Which security properties can be independently verified, and which risks
    remain?

Do not trust the Implementation Report or Implementation Verification Report
without checking the underlying implementation and available evidence.

Do not treat the presence of ASP.NET Core authentication/authorization
middleware as proof that the application is secure.

Do not treat password hashing as sufficient protection if password input,
logging, serialization, database constraints, or endpoint access remain
unsafe.

---

# Active Scope

Read:

- docs/workflow/active-story.yaml
- docs/workflow/workflow-state.yaml

Determine:

- active Story ID;
- current workflow stage;
- current artifact versions;
- implementation attempt;
- verification attempt;
- security review attempt;
- expected next stage.

Work only on the active User Story.

If no active Story is configured, stop and report:

    SECURITY_REVIEW_BLOCKED:
    No active User Story is configured.

If the workflow stage does not permit Security Review, stop and report:

    SECURITY_REVIEW_BLOCKED:
    Current workflow stage does not allow Security Review.

Do not select another Story automatically.

---

# Canonical Sources

- Workflow / stage / loop-back: `docs/workflow/stage-map.yaml`
  (`SECURITY_REVIEW`; loop_back `changes_required` → `IMPLEMENTATION`,
  `invalid_security_design` → `API_DESIGN`).
- Artifact paths: `docs/workflow/artifact-paths.yaml` — **authoritative**.
  Resolve every path from its registry key. Paths shown are illustrative.
- Status vocabulary: `docs/workflow/artifact-lifecycle.md`.
- Front matter: `docs/workflow/artifact-schema.md`.

# Required Context

Read AGENTS.md first. Read `docs/workflow/active-story.yaml` and
`docs/workflow/workflow-state.yaml` (read only).

Read (registry keys, resolved via `artifact-paths.yaml`):

- `story`
- `specification`
- `implementation_report`  ← its build/test evidence is the only such evidence
  available in this variant; there is no `implementation_verification`
- `api_design`, `openapi`, `database_design`, `entity_model`
  (or their `NOT_APPLICABLE` record)
- `test_strategy`, `ac_test_matrix`  (+ executable tests under
  `tests/ClassroomAgent.Tests/`)
- `open_decisions`
- `requirements` — `trebovaniya.md`. Section 5 (privacy: personal data of
  minors; secrets handling), section 2 (role permission matrix), section 6
  (OAuth scopes, service-account model), section 9 (Control Plane
  authorization, the three Owner control points) are the security baseline
  this review holds the implementation to.

Read relevant project configuration: `ClassroomAgent.sln`, `*.csproj` files,
`appsettings.json` / `appsettings.{Environment}.json`, EF Core migrations,
security configuration, Git ignore rules, environment templates.

Read telemetry only when needed: `docs/hooks/tool-usage.jsonl`, `docs/evidence/`.

Read architecture references:

- docs/architecture/architecture.md
- docs/architecture/package-map.md
- docs/architecture/api-conventions.md
- docs/architecture/persistence-conventions.md
- docs/architecture/security-conventions.md

Read product context:

- docs/product/business-rules.md
- docs/product/business-glossary.md
- docs/product/non-functional-requirements.md

(`open_decisions` is listed in Required Context above.)

Do not load unrelated Story artifacts unless a concrete security dependency
requires them.

---

# Artifact Authority

Use the following authority order:

1. Active User Story and Acceptance Criteria
2. Approved Specification
3. Resolved Story decisions
4. Approved security requirements and conventions
5. Approved API and database designs
6. Approved Implementation Plan
7. Product-wide business rules and NFRs
8. Implementation Verification evidence
9. Current code and configuration
10. Implementation Report claims

Current code cannot redefine security requirements.

Tests cannot redefine security requirements.

A convenient implementation choice cannot replace an unresolved security
decision.

If authoritative artifacts conflict, stop and report the conflict.

Do not silently select the least restrictive interpretation.

---

# Preconditions

## Implementation

`implementation_report` must exist with verdict `PASS`, current version, and
must record a green build and a green test run. There is no separate
verification stage in this variant, so **this report's evidence is the only
functional evidence** — treat it sceptically: if it claims success without
concrete build/test output, return `BLOCKED` rather than assuming.

Do not issue a positive Security Review result when the implementation is
functionally failing; return `CHANGES_REQUIRED` with
`loop_back_stage: IMPLEMENTATION`.

`HUMAN_SPEC_APPROVAL` must be recorded. Record every consumed artifact version
in this review's `inputs`; any `SUPERSEDED` mandatory input →
`verdict: BLOCKED`.

## Security Requirements

The Specification or resolved Story decisions must define security-relevant
behavior when the Story handles:

- passwords;
- credentials;
- authentication;
- authorization;
- roles;
- personal data;
- account state;
- tokens;
- external input;
- externally accessible endpoints.

If material security behavior is undefined, do not invent policy. Return
`verdict: BLOCKED`; name `SPECIFICATION` in `blocking_issues` (an undefined
security requirement is an upstream defect, not something Security Review can
route to `IMPLEMENTATION`).

## Architecture Documentation

The following file must exist and contain meaningful guidance:

- docs/architecture/security-conventions.md

Relevant architecture, API, and persistence convention files must also be
available.

An empty security conventions file is a blocker for approval.

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

Security-sensitive Open Decisions are blockers.

Examples include:

- password policy;
- password hashing algorithm;
- account activation behavior;
- email uniqueness behavior;
- authentication requirements;
- authorization rules;
- sensitive data retention;
- database admin/diagnostic UI exposure;
- database file location;
- schema initialization strategy;
- error response information;
- audit logging requirements.

## Working Tree

Inspect the current Git state.

Identify:

- modified files;
- untracked files;
- deleted files;
- generated SQLite database files;
- configuration files;
- secret-like files;
- unrelated changes.

Do not modify or remove existing changes.

---

# Security Review Principles

## Requirements Before Assumptions

Security behavior must come from approved artifacts.

Do not invent password complexity, account lockout, token expiration, or other
business policies during review.

If a necessary security decision is missing, report it as a blocker.

## Deny by Default

Externally accessible functionality should not become publicly available
unless the approved requirements explicitly allow it.

## Least Privilege

Accounts, endpoints, tools, database access, and configuration should receive
only the permissions required by the current Story.

## Defense in Depth

Do not rely on a single control when multiple layers are appropriate.

Examples:

- request validation and database constraints;
- authorization rules and service-level ownership checks;
- password hashing and response DTO isolation;
- secret scanning and Git ignore rules.

## No Sensitive Data Exposure

Credentials and internal security data must not be exposed through:

- API responses;
- logs;
- exceptions;
- generated reports;
- telemetry;
- database consoles;
- committed files.

## Verify Runtime Effect

The presence of an annotation or configuration class does not prove that the
control is active.

Prefer runtime, test, configuration, or framework-wiring evidence.

## Evidence Over Confidence

A reviewer statement such as:

    The implementation appears secure.

is not sufficient evidence.

---

# Threat-Oriented Review Model

For each externally observable capability, consider:

- who can invoke it;
- what input is accepted;
- what data is read;
- what data is written;
- what sensitive data exists;
- what trust boundary is crossed;
- what happens on invalid input;
- what happens on repeated input;
- what happens on unauthorized input;
- what information is revealed by errors;
- what state can be changed;
- what abuse is possible.

Use practical, Story-scoped abuse cases.

Do not produce speculative enterprise threat models unrelated to the active
Story.

---

# IDEA MCP Tooling Strategy

Prefer JetBrains Rider (IntelliJ-platform) MCP capabilities when available.

## Project Inspection

Use when appropriate:

- mcp__idea__get_project_modules
- mcp__idea__get_project_dependencies
- mcp__idea__list_directory_tree
- mcp__idea__git_status
- mcp__idea__get_repositories

## Semantic Analysis

Use when appropriate:

- mcp__idea__search_symbol
- mcp__idea__get_symbol_info
- mcp__idea__analyze_calls
- mcp__idea__generate_psi_tree

Use semantic evidence to inspect:

- security configuration usage;
- password hasher injection;
- service call paths;
- repository access paths;
- endpoint-to-service relationships;
- sensitive field propagation;
- direct repository access;
- authorization-related call paths.

## Build and Diagnostics

Use when appropriate:

- mcp__idea__build_project
- mcp__idea__get_file_problems
- mcp__idea__lint_files
- mcp__ide__getDiagnostics

## Runtime and Test Execution

Use when appropriate:

- mcp__idea__get_run_configurations
- mcp__idea__execute_run_configuration
- mcp__idea__execute_terminal_command

Do not start or expose the application on an externally accessible interface
unless explicitly approved.

## Database Inspection

When an approved IDEA database connection already exists, use read-only
capabilities when relevant:

- mcp__idea__list_database_connections
- mcp__idea__test_database_connection
- mcp__idea__list_database_schemas
- mcp__idea__list_schema_objects
- mcp__idea__get_database_object_description
- mcp__idea__introspect_schema
- mcp__idea__execute_sql_query
- mcp__idea__preview_table_data

Do not create a database connection automatically.

Do not execute destructive SQL.

Do not retrieve or copy sensitive data unnecessarily.

---

# Built-In Tool Fallback

If the Rider MCP is unavailable:

1. Use built-in file inspection and search.
2. Use approved `dotnet` CLI commands.
3. Inspect configuration and Git state through approved shell operations.
4. Record unavailable semantic and runtime checks.
5. Avoid unsupported security claims.

Suggested project commands may include:

    dotnet clean
    dotnet test
    dotnet build
    dotnet format --verify-no-changes

Use only commands supported by the repository.

Record actual results.

---

# Security Review Workflow

## Step 1: Resolve Active Story

Read workflow state.

Record:

- Story ID;
- current stage;
- implementation attempt;
- Implementation Verification version;
- Security Review attempt;
- relevant artifact versions.

Confirm that SECURITY_REVIEW is the current permitted stage.

---

## Step 2: Validate Artifact Chain

Verify that the Security Review uses current versions of:

- User Story;
- Specification;
- Specification Review;
- API Design;
- Database Design;
- Impact Analysis;
- Implementation Plan;
- Plan Review;
- Implementation Report;
- Implementation Verification.

If a material input is stale or superseded:

1. set result to BLOCKED;
2. list stale artifacts;
3. recommend regeneration of dependent artifacts;
4. stop before positive approval.

---

## Step 3: Determine Security-Relevant Scope

From the Story, Specification, designs, Impact Analysis, Implementation Plan,
and changed files, identify:

- exposed endpoints;
- authentication changes;
- authorization changes;
- password or token handling;
- personal data;
- persistence changes;
- validation changes;
- error handling changes;
- configuration changes;
- dependency changes;
- logging changes;
- external integrations.

Create a Story-specific security checklist.

---

## Step 4: Identify Assets and Trust Boundaries

List relevant assets:

- user credentials;
- password hashes;
- email addresses;
- account identifiers;
- role information;
- database content;
- configuration;
- session or authentication state.

Identify relevant trust boundaries:

- external client to Controller;
- Controller to Service;
- Service to Repository;
- application to database;
- application to external system;
- developer environment to repository;
- MCP tool to external service.

Do not invent boundaries that are unrelated to the Story.

---

## Step 5: Inspect Dependency Changes

Compare current dependencies with the approved plan.

Check for:

- newly added dependencies;
- unexpected transitive capabilities;
- unnecessary security libraries;
- obsolete or duplicate components;
- development-only dependencies used at runtime;
- test dependencies leaking into production configuration.

New dependencies without explicit approval are findings.

If dependency vulnerability tooling is unavailable, record that vulnerability
database checking was not performed.

Do not claim that dependencies are vulnerability-free without evidence.

---

## Step 6: Review ASP.NET Core Security Configuration

Inspect relevant `Security` namespace and authentication/authorization
configuration.

Verify:

- endpoint access rules;
- authentication requirements;
- authorization requirements (fallback policy / `[Authorize]`);
- default deny behavior where required;
- explicit `[AllowAnonymous]` endpoints;
- CSRF/antiforgery handling;
- cookie session behavior when relevant;
- password hasher configuration;
- authentication/authorization middleware ordering;
- development-only exceptions;
- error handling.

Flag broad rules such as unrestricted access when not explicitly approved.

Do not assume an endpoint is protected because authentication middleware is
present.

---

## Step 7: Review Password Handling

When the Story handles passwords, verify:

- plaintext password is accepted only in the request boundary;
- plaintext password is not persisted;
- plaintext password is not logged;
- plaintext password is not returned;
- password hash is not returned;
- approved `IPasswordHasher` (BCrypt.Net-Next) is used;
- hasher configuration is not a no-op;
- password policy matches approved requirements;
- invalid password is rejected before persistence;
- DTO and entity serialization cannot expose credential fields;
- test fixtures do not introduce committed real credentials.

For Customer Portal, use the password mechanism defined by approved security
conventions.

Do not invent a policy if the policy is absent.

---

## Step 8: Review Sensitive Data Exposure

Inspect:

- response DTOs;
- entity serialization;
- exception responses;
- log statements;
- debug output;
- implementation reports;
- telemetry;
- database previews;
- test output.

Verify that sensitive data is not exposed.

Sensitive fields may include:

- password;
- password hash;
- token;
- authorization header;
- database credential;
- secret key;
- internal security state.

Flag broad serialization of persistence entities.

---

## Step 9: Review Input Validation

Verify:

- validation is defined for external input;
- validation is active at runtime;
- validation is server-side;
- malformed input is rejected;
- length constraints are explicit;
- required fields are enforced;
- email validation follows approved behavior;
- unexpected fields do not create unsafe state;
- validation errors do not reveal internal details.

Validation annotations alone are not sufficient if framework validation is not
activated.

---

## Step 10: Review Account and Identity Rules

When the Story creates or modifies user accounts, verify:

- email uniqueness;
- case-sensitivity behavior;
- default role;
- default account state;
- disabled account behavior;
- duplicate registration behavior;
- ownership boundaries;
- identifier exposure.

Confirm that implementation follows approved business rules.

Do not infer identity policy from framework defaults.

---

## Step 11: Review Authorization

For every affected operation, determine:

- whether the operation is public;
- whether authentication is required;
- which role or principal may invoke it;
- whether ownership checks are required;
- whether administrative operations are isolated;
- whether service methods can bypass endpoint authorization.

Look for insecure direct object access risks where identifiers are accepted.

Authorization findings are Critical when unauthorized users can access or
modify protected data.

---

## Step 12: Review API Security

Compare implementation with approved API design.

Verify:

- only approved endpoints exist;
- HTTP methods are appropriate;
- request fields are restricted;
- response fields are minimized;
- error responses do not leak internal information;
- duplicate and validation behavior does not reveal unnecessary data;
- authentication and authorization declarations match implementation;
- content types are constrained when required.

Undocumented endpoints or response fields are findings.

---

## Step 13: Review Error Handling

Verify that error responses do not expose:

- stack traces;
- SQL statements;
- database paths;
- entity internals;
- package or class names;
- password hashes;
- tokens;
- filesystem paths;
- secret configuration.

Check whether different error responses unintentionally reveal account
existence when product requirements prohibit that behavior.

Do not redefine the approved duplicate-email response during review.

If account enumeration policy is not defined and materially relevant, create
an Open Decision.

---

## Step 14: Review Persistence Security

Compare implementation and schema evidence with approved DB design.

Verify:

- password fields cannot contain plaintext by design and behavior;
- sensitive columns have appropriate length and nullability;
- email uniqueness is enforced at the appropriate layer;
- constraints are explicit;
- database files are stored in the approved location;
- generated SQLite files are excluded from Git;
- database path/connection string is not exposed in responses or logs;
- schema behavior is documented;
- destructive schema recreation (`EnsureCreated`/`EnsureDeleted` against the
  file database) is not enabled without approval.

For the training repository, verify that SQLite is file-based when required.

Do not accept an `EnsureCreated()`/`EnsureDeleted()` shortcut against the file
database as a substitute for explicit EF Core migrations merely because the
application starts successfully.

---

## Step 15: Review Database Configuration

Inspect all active configuration profiles.

Verify:

- no database admin/diagnostic UI is exposed, in any profile;
- no such UI is exposed through broad authorization rules;
- the connection string uses the approved file-based SQLite location;
- credentials are not committed when they should be environment-specific;
- generated database files are ignored by Git;
- schema initialization behavior is explicit (EF Core Migrations only);
- development settings cannot accidentally become default runtime settings.

Treat an externally reachable database admin/diagnostic UI as a Critical
finding.

---

## Step 16: Review Logging and Telemetry

Inspect application logs and configured observability hooks.

Verify that logs and telemetry do not contain:

- passwords;
- password hashes;
- authorization headers;
- tokens;
- full request bodies containing credentials;
- database credentials;
- unnecessary personal information.

Tool usage logs should record metadata such as tool name, timestamp, status,
input size, and response size rather than full sensitive payloads.

If current PostToolUse telemetry stores full tool input or response, flag the
risk and recommend redaction or metadata-only logging.

---

## Step 17: Review Secrets and Repository Hygiene

Inspect relevant tracked and untracked files.

Look for:

- tokens;
- authorization headers;
- hardcoded passwords;
- private keys;
- `.env` files;
- local database credentials;
- generated SQLite database files;
- copied MCP configuration containing secrets;
- logs containing credentials.

Do not copy suspected secret values into the Security Review.

Record only:

- file path;
- secret category;
- remediation requirement.

Potential live secrets are Critical findings and require human action.

---

## Step 18: Review Security Tests

Verify that tests cover relevant security behavior.

For registration, expected tests may include:

- password is hashed before persistence;
- plaintext password is not stored;
- password hash is not included in response;
- invalid password is rejected;
- invalid email is rejected;
- duplicate email behavior is enforced;
- unapproved fields are not returned;
- database admin/diagnostic UI is not publicly accessible when applicable;
- endpoint access matches approved public or protected status.

Assess test quality.

A test that only checks that a method was called may not prove the security
property.

---

## Step 19: Review Abuse Cases

For the active Story, identify a small set of realistic misuse cases.

For registration, consider:

- repeated duplicate registrations;
- malformed email input;
- oversized input;
- weak or invalid password;
- unexpected request fields;
- attempts to submit role or account-state fields;
- response inspection for sensitive fields;
- unauthorized access to administrative behavior.

Only include abuse cases relevant to approved scope.

Rate limiting and denial-of-service protections should not be invented as
mandatory requirements unless defined by approved artifacts.

If materially needed but undefined, record an Open Decision or recommendation.

---

## Step 20: Review Plan and Implementation Deviations

Compare actual security behavior with:

- Specification;
- security conventions;
- API design;
- DB design;
- Implementation Plan;
- Implementation Report;
- Implementation Verification.

Identify:

- undocumented security behavior;
- omitted controls;
- permissive defaults;
- unapproved changes;
- security-relevant supporting changes;
- false or incomplete implementation claims.

---

## Step 21: Classify Findings

Classify each finding as:

### Critical

Blocks progression.

Examples:

- plaintext password persistence;
- password or hash exposure;
- exposed database admin/diagnostic UI without approval;
- unrestricted access to protected functionality;
- committed token or credential;
- missing required authorization;
- security-sensitive Open Decision implemented as an assumption;
- active test bypass hiding insecure behavior.

### Major

Requires correction before Reconciliation or Pull Request.

Examples:

- missing security test;
- weak input validation;
- undocumented security configuration;
- incomplete error sanitization;
- missing persistence constraint;
- unnecessary sensitive logging;
- unapproved dependency;
- generated database files tracked by Git.

### Minor

Does not immediately block progression but should be addressed or documented.

Examples:

- low-risk information exposure;
- incomplete security documentation;
- non-sensitive verbose logging;
- maintainability issue in security configuration;
- defense-in-depth recommendation not required by current Acceptance Criteria.

### Informational

Useful observation with no required correction.

Informational observations must not inflate severity.

---

## Step 22: Assign Security Category

For every finding assign one category:

- AUTHENTICATION;
- AUTHORIZATION;
- PASSWORD_HANDLING;
- DATA_EXPOSURE;
- INPUT_VALIDATION;
- API_SECURITY;
- ERROR_HANDLING;
- PERSISTENCE;
- CONFIGURATION;
- DEPENDENCY;
- LOGGING;
- SECRET_MANAGEMENT;
- TEST_COVERAGE;
- REPOSITORY_HYGIENE;
- OTHER.

---

## Step 23: Determine Loop-Back Target

`stage-map.yaml` defines two loop-backs for `SECURITY_REVIEW`:

| Root cause | verdict | loop_back_stage | key |
|---|---|---|---|
| Correct artifacts but insecure code | `CHANGES_REQUIRED` | `IMPLEMENTATION` | `changes_required` |
| Approved API/security design exposes a prohibited field or permits unsafe access | `CHANGES_REQUIRED` | `API_DESIGN` | `invalid_security_design` |

For any other upstream root cause (missing password policy / authorization
requirement → `SPECIFICATION`; missing security test → `TEST_WRITING`; omitted
security component → `DB_DESIGN`; missing security step → `SPECIFICATION`; a
change since verification → `IMPLEMENTATION_VERIFICATION`), return
`verdict: BLOCKED` and name the responsible stage in `blocking_issues` for the
orchestrator / a human to route. Do not route every finding to `IMPLEMENTATION`.

---

## Step 24: Create Security Review Report

Create the `security_review` artifact at its registry path
(`docs/reviews/security/{story_id}-security-review.md`), front matter per
`docs/workflow/artifact-schema.md` (`artifact_type: security_review`).

Do not modify source code, tests, or approved artifacts. Do not update workflow
state. Do not create a commit or Pull Request.

---

# Security Review Report Format

## Front Matter

Shared block from `docs/workflow/artifact-schema.md`
(`artifact_type: security_review`), plus: `critical_findings`,
`major_findings`, `minor_findings`, `informational_findings`,
`security_sensitive` (bool), `runtime_checks` (`FULL` / `PARTIAL` / `NONE`),
`semantic_analysis` (`IDEA_MCP` / `TEXT_FALLBACK` / `UNAVAILABLE`).
`created_at` / `updated_at` are runtime timestamps.

Illustrative (dates are examples only):

    ---
    artifact_type: security_review
    story: US-001
    version: 1
    status: DRAFT
    created_at: <runtime>
    updated_at: <runtime>
    produced_by: security-reviewer
    inputs:
      - path: docs/evidence/US-001-implementation-report.md
        version: 1
      - path: docs/verification/US-001-implementation-verification.md
        version: 1
      - path: docs/specifications/US-001-spec.md
        version: 1
    supersedes: null
    critical_findings: 1
    major_findings: 2
    minor_findings: 1
    informational_findings: 0
    security_sensitive: true
    runtime_checks: PARTIAL
    semantic_analysis: IDEA_MCP
    ---

## 1. Executive Summary

Summarize:

- overall security result;
- principal security controls;
- Critical and Major risks;
- review limitations;
- recommended next action.

## 2. Reviewed Artifacts

List exact artifact paths and versions.

## 3. Security-Relevant Scope

Describe:

- exposed functionality;
- protected assets;
- trust boundaries;
- affected security components.

## 4. Environment and Tools

Record:

- .NET version;
- ASP.NET Core version;
- active profile;
- database mode;
- review tools;
- semantic capabilities;
- runtime capabilities;
- unavailable checks.

Do not record secrets.

## 5. Authentication Review

Record:

- applicable requirements;
- implementation evidence;
- tests;
- findings.

## 6. Authorization Review

Record:

- endpoint access;
- role checks;
- ownership checks;
- service-level boundaries;
- findings.

## 7. Password and Credential Handling

Record:

- request handling;
- policy enforcement;
- hashing;
- persistence;
- serialization;
- logging;
- tests;
- findings.

## 8. Sensitive Data Exposure

Record review results for:

- responses;
- entities;
- DTOs;
- logs;
- exceptions;
- reports;
- telemetry.

## 9. Input Validation

Record:

- constraints;
- runtime activation;
- negative scenarios;
- oversized or malformed input;
- findings.

## 10. API Security

Record:

- exposed endpoints;
- approved public access;
- protected operations;
- request and response restrictions;
- error behavior;
- findings.

## 11. Persistence Security

Record:

- sensitive fields;
- schema constraints;
- uniqueness;
- nullability;
- database location;
- generated files;
- findings.

## 12. Database and Application Configuration

Record:

- SQLite mode (file vs. in-memory);
- database admin/diagnostic UI state;
- schema behavior;
- profiles;
- secrets;
- unsafe defaults;
- findings.

## 13. Logging and Telemetry

Record:

- sensitive logging review;
- hook telemetry review;
- payload retention;
- redaction controls;
- findings.

## 14. Dependencies

Record:

- added dependencies;
- approval status;
- review limitations;
- vulnerability scanning evidence when available;
- findings.

Do not state that dependencies are secure when vulnerability scanning was not
performed.

## 15. Security Test Coverage

Map security requirements and abuse cases to tests.

## 16. Abuse Case Review

For every reviewed abuse case record:

- scenario;
- expected protection;
- evidence;
- status;
- finding.

## 17. Repository Hygiene

Record:

- secret-like files;
- generated SQLite files;
- ignored files;
- unsafe local configuration;
- findings.

## 18. Deviations

List deviations between approved security requirements and actual
implementation.

## 19. Findings

For each finding provide:

- ID;
- severity;
- category;
- affected file or artifact;
- observed evidence;
- expected security behavior;
- risk;
- required correction;
- loop-back target;
- verification required after correction.

Do not include actual secret values.

## 20. Positive Controls

List security controls that were independently observed and verified.

## 21. Open Decisions

List unresolved security decisions.

If none exist, state:

    No blocking security Open Decisions were identified.

## 22. Review Limitations

List checks that were not performed and explain why.

## 23. Verdict Rationale

Explain the verdict (see Result Envelope). Do not use `PROCEED_TO_*` /
`RETURN_TO_*` labels — they are retired. When a human security decision is
needed (risk acceptance, exception, suspected credential compromise), return
`verdict: BLOCKED` and say so explicitly in `blocking_issues`.

---

# Result Envelope

Return exactly this; the story-orchestrator records the transition — this Skill
does not update `workflow-state.yaml`:

```yaml
result:
  verdict: PASS | CHANGES_REQUIRED | BLOCKED
  stage: SECURITY_REVIEW
  story: <StoryId>
  artifact_status: APPROVED        # of the security_review artifact itself
  artifacts:
    - docs/reviews/security/<StoryId>-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null            # or IMPLEMENTATION / API_DESIGN
  blocking_issues: []
  non_blocking_findings: []
```

## PASS

Use only when: `implementation_report` records a green build and green tests; no
Critical or Major findings; required security tests pass; security-sensitive
Acceptance Criteria are verified; no blocking security Open Decision. Minor /
Informational findings go in `non_blocking_findings`. The orchestrator advances
to `HUMAN_PR_APPROVAL`.

## CHANGES_REQUIRED

Use when there is a Critical/Major finding the implementation (or the API/security
design) can fix:

- insecure code with correct artifacts → `loop_back_stage: IMPLEMENTATION`;
- the approved contract/design itself is unsafe →
  `loop_back_stage: API_DESIGN`.

## BLOCKED

Use when: a mandatory security requirement is undefined; a required artifact is
missing/stale; the implementation cannot be inspected; the environment prevents
meaningful review; a security-sensitive Open Decision is unresolved; a human
security decision is required; or the root cause is an upstream artifact other
than the API/security design (name the stage in `blocking_issues`).

---

# Prohibited Actions

This Skill must not:

- edit production code;
- edit tests;
- alter User Story or Acceptance Criteria;
- alter Specification;
- alter API or database design;
- alter Implementation Plan;
- resolve security decisions;
- accept security risk;
- expose secret values in reports;
- execute destructive database operations;
- create database connections without approval;
- expose a database admin/diagnostic UI;
- weaken ASP.NET Core security configuration;
- disable CSRF/antiforgery or authentication without approved requirements;
- disable or weaken security tests;
- suppress security findings;
- update workflow state automatically;
- commit or push files;
- create or merge a Pull Request;
- mark the Story `COMPLETED`;
- claim penetration testing was performed when the Skill only conducted code,
  configuration, and test review.

---

# Failure Handling

If `implementation_report` does not record a green build and green tests:

1. Create the `security_review` artifact referencing the failed implementation.
2. Return `verdict: CHANGES_REQUIRED` with `loop_back_stage: IMPLEMENTATION`
   (or `BLOCKED` if the failure is not something IMPLEMENTATION can fix).
3. Do not issue a positive security result.

If a potential live secret is found:

1. Do not display or copy the secret.
2. Record the affected file and secret category.
3. Create a Critical finding.
4. Recommend immediate human intervention.
5. Recommend credential rotation without claiming it has occurred.
6. Stop actions that could further expose the value.

If required runtime verification cannot be performed:

1. record the limitation;
2. continue static and configuration review where safe;
3. do not mark unverified controls as confirmed;
4. return `verdict: BLOCKED` (cannot evaluate) or `CHANGES_REQUIRED` (a concrete
   correctable insecurity was still found) according to impact.

If IDEA MCP is unavailable:

1. use built-in tools and `dotnet` CLI evidence;
2. record unavailable semantic checks;
3. lower confidence where necessary;
4. avoid unsupported conclusions.

If vulnerability scanning is unavailable:

1. inspect dependency changes;
2. record that vulnerability database analysis was not performed;
3. do not claim dependency safety;
4. recommend an approved scanner when organizational policy requires one.

---

# Observability

Do not disable or bypass configured telemetry hooks.

Use telemetry to understand which tools and external capabilities participated
in implementation and verification.

Review telemetry for potential sensitive payload capture.

Preferred telemetry fields include:

- timestamp;
- session identifier;
- tool name;
- success or failure;
- input byte size;
- response byte size;
- duration when available.

Avoid logging full tool inputs and responses for security-sensitive tools.

Never store:

- passwords;
- password hashes;
- authorization tokens;
- authorization headers;
- database credentials;
- secret environment values;
- private keys;
- full credential-bearing request payloads.

If existing telemetry stores sensitive payloads, create a finding.

---

# Human Review Boundary

This Skill provides an engineering security review.

It cannot:

- accept business risk;
- approve exceptions to organizational policy;
- replace human code review;
- replace specialized security assessment;
- approve production deployment;
- approve merge;
- waive Critical or Major findings.

Return `verdict: BLOCKED` with an explicit "human security decision required"
note in `blocking_issues` when:

- a security exception is requested;
- risk acceptance is needed;
- a sensitive architectural decision remains open;
- available tooling cannot provide sufficient evidence;
- suspected credential compromise exists;
- organizational security policy requires specialist review.

The orchestrator surfaces this to a human; it is not a stage transition the
Skill routes.

---

# Completion Criteria

Security Review is complete only when:

- active Story and workflow stage are resolved;
- artifact versions are validated;
- security-relevant scope is identified;
- assets and trust boundaries are documented;
- authentication is reviewed when relevant;
- authorization is reviewed when relevant;
- password handling is reviewed when relevant;
- sensitive data exposure is reviewed;
- input validation is reviewed;
- API security is reviewed;
- persistence security is reviewed;
- database and application configuration are reviewed;
- logging and telemetry are reviewed;
- dependencies are reviewed within available capabilities;
- security tests are evaluated;
- relevant abuse cases are evaluated;
- repository hygiene is inspected;
- deviations are documented;
- findings are classified;
- loop-back targets are assigned;
- limitations are explicit;
- Security Review artifact is saved;
- review result is explicit;
- recommended next stage is explicit.

Finish with a concise summary containing:

- Security Review result;
- Critical finding count;
- Major finding count;
- principal risks;
- verified positive controls;
- review limitations;
- Security Review artifact path;
- recommended next stage.