# classroom-agent

A system for school staff to monitor the teaching process in Google Classroom.
An existing Python/Streamlit prototype is being rewritten as a stable
C#/.NET system. Each school gets its own installation (backend + database);
a shared Control Plane owned by the service Owner governs which installations
may run and with which Google Workspace domain.

All changes must be traceable to documented requirements and workflow artifacts.

---

# Hard Stops

Absolute. No Story, instruction or Open Decision makes these acceptable:

- commit a secret or a service-account key, in any form;
- write anything to Google Workspace — every scope is read-only;
- store the service-account key in an installation database, or accept it
  through the UI;
- give a Teacher or Student an account, or scope visibility by Classroom roster
  (v1 roles are Owner, Admin, Dean);
- read student data out of `classroom_cache.db` or the generated `.xlsx`
  exports;
- record approval at a human gate by any means other than `/so:approve`.

Procedural. Never done on an agent's own judgement; a human may authorise an
exception by recording it as a resolved Open Decision:

- commit a generated database file or a generated `.xlsx` export
  (`docs/product/report-templates/` holds the only versioned spreadsheets);
- start implementation without an approved Specification, or invent an endpoint,
  schema, security rule or business rule that no artifact defines;
- write workflow state from a stage Skill, or disable a hook;
- edit the Python prototype.

---

# Canonical Sources (authoritative — do not duplicate their content elsewhere)

| Concern | File |
|---|---|
| **Requirements — the source of truth for the whole system** | `trebovaniya.md` (Russian, versioned; read its header changelog first) |
| Workflow: stages, order, ownership, transitions, loop-backs, human gates | `docs/workflow/stage-map.yaml` |
| Where every artifact lives and who owns it | `docs/workflow/artifact-paths.yaml` |
| Status vocabularies (artifact status / review verdict / workflow status) | `docs/workflow/artifact-lifecycle.md` |
| Workflow-state & active-story schema, history event schema | `docs/workflow/state-schema.md` |
| Artifact front-matter schema | `docs/workflow/artifact-schema.md` |
| Human-readable workflow overview (non-normative) | `docs/workflow/stages.md` |
| Architecture decisions | `docs/architecture/architecture.md` |
| Package ownership & dependency rules | `docs/architecture/package-map.md` |
| API conventions | `docs/architecture/api-conventions.md` |
| Persistence conventions | `docs/architecture/persistence-conventions.md` |
| Security conventions | `docs/architecture/security-conventions.md` |
| Testing conventions | `docs/architecture/testing-conventions.md` |
| Deployment & operations | `docs/architecture/deployment-conventions.md` |
| Product context | `docs/product/` (vision, epic-map, business-glossary, business-rules, personas, non-functional-requirements) |

Rule identifiers used throughout this file: **AD-** architecture decisions
(`architecture.md`), **AC-** API conventions, **PC-** persistence conventions,
**SC-** security conventions, **TC-** testing conventions,
**DC-** deployment & operations — all in `docs/architecture/`; **NFR-**
non-functional requirements in `docs/product/non-functional-requirements.md`.

`trebovaniya.md` outranks every document under `docs/`. The files in
`docs/architecture/` and `docs/product/` are derived from it — if one of them
contradicts `trebovaniya.md`, the derived document is wrong and must be fixed,
not the requirements.

No Skill, command, or document may define an alternative stage list, alternative
stage identifiers, or an alternative artifact-path convention.

---

# Technology Stack

- **.NET 10 (LTS)**, C# — see NFR-062 for why not .NET 8/9
- ASP.NET Core MVC / Razor Pages + REST API (server-rendered UI; not Blazor)
- **EF Core** with the **Npgsql** provider
- **PostgreSQL** — rationale and runtime rules in `persistence-conventions.md` PC-1
- xUnit, `Microsoft.AspNetCore.Mvc.Testing`
- Serilog — structured logging to a rolling file (DC-10, approved in v15)
- ASP.NET Core Identity — local login/password for Dean, Google OAuth
  (external login) for Admin
