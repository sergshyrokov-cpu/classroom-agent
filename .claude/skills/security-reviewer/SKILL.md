---
name: security-reviewer
description: >
  Performs an independent security review of the active User Story
  implementation in the classroom-agent .NET solution. Reviews the change
  against security-conventions.md (SC-1…SC-13): roles and authentication,
  authorization, read-only mode, the service-account key, Google access,
  sensitive data, validation, persistence, configuration, dependencies,
  logging, audit and outbound data flows. Use at the SECURITY_REVIEW stage,
  after IMPLEMENTATION and before HUMAN_PR_APPROVAL.
---

# Purpose

Perform an independent, evidence-based security review of the active User
Story implementation.

The Skill determines whether the implementation introduces unacceptable
security risks or violates approved security requirements.

The Skill focuses on security properties rather than general functional
correctness.

The Skill does not assume that successful compilation or passing tests
automatically imply secure behavior.

The Skill produces a Security Review artifact.

The Skill does not modify production code, rewrite tests, accept security
risk, commit, or mark the Story complete.

This system stores personal data of school students, potentially minors, and
reads a school's Google Workspace through a service account with domain-wide
delegation. Treat every finding as production severity
(`security-conventions.md`, "Why this is not a training-project policy").

---

# Position in the Workflow

Canonical workflow: `docs/workflow/stage-map.yaml`. Relevant slice:

    IMPLEMENTATION
    → SECURITY_REVIEW            (this Skill)
    → HUMAN_PR_APPROVAL
    → COMPLETED

This is the lightweight workflow variant: SECURITY_REVIEW is the **only**
automated reviewer, and it runs directly after IMPLEMENTATION. There is no
verification stage ahead of it and no reconciliation after it, so this review
is the last automated check before a human commits.

This Skill owns only the `SECURITY_REVIEW` stage. Loop-back
(`stage-map.yaml`): `changes_required` → `IMPLEMENTATION`,
`invalid_security_design` → `API_DESIGN`.

---

# Security Review Scope

The review covers security behavior introduced, modified, or affected by the
active User Story.

Review areas include:

- roles and authentication;
- authorization and anonymous access;
- read-only mode;
- password and credential handling;
- the service-account key and Google API access;
- sensitive data exposure;
- request validation;
- exception and error handling;
- persistence constraints;
- configuration;
- logging and audit;
- the Control Plane channel and outbound data flows;
- dependency changes;
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
- implementation has completed and an `implementation_report` exists;
- security review is the current workflow stage;
- a previous Security Review rejected the implementation and security fixes
  have been applied.

Typical requests:

- Perform Security Review for the active User Story.
- Review US-001 implementation for security issues.
- Check whether the implementation is safe to commit.
- Re-run Security Review after security fixes.

---

# When Not To Use

Do not use this Skill:

- before implementation exists;
- to define product security policy;
- to invent missing security requirements;
- to implement security fixes;
- to generate the initial test suite;
- to perform general code style review;
- to accept security risk on behalf of a human;
- to commit, push or approve the Story;
- to change workflow state automatically;
- as a substitute for professional penetration testing.

---

# Independent Review Principle

The Security Reviewer must remain independent from the Implementor.

The Implementor answers:

    Which security requirements were implemented?

The Security Reviewer answers:

    Which security properties can be independently verified, and which risks
    remain?

Do not trust the Implementation Report without checking the underlying
implementation and available evidence.

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
  available in this variant
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
security configuration, `.gitignore`.

Read telemetry only when needed: `docs/hooks/tool-usage.jsonl`, `docs/evidence/`.

Read architecture references:

- docs/architecture/security-conventions.md  ← the checklist source
- docs/architecture/architecture.md
- docs/architecture/package-map.md
- docs/architecture/api-conventions.md
- docs/architecture/persistence-conventions.md
- docs/architecture/testing-conventions.md
- docs/architecture/deployment-conventions.md

Read product context:

- docs/product/business-rules.md
- docs/product/business-glossary.md
- docs/product/non-functional-requirements.md

Do not load unrelated Story artifacts unless a concrete security dependency
requires them.

---

# Artifact Authority

