---
artifact_type: entity_model
story: US-042
version: 1
status: DRAFT
created_at: 2026-10-05T11:29:58Z
updated_at: 2026-10-05T11:29:58Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
  - path: docs/designs/database/US-042-db-design.md
    version: 1
  - path: docs/designs/database/US-027-entity-model.md
    version: 1
supersedes: null
---

# US-042 Entity Model — Name parts and the template's name source

Design rationale and every constraint: `docs/designs/database/US-042-db-design.md`.
Names of entities, properties, enum members and record fields are fixed by this
stage; method signatures are indicative where marked.

## 1. Domain

### 1.1 New enum `ReportNameSource` (`ClassroomAgent.Domain.Enums`)

| Member | Stored code | Form / query value | Source |
|---|---|---|---|
| `Profile` | `profile` | `profile` | FR-002 |
| `Email` | `email` | `email` | FR-002 |

Stored through an explicit value converter in `ReportTemplateConfiguration`
(US-027 pattern); never as an integer.

### 1.2 `ClassroomParticipant` (changed)

```csharp
public const int MaxSurnameLength = 750;     // db-design D-2
public const int MaxGivenNameLength = 750;   // db-design D-2

/// Google name.familyName; optional; personal data.
public string? Surname { get; private set; }

/// Google name.givenName; optional; personal data.
public string? GivenName { get; private set; }

// indicative signatures — both now carry the two parts
public static ClassroomParticipant Import(
    string googleUserId, string? email, string? fullName, string? surname, string? givenName);
public void UpdateFrom(string? email, string? fullName, string? surname, string? givenName);
```

- `Apply` normalises `surname` and `givenName` exactly as `fullName`: trim; blank →
  null; cut to the bound (FR-001, VR-003). No case change, no splitting of
  `fullName`.
- `UpdateFrom` **replaces** all four values (AC-001).
- The submitter-only import (`RunSynchronizationUseCase`, US-015 OD-006) passes
  null for every optional value, as today.

### 1.3 `ReportTemplateSettings` (changed, `ClassroomAgent.Domain.Rules`)

One positional member added **last**:

```csharp
public sealed record ReportTemplateSettings(
    ReportView View,
    bool HideMaterials,
    int HoursPerLesson,
    ReportScaleMode ScaleMode,
    IReadOnlyList<ReportScaleRow> ScaleRows,
    IReadOnlyDictionary<ReportCellState, ReportMark> Marks,
    ReportLateMark LateMark,
    ReportNameSource NameSource);
```

- `BuiltInReportTemplates.AcademicJournal` gets `ReportNameSource.Profile`
  (FR-002, D-7).

### 1.4 `ReportTemplate` (changed)

```csharp
public ReportNameSource NameSource { get; private set; }
```

- `Apply` (create, copy, change) sets it from `settings.NameSource`; an undefined
  enum value throws `ArgumentException` (programming error — `Application`
  validates first, US-027 pattern).
- `ToSettings()` returns it.

## 2. Persistence mapping

| Entity.Property | Column | Configuration |
|---|---|---|
| `ClassroomParticipant.Surname` | `surname varchar(750) NULL` | `HasMaxLength(MaxSurnameLength)` |
| `ClassroomParticipant.GivenName` | `given_name varchar(750) NULL` | `HasMaxLength(MaxGivenNameLength)` |
| `ReportTemplate.NameSource` | `name_source varchar(8) NOT NULL` | `HasMaxLength(8).IsRequired().HasConversion(code ↔ enum)`; check `ck_report_template_name_source` |

No index, no foreign key, no `HasDefaultValue` (db-design D-5, D-6).

## 3. Application ports and records

### 3.1 `RosterEntry` (`Application/Models`, changed)

```csharp
public sealed record RosterEntry(
    string GoogleUserId, string? Email, string? FullName, string? Surname, string? GivenName);
```

`GoogleClassroomReader.Entry` maps `profile?.Name?.FamilyName` → `Surname` and
`profile?.Name?.GivenName` → `GivenName`; no Google SDK type crosses into
`Application` (AD-4). Scopes unchanged — the name is part of the roster profile
already read (spec S-05).

### 3.2 `JournalCourseMemberRecord` (`Application/Models`, changed)

Used only by the report (`IJournalFieldSource.GetMembersAsync`, F3).
`FullName` is replaced by the two parts (spec FR-006, I-6):

```csharp
public sealed record JournalCourseMemberRecord(
    long ParticipantId,
    ClassroomRole Role,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    bool OnRoster,
    string? Surname,
    string? GivenName,
    string? Email);
```

`JournalMemberRecord` (US-025 journal) is **unchanged** — it keeps `FullName`.

### 3.3 The name function (spec FR-003, I-10)

One report-independent function in `Application` (indicative):

```csharp
internal static class ReportPersonNameRule
{
    // (null, Unnamed) when FR-003 yields nothing; the caller picks the student/teacher label.
    public static (string? Name, ReportNameKind Kind) Label(
        string? surname, string? givenName, string? email, ReportNameSource source);
}
```

`EmailLocalPart` = the stored address up to its first `@`; an address without
`@` or with nothing before it counts as no email (FR-003).

## 4. Mapping to API DTOs (openapi US-042)

| Business concept | Domain / Application | DTO (openapi) |
|---|---|---|
| Template setting "names" | `ReportTemplate.NameSource`, `ReportTemplateSettings.NameSource` | `ReportTemplateForm.names` (string, via `ReportTemplateFormValues.Names`) |
| Effective name source | `ReportNameSource` | `Report.nameSource` |
| Where it came from | new Application DTO enum `NameSourceOrigin` (`Page`, `Template`) | `Report.nameSourceOrigin` |
| Displayed name | `ReportPersonNameRule.Label` | `PersonName.displayName` |
| Which rule produced it | new Application DTO enum `ReportNameKind` (`Profile`, `EmailLocalPart`, `Unnamed`) | `PersonName.nameKind` (replaces `JournalNameKind` in the report's `PersonName` only) |
| The switch | built in `GetReportQuery` from validated values | `ReportPageModel.nameSwitch` |

Domain enums may appear in DTOs (US-027 precedent: `ReportCellState`); entities
never do (AD-8).

## 5. Expected migration effect

Exactly as db-design §4: two nullable columns on `classroom_participant`; one
non-null column with a check on `report_template`, existing rows `profile`, no
lasting default. The snapshot gains these three properties and the check, and
nothing else.