- Google APIs: Classroom API, Admin Reports API (Meet events) —
  all read-only, via a service account with domain-wide delegation

Use only packages already referenced in the target `.csproj`. Adding a NuGet
package requires an approved Open Decision.

Solution `ClassroomAgent.sln` (AD-2): six production projects under `src/`,
tests in `tests/ClassroomAgent.Tests/`. From the repository root:

| Task | Command |
|---|---|
| Build | `dotnet build ClassroomAgent.sln` |
| All tests | `dotnet test ClassroomAgent.sln` |
| One test class | `dotnet test --filter FullyQualifiedName~<ClassName>` |
| Run the Data Plane | `dotnet run --project src/ClassroomAgent.Web` |
| Run the Control Plane | `dotnet run --project src/ClassroomAgent.ControlPlane` |
| Add a migration | `dotnet ef migrations add <Name> --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web` |

Integration tests need a running Docker daemon (Testcontainers, PC-1).

---

# Architecture Invariants

The full rules live in `docs/architecture/` (AD-*, AC-*, PC-*, SC-*). These six
are non-negotiable — violating one is a defect, not a style preference:

1. `Domain` depends on nothing; `Application` depends on `Domain` only. An
   `Application → Infrastructure` project reference is forbidden (AD-3).
2. Everything outside the process is reached through a port interface declared
   in `Application/Ports` and implemented in `Infrastructure`. No Google SDK
   type crosses into `Application` or `Domain` (AD-4).
3. `DbContext` never appears in `Web`/`ControlPlane`; the presentation layer
   holds no business rules and calls no Google API directly (AD-3).
4. Domain entities never appear in a controller signature, request body,
   response body or Razor view model — DTOs only, mapped in `Application` (AD-8).
5. Read-only mode is enforced in `Application`, never by hiding UI (AD-6).
6. Control Plane and installation are two deployables with two databases; one
   database serving several schools is an architecture violation (AD-1, PC-1).

---

# Coding Conventions

These are binding now; the machine-enforceable ones move into `.editorconfig`
and `Directory.Build.props` when the solution is created.

