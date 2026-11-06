---
artifact_type: entity_model
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T19:59:31Z
updated_at: 2026-11-06T19:59:31Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/designs/api/US-074-openapi.yaml
    version: 1
  - path: docs/designs/database/US-074-db-design.md
    version: 1
supersedes: null
---

# US-074 Entity Model

No domain entity changes: `MeetSession`, `ClassroomParticipant` and their configurations are untouched. The changes
are in the Application read models and DTOs that carry what the read source returns (spec FR-002, API design §1).

## 1. New Application model

```csharp
namespace ClassroomAgent.Application.Models;

/// <summary>One organizer of a Meet code as stored (US-074 db-design §4): the email and the chosen participant name
/// before the blank test, null when no participant of that email has a name.</summary>
public sealed record MeetOrganizerRecord(string Email, string? StoredFullName);
```

File `src/ClassroomAgent.Application/Models/MeetOrganizerRecord.cs`. Business concept: "organizer of a code"
(spec FR-001). `Email` is never null (an erased/excluded organizer is not listed).

## 2. Changed Application row models

In each, `IReadOnlyList<string> OrganizerEmails` is **replaced** by `IReadOnlyList<MeetOrganizerRecord> Organizers`,
same position in the positional record:

| Record | File |
|---|---|
| `UnassignedCodeRow` | `src/ClassroomAgent.Application/Models/UnassignedCodeRow.cs` |
| `LinkedCodeRow` | `src/ClassroomAgent.Application/Models/LinkedCodeRow.cs` |
| `MarkedCodeRow` | `src/ClassroomAgent.Application/Models/MarkedCodeRow.cs` |

The list is distinct by lower-cased email and ordered by `Email`, ordinal (db-design §4).

## 3. Changed DTOs (API design, OpenAPI `MeetCodesPageModelDelta`)

In each, `IReadOnlyList<string> OrganizerEmails` is **replaced** by `IReadOnlyList<PersonRef> Organizers`, same
position:

| DTO | File |
|---|---|
| `UnassignedCodeItem` | `src/ClassroomAgent.Application/Models/Dtos/UnassignedCodeItem.cs` |
| `LinkedCodeItem` | `src/ClassroomAgent.Application/Models/Dtos/LinkedCodeItem.cs` |
| `NotACourseCodeItem` | `src/ClassroomAgent.Application/Models/Dtos/NotACourseCodeItem.cs` |

`PersonRef(string? FullName, string? Email)` is the existing DTO (`Models/Dtos/PersonRef.cs`), unchanged.

**Mapping** in `GetMeetCodesQuery` (Application, AD-8): each `MeetOrganizerRecord r` becomes
`new PersonRef(MeetReportRules.Name(r.StoredFullName), r.Email)` — the blank test of db-design §2 step 3, through the
same `internal` method the Meet reports use (same assembly). No domain entity in any DTO.

## 4. Infrastructure (private, not an entity)

`MeetCodesReadSource` gains a private `sealed record OrganizerNameRow(string LowerEmail, string FullName)` and the
private query `OrganizerNames()` (db-design §3). `LoadOrganizersAsync` returns
`Dictionary<string, List<MeetOrganizerRecord>>`; `OrganizersOf` returns `IReadOnlyList<MeetOrganizerRecord>`.

Cases TEST_WRITING must cover for the name rule (db-design §2, closes m-1):

| Participants of one email (id order) | Shown |
|---|---|
| none | email alone |
| `null` | email alone |
| `'Петренко Іван'` | name + email |
| `null`, `'Петренко Іван'` | name + email (null is not a candidate) |
| `'  '`, `'Петренко Іван'` | email alone (blank chosen first, then the blank test) |
| `'Петренко Іван'`, `'Іваненко Петро'` | `Петренко Іван`; a search for `Іваненко` does not find the code through this organizer (I-3) |

For each row the Meet reports show the same name for the same data (AC-009).

## 5. Mapping to business concepts

| Concept (spec) | Model |
|---|---|
| Organizer of a code (FR-001) | `MeetOrganizerRecord` → `PersonRef` |
| Organizer's name (FR-001) | `OrganizerNames()` + `MeetReportRules.Name` |
| Organizer cell (FR-002) | `*CodeItem.Organizers` rendered by `_MeetPerson.cshtml` |
| Search by organizer name (FR-003, FR-004) | `CodesByOrganizer` name branch |