Use the order of authority from `AGENTS.md`:

1. `trebovaniya.md`
2. Active User Story and Acceptance Criteria
3. Approved Specification
4. Resolved Open Decisions
5. Approved API and database designs

Below them: `security-conventions.md` and the other conventions, then current
code and configuration, then Implementation Report claims.

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

The Specification or resolved Open Decisions must define security-relevant
behavior when the Story handles:

- passwords or credentials;
- authentication;
- authorization or roles;
- account state;
- personal data of students or staff;
- the service-account key or Google access;
- the Control Plane channel;
- external input;
- externally accessible endpoints.

If material security behavior is undefined, do not invent policy. Return
`verdict: BLOCKED`; name `SPECIFICATION` in `blocking_issues` (an undefined
security requirement is an upstream defect, not something Security Review can
route to `IMPLEMENTATION`).

## Architecture Documentation

`docs/architecture/security-conventions.md` must exist and contain meaningful
guidance. Relevant architecture, API, and persistence convention files must
also be available.

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

Security-sensitive Open Decisions are blockers when they affect the Story.

Examples include:

- password or lockout policy;
- authorization rules or a permission-matrix cell not in `trebovaniya.md` §2;
- an anonymous endpoint not on the SC-4 list;
- a new Google scope or a new outbound data flow;
- sensitive data retention;
- error response information;
- audit requirements for a new action.

## Working Tree

Inspect the current Git state.

Identify:

- modified files;
- untracked files;
- deleted files;
- generated database files or `.xlsx` exports;
- configuration files;
- secret-like files;
- unrelated changes.

Do not modify or remove existing changes.

---

# Security Review Principles

## Requirements Before Assumptions

Security behavior must come from approved artifacts.

Do not invent password complexity, account lockout, token expiration, or other
policies during review (for the Owner and the Dean — SC-2).

If a necessary security decision is missing, report it as a blocker.

## Deny by Default

Nothing is anonymous unless it is on the SC-4 closed list.

## Least Privilege

Accounts, endpoints, Google scopes, database access, and configuration should
receive only the permissions required by the current Story.

## Defense in Depth

Do not rely on a single control when multiple layers are appropriate.

Examples:

- request validation and database constraints;
- authorization policy on the endpoint and the read-only check in the use case;
- password hashing and response DTO isolation;
- secret store and Git ignore rules.

## No Sensitive Data Exposure

Credentials, the service-account key and personal data must not be exposed
through:

- API responses or Razor views;
- logs;
- audit rows;
- exceptions;
- exports beyond what the Story approves;
- telemetry;
- committed files.

## Verify Runtime Effect

The presence of an attribute or configuration class does not prove that the
control is active.

Prefer runtime, test, configuration, or framework-wiring evidence.

## Evidence Over Confidence

A reviewer statement such as:

    The implementation appears secure.

is not sufficient evidence.

---

# Threat-Oriented Review Model

For each externally observable capability, consider:

- who can invoke it (Owner, Admin, Dean, anonymous, background service,
  installation, Control Plane);
- what input is accepted;
- what data is read;
- what data is written;
- what sensitive data exists;
- what trust boundary is crossed;
- what happens on invalid input;
- what happens on repeated input;
- what happens on unauthorized input;
- what happens in read-only mode;
- what information is revealed by errors;
- what state can be changed;
- what abuse is possible.

Use practical, Story-scoped abuse cases.

Do not produce speculative enterprise threat models unrelated to the active
Story.

---

# Tooling

Use the built-in file reading and search tools, Git, and the `dotnet` CLI.
Text search gives no semantic certainty: when a conclusion depends on call
paths (for example whether a use case can reach a Google port in read-only
mode), confirm it with a test or the compiler, or record the limitation.

Commands (from the repository root, see `AGENTS.md`):

    dotnet build ClassroomAgent.sln
    dotnet test ClassroomAgent.sln
    dotnet list package --vulnerable

Integration tests need a running Docker daemon (Testcontainers, TC-2).

Record actual results.

Do not start or expose the application on an externally accessible interface.

