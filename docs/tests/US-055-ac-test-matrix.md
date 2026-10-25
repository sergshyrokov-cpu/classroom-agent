---
artifact_type: ac_test_matrix
story: US-055
version: 1
status: DRAFT
created_at: 2026-10-25T12:30:00Z
updated_at: 2026-10-25T12:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/designs/database/US-055-db-design.md
    version: 1
  - path: docs/tests/US-055-test-strategy.md
    version: 1
supersedes: null
---

# US-055 Acceptance Criteria → Test Matrix

Classes (all under `tests/ClassroomAgent.Tests/`):
**P** = `Infrastructure/Persistence/GradeColumnsPersistenceTests`,
**S** = `Web/BackgroundServices/LargeGradesSyncTests`,
**W** = `Web/Pages/LargeGradesPageTests`.

Status is the red-phase state before IMPLEMENTATION (see the test generation
report for the evidence).

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Run stores maximum points 12 345 678.5 | Integration (host) | S | `LargeMaximumPoints_AreStoredAsReturned_AndTheRunCompletes` | run `completed`, stored 12345678.5 | red |
| AC-001 | EF Core write/read of 12 345 678.5 | Integration (persistence) | P | `LargeAndFineValues_AreStoredExactly_AndReadBackEqual("12345678.5", …)` | equal in SQL and through EF Core | red |
| AC-002 | Run stores 2 500 000 / 1 000 000.25 | Integration (host) | S | `LargeGrades_AreStoredAsReturned_AndTheRunCompletes` | run `completed`, both stored exactly | red |
| AC-002 | EF Core write/read of the grades | Integration (persistence) | P | `LargeAndFineValues_AreStoredExactly_AndReadBackEqual("12345678.5", "2500000", "1000000.25")` | equal | red |
| AC-003 | Run stores 10.123456 / 7.654321 unrounded | Integration (host) | S | `SixDecimalPlaces_AreNotRounded` | decimal equal and text `10.123456`, `7.654321` | red |
| AC-003 | EF Core write/read of six and more decimals | Integration (persistence) | P | `LargeAndFineValues_AreStoredExactly_AndReadBackEqual("10.123456", …)` and the 26-digit case | equal | red |
| AC-004 | Three courses, second above old ceiling | Integration (host) | S | `TheCoursesAfterTheUnusualOne_StillLoad_AndTheMeetStepRuns` | 3 courses + 3 items, Meet port called, `meet_loaded_up_to` set, `completed` | red |
| AC-005 | Values at the old schema survive `WidenGradeColumns` | Integration (persistence) | P | `TheMigration_KeepsEveryStoredValueAndNull` | identical text for every value and null | red |
| AC-005 / FR-003 | Migration exists under the designed name and order | Integration (persistence) | P | `TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne` | one `*_WidenGradeColumns` after `20261015203416_AddNotAnAdminAccountRefusal` | red |
| FR-001 | Columns unconstrained `numeric`, nullable | Integration (persistence) | P | `TheThreeColumns_AreUnconstrainedNumeric_AndNullable` | `numeric|-|-|YES` × 3 | red |
| FR-003 (design §7) | CHECK constraints kept | Integration (persistence) | P | `NegativeValues_AreStillRefusedByTheExistingChecks` | 23514 with the existing constraint names | green by design |
| AC-006 | Journal shows grade over maximum; a second, ungraded student's large draft | Integration (HTTP, Dean) | W | `TheJournal_ShowsTheLargeGradeAndMaximum_AsStored` | 200; `2500000 / 12345678,5`, `1000000,25` | red (corrected at IMPLEMENTATION with Owner OK, see implementation report §7) |
| AC-006 | Raw-points report shows grade over maximum | Integration (HTTP, Dean) | W | `ARawPointsReport_ShowsTheLargeGradeAndMaximum_AsStored` | 200; `2500000 / 12345678,5` | red |
| AC-006 (m-1) | Built-in scale report does not fail | Integration (HTTP, Dean) | W | `TheBuiltInScaleReport_DoesNotFail_OnTheLargeValues` | 200; the task is listed | red |
| AC-006a | Report export (table layout) carries the values | Integration (HTTP, Dean) | W | `TheReportExport_CarriesTheLargeGradeAndMaximum_AsStored` | 200 xlsx; a cell with `2500000 / 12345678,5` | red |
| S-04 | Completed run logs none of the values | Integration (host) | S | `ACompletedRunWithLargeValues_LogsNoneOfThem` | run `completed`; no value in the log | red |

Every Acceptance Criterion (AC-001 … AC-006, derived AC-006a) has at least one
mapped test.