- `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  in every project. A nullable warning is a build failure, not a hint.
- Asynchronous methods end in `Async` and accept a `CancellationToken`. No
  `.Result`, no `.Wait()`, no `async void` outside event handlers.
- Dependencies arrive by constructor injection. No service locator, no static
  mutable state.
- One public type per file; the file name matches the type.
- Exceptions signal failures, not expected outcomes (AD-9).
- Code, identifiers and comments are English (see Agent Behavior).

---

# Domain Essentials

**Non-normative summary** of `trebovaniya.md` sections 2, 5 and 9 — a cheap
cache so routine decisions do not require opening a 65 KB Russian document.
`trebovaniya.md` always wins; on conflict this section is the one that gets
corrected. Re-verify it whenever `trebovaniya.md` changes version.

- **Roles in the first version are Owner, Admin and Dean only.** Teacher and
  Student are deferred to Epic 7. Teachers and students exist as *synced data*
  (`ClassroomParticipant`), never as accounts — nothing in v1 may grant them a
  login or scope visibility by Classroom roster.
- **Owner** lives in the Control Plane (separate service, separate database),
  never in an installation's `AppUser` table.
- **Admin is a Google Workspace domain administrator of that school.** One email
  is simultaneously the OAuth login, the `AllowedAdmin` entry in the Control
  Plane, the `AppUser` with role Admin, and the impersonation user in
  `WorkspaceConnection`. Only the service-account email is separate.
- **Dean does the day-to-day work**; Admin installs, configures and grants roles.
  The permission matrix is in `trebovaniya.md` section 2 — do not invent cells.
- **The service-account key never reaches a school.** The Owner places it at
  deployment time; `WorkspaceConnection` stores a *reference to a secret*, never
  the key itself.
- **Read-only mode** (grace period expired, or the Owner suspended the
  `Installation`) leaves viewing and export working and blocks everything else,
  including synchronization.

---

# The Python Prototype

The repository root holds the original Streamlit prototype (`app.py`, `db.py`,
`google_api.py`, `google_api_OLD.py`, `collect_analytics.py`, `exports.py`,
`views/`, `test_*.py`, `_backup/`). It is a frozen **reference**, not legacy to
maintain and not the source of requirements.

- Use it to answer empirical questions the documents cannot: what a Google API
  actually returns, which scopes really work (`trebovaniya.md` section 6 cites
  `google_api.py` and `test_meet.py` as the evidence for its scope table), and
  how `exports.py` builds a journal from the templates in
  `docs/product/report-templates/`.
- Do not edit, refactor or fix prototype files, and do not translate them into
  C#. The .NET system is written from `trebovaniya.md`; where the prototype
  disagrees with it, the prototype is simply old.
- `classroom_cache.db` and the generated `.xlsx` exports in the root may hold
  real student data — do not read their contents into a conversation or copy
  them into the .NET tree. The blank report templates were moved to
  `docs/product/report-templates/` and are versioned.
- `google_credentials.json` and `dac-classroom-agent-*.json` are live
  credentials. Never open, print or quote them.
- The prototype may be deleted once Epics 3 and 4 are delivered and the Open
  Decisions it answers are resolved. **Retiring it has a mandatory first step:**
  `dac-classroom-agent-*.json` is a *live* service-account key. Remind the human
  to delete that key in Google Cloud Console first, and only then the file — a
  key copy outside the secret store is forbidden (DC-5). Never delete either
  yourself: the key belongs to the Owner, and removing it stops the prototype.

---

# Active Scope

The active Story is defined by `docs/workflow/active-story.yaml` and its
execution state by `docs/workflow/workflow-state.yaml`. Story lifecycle status
is owned by `docs/catalog/stories.yaml`. Work only on the active Story unless
explicitly instructed otherwise.

Stories are **authored by a human** in `docs/stories/` — there is no
backlog-sync stage and no GitHub-Issue source in this project.

Only `story-orchestrator` writes `workflow-state.yaml` and `active-story.yaml`.
Stage Skills never write workflow state — they return a result envelope and the
orchestrator records the transition.

---

# Artifact-Driven Development

Code generation is always driven by approved artifacts. The delivery flow is
`docs/workflow/stage-map.yaml`. Do not start implementation directly from a
User Story. Do not invent endpoints, schema, security behavior, or business
rules during coding — if something is undefined, record an Open Decision.

Order of authority when artifacts conflict:

1. `trebovaniya.md`
2. User Story & Acceptance Criteria
3. Approved Specification
4. Resolved Open Decisions
5. Approved API & database designs

Implementation never overrides a documented requirement.

**This project runs the lightweight workflow variant** — 6 automated stages,
2 human gates, 1 terminal stage. The SCOPE NOTE in `stage-map.yaml` lists which
stages of the full harness were dropped and what covers their work now.

---

# Human Gates

`stage-map.yaml` defines two gates where the workflow stops for a person:
`HUMAN_SPEC_APPROVAL` and `HUMAN_PR_APPROVAL`. Approval is recorded only via
`/so:approve` (or `/so:reject`). Auto Mode never passes a human gate.

`HUMAN_SPEC_APPROVAL` carries more weight here than in the full harness: with no
automated spec or design reviewer, it is the only check that the Specification
faithfully reflects `trebovaniya.md`.

`HUMAN_PR_APPROVAL` keeps its name from the full harness. There is no pull
request in this project — it is the approval to commit the finished Story to
`master` (see Git Policy).

---

# Open Decisions Policy

Open Decisions are blockers. If an approved artifact contains `TODO`, `TBD`,
`FIXME`, `???`, or an unresolved Open Decision that affects the next stage, do
not proceed. Instead: document the gap, request clarification, update the
Specification. Clarification is always preferred over guessing.

Before writing a Specification, check `trebovaniya.md` section 7
("Открытые вопросы"). If the Story depends on something still listed there, that
is an Open Decision — raise it, do not resolve it yourself.

---

# Testing Strategy

`TEST_WRITING` runs before `IMPLEMENTATION` — tests are written against the
approved Specification and API design, never against finished code. The full
rules are `docs/architecture/testing-conventions.md` (TC-*).

Three that are never negotiable:

- No automated test calls a live Google API; the Google ports are substituted
  and fixtures are synthetic (TC-4).
- Integration tests run against real PostgreSQL via Testcontainers; the EF Core
  InMemory provider is forbidden (TC-2).
- Every protected endpoint has both an allowed-role and a forbidden-role test;
  read-only mode is tested in the Application layer, not as UI state (TC-5).

---

# Definition of Done

A Story is Done only when all of the following hold:

1. `dotnet build` succeeds with no errors and no warnings
   (`TreatWarningsAsErrors` makes this one check).
2. `dotnet test` is green — no skipped, ignored or commented-out tests.
3. Every Acceptance Criterion of the Story maps to at least one passing test.
4. Every entity change ships with its EF Core migration in the same Story
   (PC-2); no `EnsureCreated()`, no schema change outside a migration.
5. No `TODO`, `TBD`, `FIXME` or unresolved Open Decision remains in the changed
   code or in the Story's artifacts.
6. `SECURITY_REVIEW` returned PASS.
7. No secret, generated database file or IDE-local config is staged for commit.
8. Changed files stay within the active Story's scope.
9. `HUMAN_PR_APPROVAL` is recorded via `/so:approve` and the Story is committed
   to `master`. Until then the Story is finished, not Done — `stage-map.yaml`
   reaches `COMPLETED` only after the gate.

---

# Security Policy

The full policy lives in
`docs/architecture/security-conventions.md`, derived from `trebovaniya.md`
sections 5, 6 and 9. Do not weaken it without a human-approved Open Decision.

Non-negotiable:

- The system stores personal data of students, potentially minors. Access is
  limited to the Admin and Dean roles.
- Never commit secrets. The service-account key lives in
  secrets/Key Vault/environment variables — never in the repository, never in
  the installation database, never uploaded through the UI.
- Dean passwords are stored only as a hash and never returned by any API.
  Admin has no local password at all.
- All Google API scopes are read-only. The program never writes to Google
  Workspace.
- All external input is validated before it reaches business logic: request
  bodies, query and route parameters, uploaded files, and data returned by
  Google APIs. Shape rules (required, length, format, range) are declared for
  the request types in `Application/Models/Requests`; the controller only turns
  a failed check into `400` with the `fieldErrors` body of AC-6. Rules that
  need domain state — "does this Dean belong to this installation?" — are
  enforced in the Application use case, never in the controller and never in a
  Domain entity's constructor. The rejected payload is never written to a log
  (SC-10).

---

# Git Policy

- Modify only files required by the active Story's approved artifacts;
  touching anything else requires an Open Decision. No opportunistic
  refactoring.
- **Commits go directly to `master`** — this is a solo project with no PR flow.
  Do not create feature branches.
- No automated stage commits or pushes: a Skill never runs `git commit` or
  `git push`. A commit happens only after `HUMAN_PR_APPROVAL`, made by the human
  or by an agent acting on an explicit request from the human in that
  conversation.
- Generated database files, IDE-local config, and secrets never enter a commit.

---

# Observability

This section is about the **harness**. Application logging and health checks are
`deployment-conventions.md` DC-10 and DC-11.

- `docs/workflow/history.jsonl` — the single append-only workflow transition
  log (owned by `story-orchestrator`).
- `docs/hooks/tool-usage.jsonl` — tool-usage telemetry (separate; git-ignored).
  It records tool names, artifact keys and stage identifiers — never file
  contents, student names or emails, tokens, or secret values.

Telemetry is execution evidence, never requirement authority. Do not disable or
bypass configured hooks.

---

# Agent Behavior

When information is missing: do not assume, do not invent requirements,
security rules, or business rules. Record an Open Decision, explain the
uncertainty, and request clarification.

`trebovaniya.md` is written in Russian and is the authority. Workflow artifacts
and code comments are written in English.