Do not connect to any database other than the Testcontainers instances the
tests start. Do not execute destructive SQL. Do not retrieve or copy personal
data.

---

# Security Review Workflow

## Step 1: Resolve Active Story

Read workflow state.

Record:

- Story ID;
- current stage;
- implementation attempt;
- Security Review attempt;
- relevant artifact versions.

Confirm that SECURITY_REVIEW is the current permitted stage.

---

## Step 2: Validate Artifact Chain

Verify that the Security Review uses current versions of:

- User Story;
- Specification;
- Open Decisions;
- API Design;
- Database Design;
- test artifacts;
- Implementation Report.

If a material input is stale or superseded:

1. set result to BLOCKED;
2. list stale artifacts;
3. recommend regeneration of dependent artifacts;
4. stop before positive approval.

---

## Step 3: Determine Security-Relevant Scope

From the Story, Specification, designs, the Implementation Report change set,
and the actual changed files, identify:

- exposed endpoints and Razor pages, per host;
- authentication changes;
- authorization changes and anonymous endpoints;
- password or credential handling;
- personal data read, stored, shown or exported;
- write use cases (subject to read-only mode);
- Google port usage;
- Control Plane channel or `Contracts` changes;
- persistence changes;
- validation changes;
- error handling changes;
- configuration changes;
- dependency changes;
- logging and audit changes.

Select the SC-1…SC-13 items this scope touches (Step 6).

---

## Step 4: Identify Assets and Trust Boundaries

List relevant assets:

- personal data of students and staff (names, emails, grades, Meet
  participation);
- Dean and Owner password hashes;
- the service-account key and the reference to it;
- session cookies and the OAuth sign-in result;
- `AllowedAdmin` entries and `Installation` state;
- `WorkspaceConnection` (domain, impersonation user);
- audit rows.

Identify relevant trust boundaries:

- browser to the installation host (public HTTPS);
- the Owner's browser to the Control Plane (private network only, over HTTPS —
  DC-6);
