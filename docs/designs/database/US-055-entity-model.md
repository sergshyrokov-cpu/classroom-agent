---
artifact_type: entity_model
story: US-055
version: 1
status: DRAFT
created_at: 2026-10-25T11:45:00Z
updated_at: 2026-10-25T11:45:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/designs/api/US-055-api-design.md
    version: 1
  - path: docs/designs/database/US-055-db-design.md
    version: 1
supersedes: null
---

# US-055 Entity Model — Points and grades of any size are stored as Google returned them

**No C# signature changes.** The entity model is unchanged; only the column
mapping of three existing properties changes (DB design §3.1). There is
therefore nothing for a TEST_WRITING skeleton to compile against beyond the
existing code.

## 1. Properties and their mapping

| Entity (Domain) | Property | CLR type | Column | Before | After | Business concept |
|---|---|---|---|---|---|---|
| `CourseWork` | `MaxPoints` | `decimal?` | `course_work.max_points` | `numeric(10,4)` | `numeric` | §3 «максимум баллов»; null = ungraded work (BR-052) |
| `Submission` | `AssignedGrade` | `decimal?` | `submission.assigned_grade` | `numeric(10,4)` | `numeric` | §3 `assignedGrade` «в баллах как есть» |
| `Submission` | `DraftGrade` | `decimal?` | `submission.draft_grade` | `numeric(10,4)` | `numeric` | §3 `draftGrade` «в баллах как есть» |

The properties are set through the existing `CourseWorkDetails` /
`SubmissionDetails` value records, unchanged.

## 2. Carriers outside the schema (unchanged)

All already `decimal?`, so a value larger than the old column limit flows
through without a type change:

- Application port: `CourseWorkSnapshot` → `CourseWorkDetails.MaxPoints`,
  `SubmissionSnapshot.AssignedGrade/DraftGrade` (filled by
  `GoogleClassroomReader` with the existing `(decimal)double` cast — spec I-1).
- Read models: `JournalItemRecord.MaxPoints`, `JournalLessonRecord.MaxPoints`,
  `JournalSubmissionRecord.AssignedGrade/DraftGrade`.
- DTOs: `JournalCell.Points/MaxPoints/DraftPoints`, `JournalColumn.MaxPoints`,
  `ReportGrade.Points/MaxPoints`; export `WorkbookCell.Number`.

No domain entity crosses into a DTO (AD-8); the mapping in `Application` is
unchanged.

## 3. Configuration files touched

- `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseWorkConfiguration.cs`
  — `HasColumnType("numeric")` for `MaxPoints`.
- `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs`
  — `HasColumnType("numeric")` for `AssignedGrade` and `DraftGrade`.
- New migration `WidenGradeColumns` and the updated
  `ClassroomAgentDbContextModelSnapshot.cs`.
