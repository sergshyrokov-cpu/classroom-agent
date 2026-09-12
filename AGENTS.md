# classroom-agent

A system for school staff to monitor the teaching process in Google Classroom.
An existing Python/Streamlit prototype is being rewritten as a stable
C#/.NET system. Each school gets its own installation (backend + database);
a shared Control Plane owned by the service Owner governs which installations
may run and with which Google Workspace domain.

All changes must be traceable to documented requirements and workflow artifacts.

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
| Product context | `docs/product/` (vision, epic-map, business-glossary, business-rules, personas, non-functional-requirements) |

`trebovaniya.md` outranks every document under `docs/`. The files in
`docs/architecture/` and `docs/product/` are derived from it — if one of them
contradicts `trebovaniya.md`, the derived document is wrong and must be fixed,
not the requirements.

No Skill, command, or document may define an alternative stage list, alternative
stage identifiers, or an alternative artifact-path convention.

---

# Technology Stack

- **.NET 10 (LTS)**, C# — .NET 9 is out of support and .NET 8 LTS ends in
  November 2026, so neither is a valid target for this project
- ASP.NET Core MVC / Razor Pages + REST API (server-rendered UI; not Blazor)
- **EF Core** with the **Npgsql** provider
- **PostgreSQL** — chosen because the Owner hosts and pays for ~10 installations:
  no licence cost, no database size ceiling, runs in a container
- xUnit, `Microsoft.AspNetCore.Mvc.Testing`
- ASP.NET Core Identity — local login/password for Dean, Google OAuth
  (external login) for Admin
- Google APIs: Classroom API, Admin SDK Directory API, Admin Reports API —
  all read-only, via a service account with domain-wide delegation

Always verify actual project dependencies before relying on a library.

---

# Domain Essentials

Read `trebovaniya.md` for the full picture. The minimum to avoid mistakes:

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
2 human gates, 1 terminal stage. There is no clarification, spec review, design
review, impact analysis, implementation planning, plan review, implementation
verification, reconciliation, PR preparation or archive stage. See the SCOPE
NOTE in `stage-map.yaml` for why each was dropped and what covers its work now.

---

# Human Gates

`stage-map.yaml` defines two gates where the workflow stops for a person:
`HUMAN_SPEC_APPROVAL` and `HUMAN_PR_APPROVAL`. Approval is recorded only via
`/so:approve` (or `/so:reject`). Auto Mode never passes a human gate.

`HUMAN_SPEC_APPROVAL` carries more weight here than in the full harness: with no
automated spec or design reviewer, it is the only check that the Specification
faithfully reflects `trebovaniya.md`.

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

# Security Policy

Security-first defaults are mandatory. The full policy lives in
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

---

# Git Policy

- Generated changes stay scoped to the active Story. No opportunistic
  refactoring, no unrelated file edits.
- **Commits go directly to `master`** — this is a solo project with no PR flow.
  Do not create feature branches.
- Skills do not commit or push. A human commits after `HUMAN_PR_APPROVAL`.
- Generated database files, IDE-local config, and secrets never enter a commit.

---

# Observability

- `docs/workflow/history.jsonl` — the single append-only workflow transition
  log (owned by `story-orchestrator`).
- `docs/hooks/tool-usage.jsonl` — tool-usage telemetry (separate; metadata
  only, never full sensitive payloads; git-ignored).

Telemetry is execution evidence, never requirement authority. Do not disable or
bypass configured hooks.

---

# Agent Behavior

When information is missing: do not assume, do not invent requirements,
security rules, or business rules. Record an Open Decision, explain the
uncertainty, and request clarification.

`trebovaniya.md` is written in Russian and is the authority. Workflow artifacts
and code comments are written in English.