- Controller or Razor page to Application use case;
- Application to ports: Google, Control Plane, secret store, report renderer;
- installation to Control Plane service channel (private network, SC-9; HTTPS to
  the Control Plane, HTTP to the installation's private port — DC-6);
- application to PostgreSQL;
- developer environment to repository.

Do not invent boundaries that are unrelated to the Story.

---

## Step 5: Inspect Dependency Changes

Compare referenced packages before and after the change.

Check for:

- newly added packages without an approved Open Decision;
- development-only packages used at runtime;
- test packages leaking into production projects;
- a new project reference that breaks `package-map.md` (for example
  `Application → Infrastructure` or `ControlPlane → Domain`).

Run `dotnet list package --vulnerable` when available; otherwise record that
vulnerability database checking was not performed.

Do not claim that dependencies are vulnerability-free without evidence.

---

## Step 6: Project Security Checklist

For every SC item the scope touches, verify the implementation against
`security-conventions.md` and record evidence (file + symbol, test, or
configuration). The file is the rule; the lines below are only what to look for.

| SC | Verify |
|---|---|
| SC-1 Roles | No Teacher or Student in `AppRole`, a policy or a seed; no permission cell beyond `trebovaniya.md` §2. |
| SC-2 Authentication | Identity hashes Dean and Owner passwords; an Admin has no local password, column or reset flow; a Dean login is a domain email; first-run setup requires the one-time code, printed to the console only; the Owner login format, the password and lockout policy, the refusal message and its disabled-Dean exception, the temporary-password rules, cookie attributes, HTTPS redirection and HSTS per host and port all match SC-2. |
| SC-3 AllowedAdmin | Checked by a Control Plane call on **every** Admin login; no local copy or cached answer; login refused when the Control Plane does not answer. |
| SC-4 Authorization | Every endpoint and page declares a policy; a fallback policy requires an authenticated user; anonymous access only for the closed list; antiforgery validation is global for POST, PUT, PATCH and DELETE, a refusal is the translated error page or the API-6 body, and exemptions match the closed exemption list; no state-changing action on GET other than the Google OAuth callback. |
| SC-5 Read-only mode | Every write use case refuses with `409` in Application; only BR-026 service writes run; no Google port is called. |
| SC-6 No DB UI | No database browser, SQL console or diagnostic endpoint; developer exception page only in local development. |
| SC-7 Key | The key is never in a database, a UI, a request or the repository; only its reference sits in configuration; Data Protection keys sit in each host's own directory as SC-7 fixes. |
| SC-8 Google | Only read-only scopes from `trebovaniya.md` §6; impersonates the technical account; the Admin's OAuth session never calls a data API; permission failures are neither retried nor swallowed. |
| SC-9 Channel | A `WorkspaceConnection` whose domain differs from the `Installation` domain is refused; the service channel and Control Plane are not publicly reachable. |
| SC-10 Hygiene | No internals in responses; no personal data in logs; rejected payloads not logged. |
| SC-11 Audit | Every audited action the Story introduces writes an `AuditEvent` with the required fields and no personal data; no update or delete path outside the retention purge. |
| SC-12 Owner | `Contracts` carries no teaching data; `ControlPlane` does not reference `Domain`; no school statistics reach the Control Plane. |
| SC-13 Outbound | School data goes only to Google and the Control Plane channel. |

A finding `security-conventions.md` labels Critical is Critical here.

---

## Step 7: Review ASP.NET Core Security Configuration

Inspect each host's `Security` namespace and authentication/authorization
wiring.

Verify:

- fallback policy and declared policies;
- explicit `[AllowAnonymous]` endpoints match the SC-4 list;
- antiforgery validation is registered globally on both hosts for POST, PUT,
  PATCH and DELETE — Razor pages and REST controllers — anonymous forms are not
  exempt, and every such endpoint outside the exemption list is actually
  validated, however it is declared (SC-4);
- every `[IgnoreAntiforgeryToken]` or equivalent exemption matches the SC-4
  exemption list (Critical otherwise);
- REST calls from the UI send the token in the `RequestVerificationToken`
  header; a refusal returns the API-6 body to REST and the translated error page
  to a Razor form (API-7, SC-4);
- cookie attributes, HTTPS redirection and HSTS match SC-2 per host and port;
  the Data Protection key ring matches SC-7;
- no state-changing action is reachable by GET other than the Google OAuth
  callback, and export is POST (API-3, API-4);
- Identity password, lockout and login options, the refusal message and the
  disabled-account message match SC-2 for the Owner and the Dean;
- authentication/authorization middleware ordering;
- development-only exceptions;
- error handling (the single `IExceptionHandler`, AD-9).

Do not assume an endpoint is protected because authentication middleware is
present.

---

## Step 8: Review Sensitive Data Exposure

Inspect:

- response DTOs and Razor view models (AD-8);
- entity serialization;
- exception responses;
- log statements;
- audit rows;
- exports;
- implementation reports;
- telemetry;
- test fixtures (synthetic data only, TC-4).

Verify that credentials, the service-account key and personal data are not
exposed beyond what the Story approves.

Flag broad serialization of persistence entities.

---

## Step 9: Review Input Validation

Verify:

- validation is defined for external input — request bodies, query and route
  parameters, uploaded files, and data returned by Google APIs;
- validation is active at runtime (`[ApiController]`, Data Annotations);
- validation is server-side;
- malformed input is rejected with `400` and `fieldErrors` (API-6);
- length constraints are explicit;
- required fields are enforced;
- unexpected fields do not create unsafe state (for example a role or an
  account state in a request body);
- validation errors do not reveal internal details.

Validation annotations alone are not sufficient if framework validation is not
activated.

---

## Step 10: Review Authorization

For every affected operation, determine:

- whether the operation is anonymous, and if so whether SC-4 lists it;
- which role may invoke it, per `trebovaniya.md` §2;
- whether a service method or background path can bypass endpoint
  authorization;
- whether identifiers accepted from the request allow access to data of another
  installation, course or account.

Authorization findings are Critical when unauthorized users can access or
modify protected data.

---

## Step 11: Review API Security

Compare implementation with approved API design.

Verify:

- only approved endpoints exist;
- HTTP methods are appropriate;
- request fields are restricted;
- response fields are minimized;
- error responses do not leak internal information;
- authentication and authorization declarations match implementation;
- content types are constrained when required.

Undocumented endpoints or response fields are findings.

---

## Step 12: Review Error Handling

Verify that error responses do not expose anything SC-10 forbids: stack traces,
SQL, entity or namespace names, file paths, connection strings,
service-account identifiers, raw Google API errors.

Check whether different error responses unintentionally reveal account
existence when the approved requirements prohibit that behavior.

If account enumeration policy is not defined and materially relevant, create
an Open Decision.

---

## Step 13: Review Persistence and Configuration

Compare implementation and schema evidence with the approved database design
and `persistence-conventions.md`.

Verify:

- no password, key or key reference is stored where PC-9 / SC-7 forbid it;
- sensitive columns have appropriate length and nullability;
- constraints are explicit;
- schema changes come only from EF Core migrations — no `EnsureCreated()` /
  `EnsureDeleted()` (PC-2);
- the Control Plane and an installation use separate databases (AD-1);
- connection strings with passwords are not committed;
- generated database files and `.xlsx` exports are ignored by Git;
- development settings cannot accidentally become default runtime settings.

---

## Step 14: Review Logging, Audit and Telemetry

Verify that application logs (DC-10) and audit rows (SC-11) contain no:

- passwords or password hashes;
- tokens or cookies;
- the service-account key;
- full request bodies;
- names, emails or grades.

Verify that every audited action the Story introduces is written and tested.

Hook telemetry (`docs/hooks/tool-usage.jsonl`) must record metadata only and
stay git-ignored (SC-10). If it stores full tool input or response, flag it.

---

## Step 15: Review Secrets and Repository Hygiene

Inspect relevant tracked and untracked files.

Look for:

- service-account keys, client secrets, tokens;
- hardcoded passwords;
- connection strings with passwords;
- `.env` files;
- generated database files or `.xlsx` exports;
- logs containing credentials.

Never open, print or quote `google_credentials.json` or
`dac-classroom-agent-*.json` (AGENTS.md); only confirm they remain git-ignored.

Do not copy suspected secret values into the Security Review.

Record only:

- file path;
- secret category;
- remediation requirement.

Potential live secrets are Critical findings and require human action.

---

## Step 16: Review Security Tests

Verify that tests cover the security behavior `testing-conventions.md` requires,
where the Story touches it:

- each protected endpoint has an allowed-role and a forbidden-role test (TC-5);
- a test enumerates endpoints and fails on unlisted anonymous access (TC-5);
- read-only mode is tested in Application, and Google ports receive no call
  (TC-5);
- `AllowedAdmin` is asserted on every login, and a silent Control Plane refuses
  the login (TC-5);
- error bodies carry no internals (TC-3);
- no test reaches a live Google API or Control Plane; fixtures are synthetic
  (TC-4);
- audited actions are proven by a test (SC-11).

Assess test quality.

A test that only checks that a method was called may not prove the security
property.

---

## Step 17: Review Abuse Cases

For the active Story, identify a small set of realistic misuse cases, such as:

- calling a write endpoint directly while the installation is read-only;
- an Admin whose `AllowedAdmin` entry was revoked signing in again;
- submitting a role, account state or foreign identifier in a request body;
- saving a `WorkspaceConnection` for a domain the Owner never approved;
- repeated failed sign-ins;
- oversized or malformed input;
- inspecting responses, exports or logs for personal data.

Only include abuse cases relevant to approved scope.

Rate limiting and denial-of-service protections should not be invented as
mandatory requirements unless defined by approved artifacts.

If materially needed but undefined, record an Open Decision or recommendation.

---

## Step 18: Review Deviations

Compare actual security behavior with:

- Specification;
- `security-conventions.md`;
- API design;
- DB design;
- Implementation Report.

Identify:

- undocumented security behavior;
- omitted controls;
- permissive defaults;
- unapproved changes;
- security-relevant supporting changes;
- false or incomplete implementation claims.

---

## Step 19: Classify Findings

Classify each finding as:

### Critical

Blocks progression.

Every case `security-conventions.md` labels Critical, and in addition:

- plaintext password persistence, or a password, hash or key exposed;
- unrestricted access to protected functionality;
- committed key, token or credential;
- a security-sensitive Open Decision implemented as an assumption;
- an active test bypass hiding insecure behavior.

### Major

Requires correction before `HUMAN_PR_APPROVAL`.

Examples:

- a missing security test that `testing-conventions.md` requires;
- an audited action with no `AuditEvent`;
- personal data in a log line;
- weak input validation;
- incomplete error sanitization;
- missing persistence constraint;
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

## Step 20: Assign Security Category

For every finding assign one category:

- AUTHENTICATION;
- AUTHORIZATION;
- READ_ONLY_MODE;
- PASSWORD_HANDLING;
- SERVICE_ACCOUNT_KEY;
- GOOGLE_ACCESS;
- DATA_EXPOSURE;
- INPUT_VALIDATION;
- API_SECURITY;
- ERROR_HANDLING;
- PERSISTENCE;
- CONFIGURATION;
- DEPENDENCY;
- LOGGING;
- AUDIT;
- OUTBOUND_DATA;
- SECRET_MANAGEMENT;
- TEST_COVERAGE;
- REPOSITORY_HYGIENE;
- OTHER.

---

## Step 21: Determine Loop-Back Target

`stage-map.yaml` defines two loop-backs for `SECURITY_REVIEW`:

| Root cause | verdict | loop_back_stage | key |
|---|---|---|---|
| Correct artifacts but insecure code | `CHANGES_REQUIRED` | `IMPLEMENTATION` | `changes_required` |
| Approved API/security design exposes a prohibited field or permits unsafe access | `CHANGES_REQUIRED` | `API_DESIGN` | `invalid_security_design` |

For any other upstream root cause (missing password policy / authorization
requirement → `SPECIFICATION`; missing security test → `TEST_WRITING`; omitted
security component in the schema → `DB_DESIGN`), return `verdict: BLOCKED` and
name the responsible stage in `blocking_issues` for the orchestrator / a human
to route. Do not route every finding to `IMPLEMENTATION`.

---

## Step 22: Create Security Review Report

Create the `security_review` artifact at its registry path
(`docs/reviews/security/{story_id}-security-review.md`), front matter per
`docs/workflow/artifact-schema.md` (`artifact_type: security_review`).

Do not modify source code, tests, or approved artifacts. Do not update workflow
state. Do not commit.

---

# Security Review Report Format

## Front Matter

Shared block from `docs/workflow/artifact-schema.md`
(`artifact_type: security_review`), plus: `critical_findings`,
`major_findings`, `minor_findings`, `informational_findings`,
`security_sensitive` (bool), `runtime_checks` (`FULL` / `PARTIAL` / `NONE`).
`created_at` / `updated_at` are runtime timestamps.

Illustrative:

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
      - path: docs/specifications/US-001-spec.md
        version: 1
      - path: docs/tests/US-001-ac-test-matrix.md
        version: 1
    supersedes: null
    critical_findings: 1
    major_findings: 2
    minor_findings: 1
    informational_findings: 0
    security_sensitive: true
    runtime_checks: PARTIAL
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

- exposed functionality per host;
- protected assets;
- trust boundaries;
- affected security components.

## 4. Environment and Tools

Record:

- .NET SDK version;
- test environment (Docker / Testcontainers available or not);
- commands run;
- unavailable checks.

Do not record secrets.

## 5. Project Security Checklist

The SC items the scope touches, each with status (`PASS` / `FINDING` /
`NOT_VERIFIED`) and evidence. Items the Story does not touch are listed as
`NOT_APPLICABLE` with one line of reason.

## 6. Authentication and Authorization

Record:

- applicable requirements;
- endpoint and page access per role;
- anonymous endpoints against the SC-4 list;
- implementation evidence;
- tests;
- findings.

## 7. Credentials, Key and Google Access

Record:

- password handling;
- service-account key handling;
- scopes and impersonation;
- tests;
- findings.

## 8. Sensitive Data Exposure

Record review results for:

- responses and views;
- DTOs;
- logs;
- audit rows;
- exceptions;
- exports;
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
- approved anonymous access;
- protected operations;
- request and response restrictions;
- error behavior;
- findings.

## 11. Persistence and Configuration

Record:

- sensitive fields;
- schema constraints;
- migrations;
- database separation;
- configuration profiles;
- generated files;
- findings.

## 12. Logging, Audit and Telemetry

Record:

- sensitive logging review;
- audit coverage;
- hook telemetry review;
- findings.

## 13. Dependencies

Record:

- added packages and project references;
- approval status;
- vulnerability scanning evidence when available;
- findings.

Do not state that dependencies are secure when vulnerability scanning was not
performed.

## 14. Security Test Coverage

Map security requirements and abuse cases to tests.

## 15. Abuse Case Review

For every reviewed abuse case record:

- scenario;
- expected protection;
- evidence;
- status;
- finding.

## 16. Repository Hygiene

Record:

- secret-like files;
- generated database files and exports;
- ignored files;
- unsafe local configuration;
- findings.

## 17. Deviations

List deviations between approved security requirements and actual
implementation.

## 18. Findings

For each finding provide:

- ID;
- severity;
- category;
- SC reference when one applies;
- affected file or artifact;
- observed evidence;
- expected security behavior;
- risk;
- required correction;
- loop-back target;
- verification required after correction.

Do not include actual secret values.

## 19. Positive Controls

List security controls that were independently observed and verified.

## 20. Open Decisions

List unresolved security decisions.

If none exist, state:

    No blocking security Open Decisions were identified.

## 21. Review Limitations

List checks that were not performed and explain why.

## 22. Verdict Rationale

Explain the verdict (see Result Envelope). When a human security decision is
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
Critical or Major findings; every touched SC item is `PASS`; required security
tests pass; security-sensitive Acceptance Criteria are verified; no blocking
security Open Decision. Minor / Informational findings go in
`non_blocking_findings`. The orchestrator advances to `HUMAN_PR_APPROVAL`.

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
- resolve security decisions;
- accept security risk;
- expose secret values or personal data in reports;
- open, print or quote live credential files;
- execute destructive database operations;
- connect to a non-test database;
- call a live Google API or Control Plane;
- weaken ASP.NET Core security configuration;
- disable CSRF/antiforgery or authentication;
- disable or weaken security tests;
- suppress security findings;
- update workflow state automatically;
- commit or push files;
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
5. Recommend key deletion and rotation per DC-5 without claiming it has
   occurred.
6. Stop actions that could further expose the value.

If required runtime verification cannot be performed (for example Docker is
unavailable):

1. record the limitation;
2. continue static and configuration review where safe;
3. do not mark unverified controls as confirmed;
4. return `verdict: BLOCKED` (cannot evaluate) or `CHANGES_REQUIRED` (a concrete
   correctable insecurity was still found) according to impact.

If vulnerability scanning is unavailable:

1. inspect dependency changes;
2. record that vulnerability database analysis was not performed;
3. do not claim dependency safety.

---

# Human Review Boundary

This Skill provides an engineering security review.

It cannot:

- accept business risk;
- approve exceptions to `security-conventions.md`;
- replace human code review;
- replace specialized security assessment;
- approve deployment;
- approve the commit at `HUMAN_PR_APPROVAL`;
- waive Critical or Major findings.

Return `verdict: BLOCKED` with an explicit "human security decision required"
note in `blocking_issues` when:

- a security exception is requested;
- risk acceptance is needed;
- a sensitive architectural decision remains open;
- available tooling cannot provide sufficient evidence;
- suspected credential compromise exists.

The orchestrator surfaces this to a human; it is not a stage transition the
Skill routes.

---

# Completion Criteria

Security Review is complete only when:

- active Story and workflow stage are resolved;
- artifact versions are validated;
- security-relevant scope is identified;
- assets and trust boundaries are documented;
- every touched SC item has a status and evidence;
- authentication and authorization are reviewed when relevant;
- sensitive data exposure is reviewed;
- input validation is reviewed;
- API security is reviewed;
- persistence and configuration are reviewed;
- logging, audit and telemetry are reviewed;
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
