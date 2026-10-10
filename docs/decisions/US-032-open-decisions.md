---
artifact_type: open_decisions
story: US-032
version: 2
status: DRAFT
created_at: 2026-10-10T09:42:30Z
updated_at: 2026-10-10T16:14:25Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-032-meet-code-linking.md
    version: null
  - path: trebovaniya.md
    version: 88
supersedes: 1
---

# US-032 Open Decisions — Link meeting codes to courses

**Nine, all resolved** — OD-009 raised by TEST_WRITING; the first eight — carried from the Story, decided by the Owner on
2026-10-10 in discovery, before activation. Writing the Specification raised
none:

- `trebovaniya.md` v88 (§2, §3, §4 Epic 4, §5) fixes the scoring, the
  thresholds' starting values, the "not a course" mark, the audit list and the
  purge rules.
- §7 has no open item on code linking (questions 22 and 23 were closed in v55).
  Item 28 (`call_ended` field names) concerns US-031's ingestion; this Story reads
  only stored data and its tests use synthetic data (TC-4).

The choices left open by the wording are stated as interpretations I-1 … I-14
in `docs/specifications/US-032-spec.md` §11; any of them may be turned into an
Open Decision at `HUMAN_SPEC_APPROVAL`.

## OD-001 Scope

**Resolved (a)**: one Story — automatic linking, the lists, picking, confirming,
re-linking, the "not a course" mark and the purge extension.
Impact: Specification FR-001 … FR-018.

## OD-002 Where linked codes are managed

**Resolved (a)**: one "Meet meetings" page with separate lists; with v88 the
lists are Unassigned, Linked and Not a course.
Impact: FR-007.

## OD-003 Plain unlink

**Superseded by `trebovaniya.md` v88**: no plain unlink; the "not a course" mark
is added instead (BR-083).
Impact: FR-011, FR-012; §10 Out of Scope.

## OD-004 Picking a course

**Resolved (a)**: any loaded course, candidates first.
Impact: FR-008, FR-010, FR-012.

## OD-005 Codes with no candidate

**Resolved (a)**: shown in the Unassigned list, marked "no candidates", at the end.
Impact: FR-003, FR-007.

## OD-006 Thresholds

**Resolved (a)**: installation configuration, default 60 % / 30 points, not in
the UI.
Impact: FR-004, FR-005 (keys `MeetLinking:MinSharePercent`,
`MeetLinking:MinGapPoints`; DC-3 updated).

## OD-007 When linking runs

**Resolved (a)**: a step right after the Meet step of the same run; skipped when
the Meet step fails; never in read-only mode.
Impact: FR-006.

## OD-008 Audit content

**Resolved (a)**: the meeting code and the course id(s); old and new on
re-linking.
Impact: FR-014.

## OD-009 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet —
`MeetingCodeLink`, `MeetingCodeLinkState`, `SyncStep.Linking`, the six
`AuditAction` values and the `AuditEvent` factories and properties,
`RetentionPurgeCounts.MeetCodeLinks`, `MeetLinkingThresholds` and its setting,
`RosterOnDate`, `MeetCodeScorer` and its records, the ports
`IMeetingCodeLinkRepository`, `IMeetCodeScoringSource`, `IMeetCodesReadSource`,
the widened `IRetentionPurgeStore` and `CourseActivityDates`, the use cases and
queries, the request forms, the page models, the policies and routes. The
test-writer may not change production code — the gap US-025 (OD-009), US-027
(OD-005), US-031 (OD-011), US-037 (OD-007), US-039 (OD-008) and US-042 (OD-001)
resolved the same way.

Options: (a) a compile-only skeleton — only the declarations the tests
reference; new members throw `NotImplementedException`; new parameters of
existing records and methods are added last and optional; new entity
properties are not mapped by EF yet; nothing registered in DI that changes
behaviour, no migration, no existing behaviour changed; IMPLEMENTATION owns and
completes it; (b) no skeleton — tests that do not compile until IMPLEMENTATION.

**Resolution:** (a) — Owner, 2026-10-10.
