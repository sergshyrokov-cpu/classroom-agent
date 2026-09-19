---
artifact_type: open_decisions
story: US-007
version: 2
status: APPROVED
created_at: 2026-09-19T11:54:48Z
updated_at: 2026-09-19T12:17:40Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-007-read-only-mode.md
    version: null
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-007 Open Decisions

Story-level Open Decisions for US-007 (Read-only mode enforcement). Every item is
resolved only by a human, at `HUMAN_SPEC_APPROVAL`; the resolution is written next
to the item and nothing is deleted.

`trebovaniya.md` section 7 has no open item this Story depends on: item 10 (the
minimum Google Workspace roles) and item 14 (submissions of a removed student) are
Google questions this Story does not touch, and item 26 is documentation. The
read-only rule itself is closed — §2 ("В режиме «только чтение»") and §9 — and is
restated as BR-025, BR-026 and AD-6.

Status summary:

| Id | Subject | Status | Affects |
|---|---|---|---|
| OD-001 | Where the HTTP `409` mapping and the user-visible message belong | RESOLVED (2026-09-19): option 1 | FR-004, section 8, AC-002 |
| OD-002 | How `Application` writes the refusal log line without a NuGet package | RESOLVED (2026-09-19): option 3 | FR-009, AC-010, S-03 |

---

## Carried from the Story

### OD-001 — Where the HTTP `409` mapping and the user-visible message belong

**Status: RESOLVED.**

**Gap.** API-5 and API-6 fix what a caller sees when read-only mode refuses an
action: HTTP `409`, with a message naming the reason. The installation has no
controller, no `/api/v1`, no error page and no localization baseline today — all of
them arrive with US-008 (US-005 Notes, US-005 Specification I-13).

**Options.**

1. This Story delivers the refusal as far as `Application` only (the typed failure
   of AC-002); the first Story adding an installation endpoint maps it to `409`
   with the translated message and tests it (US-008). It must then be recorded that
   API-5 is not yet satisfied end to end after US-007.
2. This Story also delivers the mapping, which means bringing forward a slice of
   the installation host baseline (error body, error page, localization) that
   US-005 deliberately left to US-008.

**Resolution:** *Resolved 2026-09-19 by the human (the Owner): option 1. US-007
delivers the refusal as far as `Application` — the typed failure of AC-002,
carrying the reason as data. The mapping to HTTP `409` with the translated message
arrives with US-008, together with the installation host baseline it needs. API-5 is
therefore **not** satisfied end to end after this Story; the Specification records
that openly in sections 1, 8 and 10.*

## Raised by the Specification

### OD-002 — How `Application` writes the refusal log line without a NuGet package

**Status: RESOLVED.**

**Gap.** Three rules of this project meet here and cannot all be satisfied by the
Specification on its own:

- **AC-010** requires one `Warning` log line per refused write, naming the refused
  operation and the reason category (DC-10, SC-10).
- **AC-011** and AD-6 put the enforcement point — the place that knows a refusal
  happened — in `ClassroomAgent.Application`.
- `ClassroomAgent.Application` references **no NuGet package at all** and no
  logging abstraction. The US-005 architecture test
  `ProjectReferenceTests.FrameworkFreeProjects_ReferenceNoPackage` asserts exactly
  that (zero `PackageReference`, zero `FrameworkReference`), and AC-011 requires the
  US-005 architecture tests to keep passing **unchanged**. `AGENTS.md` (Technology
  Stack) adds that a new NuGet package needs an approved Open Decision.

So the layer that detects the refusal cannot, today, write a log line; something has
to give, and which one is a human's decision, not the Specification's.

**Options.**

1. **A logging port in `Application/Ports`** — e.g. `IReadOnlyRefusalLog`, declared
   in `Application`, implemented in `Infrastructure` over `ILogger<>` (available
   there through EF Core), wired in `Web`. `Application` stays package-free and the
   US-005 test passes unchanged. Cost: `package-map.md` currently limits `Ports` to
   "every external system … only Google and the Control Plane; a port to any other
   external system needs a separate decision (SC-13)", so this option **changes
   `package-map.md`** to allow a logging port, and every later Story gains a tempting
   general-purpose logging seam in `Application`.
2. **Add `Microsoft.Extensions.Logging.Abstractions` to
   `ClassroomAgent.Application`.** The most ordinary .NET answer, and the guard logs
   directly where the decision is made. Cost: it contradicts two things this Story
   is bound by — the US-005 architecture test must be weakened (`Application` would
   no longer be package-free), which AC-011 forbids in its current wording, and
   `AGENTS.md` requires the package addition to be approved here. Choosing it means
   accepting both and editing Story AC-011.
3. *(Recommended)* **Log at the edge, in a decorator.** `Application` declares the
   enforcement point behind a small interface (`IReadOnlyModeGuard`) and throws;
   `Infrastructure` registers a decorator around it that catches the refusal, writes
   the `Warning` line through `ILogger<>` and rethrows. Nothing is added to
   `Application`: no package, no port, no `package-map.md` change, and the US-005
   test passes unchanged. The rule stays a single decision in `Application` — the
   decorator observes it, it does not re-decide it (AC-011). Cost: "every refusal is
   logged" becomes a property of the DI wiring rather than of the guard itself, so a
   test must assert the decorator is registered in front of the guard; the
   Specification requires that test (FR-009, FR-010).

**Impact if left open.** FR-009 (refusal logging) cannot be implemented, AC-010
cannot be satisfied, and S-03 has no mechanism. Everything else in the Story — the
guard, the closed list, the commit backstop, the Google rule and the structural test
— is unaffected and could proceed, but the Story would be Done with one
Acceptance Criterion unmet, which the Definition of Done does not allow. The
Specification therefore writes FR-009 against option 3 and marks it as conditional
on this decision.

**Resolution:** *Resolved 2026-09-19 by the human (the Owner) at
`HUMAN_SPEC_APPROVAL`: option 3. `Application` throws `ReadOnlyModeException` and
`Infrastructure` registers the decorators that write the `Warning` line and rethrow
— around `IReadOnlyModeGuard` and around `IUnitOfWork`. No NuGet package is added to
`ClassroomAgent.Application`, no logging port is introduced, `package-map.md` is
unchanged and the US-005 architecture test
`ProjectReferenceTests.FrameworkFreeProjects_ReferenceNoPackage` keeps passing
unchanged, as Story AC-011 requires. The Specification must keep the test that the
decorators are registered in front of the guard and the unit of work (FR-010), so
"every refusal is logged" is asserted rather than assumed.*

## Interpretations

Behaviour not literally fixed by the Story or `trebovaniya.md` is stated in the
Specification as interpretations I-1 … I-12 (section 11 there). They are reviewed at
`HUMAN_SPEC_APPROVAL`; any interpretation the human rejects becomes an Open Decision
in a new version of this document.

*Accepted 2026-09-19 by the human (the Owner) in the conversation: I-1 … I-12.*
