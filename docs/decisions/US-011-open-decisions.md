---
artifact_type: open_decisions
story: US-011
version: 2
status: DRAFT
created_at: 2026-09-21T07:48:44Z
updated_at: 2026-09-21T08:39:55Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-011-check-access.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-011 Open Decisions

Story-level Open Decisions for US-011 (Check access diagnostic). Every item is
resolved only by a human; the resolution is written next to the item and nothing
is deleted. All five were raised in the Story and resolved by the Owner before
activation; they are carried here with their `OD-` ids and resolutions unchanged.

| Id | Raised by | Status |
|---|---|---|
| OD-001 Which library the program uses to call Google | the Story | RESOLVED 2026-09-21 (option 1) |
| OD-002 How the check finds out which scope is missing | the Story | RESOLVED 2026-09-21 (option 1) |
| OD-003 Whether the result of a check is stored | the Story | RESOLVED 2026-09-21 (option 1) |
| OD-004 What the self-check logs when it cannot run | the Story | RESOLVED 2026-09-21 (option 1) |
| OD-005 Whether repeated runs are limited | the Story | RESOLVED 2026-09-21 (option 1) |
| OD-006 Compile-only skeleton created at TEST_WRITING | TEST_WRITING | RESOLVED 2026-09-21 (option 1) |

Version 2 adds OD-006 only. It concerns how TEST_WRITING makes its tests compile and changes nothing the
Specification, the API design or the database design says, so those artifacts, which consumed version 1, are not
stale in substance.

## OD-001 Which library the program uses to call Google

No Google data package is referenced in any project, and adding a NuGet package
requires an approved Open Decision (AGENTS.md "Technology Stack").

Options: (1) Google's official .NET client libraries — `Google.Apis.Auth`,
`Google.Apis.Classroom.v1`, `Google.Apis.Admin.Reports.reports_v1`; (2) only
`Google.Apis.Auth`, REST calls by hand; (3) no package, the service-account token
signed by the program itself.

**Resolution:** option 1, decided by the Owner on 2026-09-21. The three official
packages are added to `ClassroomAgent.Infrastructure` only; no Google SDK type
crosses into `Application` or `Domain` (AD-4). The Specification pins the version
line (FR-015); SECURITY_REVIEW scans them for known vulnerabilities.

**Impact on the Specification:** FR-004, FR-015, S-13.

## OD-002 How the check finds out which scope is missing

Google issues a delegated token only when every requested scope is authorised, so
a single request for all six cannot name the missing one — and `trebovaniya.md` §4
requires the message to name it.

Options: (1) one token request per scope, then one minimal read per API; (2) two
reads only, all six scopes requested at once; (3) a real read per scope.

**Resolution:** option 1, decided by the Owner on 2026-09-21.

**Impact on the Specification:** FR-002, FR-003, FR-005.

## OD-003 Whether the result of a check is stored

Options: (1) not stored — shown right after the run, the audit row records that
the check ran; (2) the last result stored in a new table.

**Resolution:** option 1, decided by the Owner on 2026-09-21. No table stores a
check result, and BR-026's closed list of service writes is not extended.

**Impact on the Specification:** FR-008, FR-013, I-4.

## OD-004 What the self-check logs when it cannot run

Options: (1) one line saying it was skipped and why; (2) nothing.

**Resolution:** option 1, decided by the Owner on 2026-09-21. The level of that
line is fixed by the Specification within DC-10 (I-6).

**Impact on the Specification:** FR-010.

## OD-005 Whether repeated runs are limited

Options: (1) no limit; (2) a cooldown with stored state and a refusal message.

**Resolution:** option 1, decided by the Owner on 2026-09-21.

**Impact on the Specification:** FR-006 (no cooldown step), §8.

## OD-006 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING on 2026-09-21. The Story's decisive tests substitute the first real Google port: read-only
mode must make **zero** calls, a run must request exactly the six scopes, and each Google answer must map onto the
closed list of spec FR-005. A substitute has to implement the port's type, and the type does not exist yet, so the
test project cannot compile. The `test-writer` Skill may not create production source on its own judgement, and
the US-005 OD-002 and US-007 OD-003 resolutions were scoped to their own Stories.

Options: (1) a compile-only skeleton in `src/` — only the types the tests reference, members throwing
`NotImplementedException`, nothing registered in DI, owned by IMPLEMENTATION from then on; (2) tests written through
reflection; (3) IMPLEMENTATION writes the tests; (4) option 1 made a standing rule in `AGENTS.md`.

**Resolution:** option 1, decided by the Owner on 2026-09-21, scoped to US-011 exactly as US-005 OD-002 and
US-007 OD-003 were. The skeleton is: `Application/Ports/IGoogleAccessProbe.cs`,
`Application/Models/AccessCheckStepOutcome.cs`, `Application/Models/DelegatedToken.cs`,
`Application/Models/DelegationAttempt.cs`, `Infrastructure/Google/GoogleAccessProbe.cs`,
`Infrastructure/Google/GoogleServiceAccountSettings.cs`. IMPLEMENTATION may reshape them together with the tests.

**Impact on the Specification:** none (FR-004 already requires the port; the names are indicative there).

## `trebovaniya.md` §7 — the open items and this Story

- **Item 10 — the minimum Workspace roles of the technical account**
  (Проверить при внедрении). This Story **touches** it and does not close it.
  The check proves that the technical account's reads *succeed*; it cannot prove
  that the account holds *nothing beyond* read access, and — because a Classroom
  list answers with only the courses the account itself can see — it cannot prove
  the account sees *every* course either (I-8). No message of this Story names a
  Workspace admin role, the line US-010 OD-001 drew. The check is nevertheless the
  tool item 10 will be verified with on a live domain.
- **Item 14 — submissions of a removed student.** A Classroom data question for
  synchronisation; this Story reads no submissions.
- **Item 26 — documentation** (Решить). Untouched.

The rest of what this Story implements is **closed** in the requirements: §4
Epic 6 ("Проверка подключения", "Самопроверка при запуске"), §2 (the matrix row
"Проверить доступ" and the read-only rules), §5 (the audited action, logging and
the settings list), §6 (the scope table), §9 (key rotation and the technical
account), and BR-015, BR-025, BR-026, BR-030, BR-031, BR-032.

No new Open Decision was raised by SPECIFICATION. Where the requirements leave a
detail to the implementer rather than to the Owner, it is written as an
**interpretation** in §11 of the Specification, so it can be corrected at
`HUMAN_SPEC_APPROVAL` instead of being discovered in code.
