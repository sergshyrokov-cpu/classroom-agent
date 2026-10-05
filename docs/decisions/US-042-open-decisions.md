---
artifact_type: open_decisions
story: US-042
version: 2
status: DRAFT
created_at: 2026-10-05T11:11:02Z
updated_at: 2026-10-05T12:49:19Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-042-names-in-report.md
    version: null
  - path: trebovaniya.md
    version: 86
supersedes: 1
---

# US-042 Open Decisions — Names of students and teachers in a template report

**One, resolved** (OD-001, raised by TEST_WRITING). None at SPECIFICATION:

- The Story carried no Open Decision ("None at authoring").
- Writing the Specification raised none: `trebovaniya.md` v86 (§3, §4 Epic 3)
  fixes both sources, the fallback, one rule for students and teachers, the
  ordering, the built-in template's value and the switch.
- No item of `trebovaniya.md` §7 concerns this Story. The ПІБ with patronymic
  from the school's own data is a §7 question of Epic 14 and is out of scope.

The choices left open by the wording are stated as interpretations I-1 … I-10 in
`docs/specifications/US-042-spec.md` §11; any of them may be turned into an Open
Decision at `HUMAN_SPEC_APPROVAL`.

## OD-001 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet —
`ReportNameSource`, `ClassroomParticipant.Surname` / `GivenName` and the widened
`Import` / `UpdateFrom`, `ReportTemplate.NameSource`,
`ReportTemplateSettings.NameSource`, the widened `RosterEntry` and
`JournalCourseMemberRecord`, `ReportNameKind`, `NameSourceOrigin`, the switch
DTOs and the new message / field-error keys. The test-writer may not change
production code — the gap US-025 (OD-009), US-027 (OD-005), US-037 (OD-007) and
US-039 (OD-008) resolved the same way. Unlike those Stories, existing types
change here.

Options: (a) a compile-only skeleton — only the declarations the tests
reference; new members throw `NotImplementedException`; new parameters of
existing records and methods are added last and optional; new entity
properties are not mapped by EF yet; nothing registered in DI, no migration, no
existing behaviour changed; IMPLEMENTATION owns and completes it; (b) no
skeleton — tests that do not compile until IMPLEMENTATION.

**Resolution:** (a) — Owner, 2026-10-05.
