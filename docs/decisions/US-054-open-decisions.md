---
artifact_type: open_decisions
story: US-054
version: 1
status: APPROVED
created_at: 2026-10-24T19:40:00Z
updated_at: 2026-10-24T20:20:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-054-technical-account-superadmin-texts.md
    version: null
  - path: trebovaniya.md
    version: 97
supersedes: null
---

# US-054 Open Decisions — The program's texts describe the technical account as a super-admin with blocked sign-in

No Open Decision is unresolved. The four below are carried from the Story with
the Owner's resolutions of 2026-10-24. `trebovaniya.md` §7 item 29 is still
open; OD-001 records why it does not block this Story.

## OD-001 Write the texts now, while §7 item 29 is still open? — RESOLVED

Carried from the Story unchanged.

**Question.** §7 item 29: it is not yet verified that acting as the technical
account through domain-wide delegation works while 2-step verification with no
enrolled factor is enforced on it. If it does not, measure 2 must be replaced,
and this Story's measure texts would change with it.

- **(a) Write the texts now, after v97; if item 29 fails, a new requirements
  version and a follow-up Story (recommended).**
- (b) Wait until item 29 is verified on the pilot server.

**Resolution (the Owner, 2026-10-24):** (a). If item 29 fails, the replacement
measure comes through a new `trebovaniya.md` version and its own Story.

**Impact:** FR-001 measure 4.2 is written per §9 v97; the first live access
check is the manual verification of item 29 (spec §12).

## OD-002 When the super-admin grants the role, relative to the access check — RESOLVED

Carried from the Story unchanged.

**Question.** §9 says the role is granted "непосредственно перед первой
синхронизацией". The access check runs before the first synchronization and
reads Classroom too; without the role it may prove nothing.

- **(a) Grant the role as the last step, after the other measures, immediately
  before the Admin runs the access check (recommended).**
- (b) Literally "before the first synchronization"; the access check runs before
  the role is granted.

**Resolution (the Owner, 2026-10-24):** (a).

**Impact:** FR-001 measure 4.5 (`Measure.RoleGrantedLast`).

## OD-003 Which texts are in scope — RESOLVED

Carried from the Story unchanged.

**Question.** Besides the instruction's technical-account statements and the
connection-page hint, `ReadOnlyRoles` and
`AccessCheck.Outcome.TechnicalAccountCannotRead` (also the synchronization
diagnosis) contradict v97.

- **(a) Include both (recommended).**
- (b) Only the texts named in the request.

**Resolution (the Owner, 2026-10-24):** (a).

**Impact:** FR-002, FR-004.

## OD-004 How detailed the measures are — RESOLVED

Carried from the Story unchanged.

- **(a) The five measures of §9 as *what* to do, with no console menu paths
  (recommended).**
- (b) The measures plus Google Admin console menu paths.

**Resolution (the Owner, 2026-10-24):** (a). The instruction also explains in
one sentence why the account needs the super-admin role (decision 3, yes), and
the connection-page hint carries the wording proposed in the discovery
(decision 6, ok).

**Impact:** FR-001 #3 and 4.1–4.5, FR-003, VR-003.
