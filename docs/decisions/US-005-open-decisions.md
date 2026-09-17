---
artifact_type: open_decisions
story: US-005
version: 1
status: APPROVED
created_at: 2026-09-17T13:23:07Z
updated_at: 2026-09-17T13:29:44Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-005-installation-legitimacy-check.md
    version: null
  - path: trebovaniya.md
    version: 73
supersedes: null
---

# US-005 Open Decisions

Story-level Open Decisions for US-005 (Installation legitimacy check and grace
period). Every item is resolved only by a human, at `HUMAN_SPEC_APPROVAL`; the
resolution is written next to the item and nothing is deleted.

The Story's own "Open Decisions" section records none: its business questions were
decided in `trebovaniya.md` v73. `trebovaniya.md` section 7 has no open item this
Story depends on (items 10 and 14 concern Google access and synchronization; item
26 concerns documentation). Two process decisions are raised because this is the
first installation-side Story and the installation projects do not exist yet —
the same situation US-001 resolved for the Control Plane (US-001 OD-006, OD-007).

Status summary:

| Id | Subject | Status | Affects |
|---|---|---|---|
| OD-001 | NuGet packages for the installation projects | RESOLVED (2026-09-17) | TEST_WRITING, IMPLEMENTATION (FR-001, FR-014) |
| OD-002 | Compile-only installation skeleton created at TEST_WRITING | RESOLVED (2026-09-17) | TEST_WRITING (all ACs) |

---

## Carried from the Story

None.

## Raised by the Specification

### OD-001 — NuGet packages for the installation projects

**Status: RESOLVED.**

**Gap.** US-005 creates `ClassroomAgent.Domain`, `ClassroomAgent.Application`,
`ClassroomAgent.Infrastructure`, `ClassroomAgent.Web` and `ClassroomAgent.Contracts`
(AD-2). `AGENTS.md` allows only packages already referenced in the target
`.csproj` and requires an approved Open Decision to add one. US-001 OD-006 approved
packages for `ClassroomAgent.ControlPlane` and `ClassroomAgent.Tests` only; the new
projects have no approved package.

**Impact.** Without it, `Infrastructure` has no EF Core provider for the
installation database (FR-009) and `Web` has no structured logging (FR-012); the
Story cannot be built.

**Options.**

1. *(Recommended)* Approve exactly the list below — the packages already approved
   in US-001 OD-006, on the same versions already referenced in the solution,
   placed in the project that owns the concern by `package-map.md`. Any package
   not on the list still needs its own Open Decision.

   | Project | Package | Purpose | Rule |
   |---|---|---|---|
   | `ClassroomAgent.Infrastructure` | `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core provider for the installation database | AGENTS.md, PC-1 |
   | `ClassroomAgent.Infrastructure` | `EFCore.NamingConventions` | `UseSnakeCaseNamingConvention()` | PC-5 |
   | `ClassroomAgent.Infrastructure` | `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) | migrations (project of `dotnet ef migrations add`, AGENTS.md command table) | PC-2, DC-4 |
   | `ClassroomAgent.Web` | `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) | startup project of `dotnet ef migrations add` | PC-2, DC-4 |
   | `ClassroomAgent.Web` | `Serilog.AspNetCore` | structured logging, JSON formatter | DC-10 |
   | `ClassroomAgent.Web` | `Serilog.Sinks.File` | rolling file sink | DC-10 |

   `Domain`, `Application` and `Contracts` reference no package.
   `Infrastructure` gets `HttpClient` factory and hosting abstractions through a
   `FrameworkReference` to `Microsoft.AspNetCore.App` (shared framework, not a
   package). `ClassroomAgent.Tests` adds project references only.
2. Decide package by package when each stage needs one.

**Resolution:** *Resolved 2026-09-17 by the human (the Owner): option 1.*

### OD-002 — Compile-only installation skeleton created at TEST_WRITING

**Status: RESOLVED.**

**Gap.** TEST_WRITING runs before IMPLEMENTATION and must leave tests that compile
and fail only for missing behaviour. The test-writer Skill may not write production
code, and the installation projects do not exist. US-001 OD-007 settled this for
the Control Plane only.

**Impact.** Without a skeleton the installation tests cannot compile, so a test
error cannot be told apart from missing implementation until IMPLEMENTATION.

**Options.**

1. *(Recommended)* As US-001 OD-007: TEST_WRITING creates the five projects, adds
   them to `ClassroomAgent.sln` with the references of `package-map.md`, and
   declares only the types the tests reference (the Web `Program`, the
   installation `DbContext`, the entity, port, use-case, contract and result types
   named by the designs) with members throwing `NotImplementedException` and
   nothing registered in DI. IMPLEMENTATION owns every one of these files from then
   on and may reshape them together with the tests.
2. Write only the tests; the red phase is "does not compile" until IMPLEMENTATION.
3. Stop as `BLOCKED` until a general rule for new projects is decided.

**Resolution:** *Resolved 2026-09-17 by the human (the Owner): option 1.*

## Interpretations

Behaviour not literally fixed by the Story or `trebovaniya.md` is stated in the
Specification as interpretations I-1 … I-14 (section 11 there). They are reviewed
at `HUMAN_SPEC_APPROVAL`; any interpretation the human rejects becomes an Open
Decision in a new version of this document.

*Accepted 2026-09-17 by the human (the Owner) at `HUMAN_SPEC_APPROVAL`: interpretations I-1 … I-14.*
