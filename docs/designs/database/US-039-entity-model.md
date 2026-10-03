---
artifact_type: entity_model
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T08:54:15Z
updated_at: 2026-10-03T08:54:15Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/designs/api/US-039-openapi.yaml
    version: 1
  - path: docs/designs/database/US-039-db-design.md
    version: 1
supersedes: null
---

# US-039 Entity Model — Choose UI language

No entity or property is added; one domain method is added. Design rationale:
`docs/designs/database/US-039-db-design.md`.

## 1. Changes

### `ClassroomAgent.Domain.Entities.AppUser` (installation)

```csharp
/// <summary>The user's own choice of UI language (US-039 spec FR-004). Not a credential change: the
/// security stamp is not rotated.</summary>
public void ChooseUiLanguage(UiLanguage language)
```

- Rejects a value that is not a defined `UiLanguage` member
  (`ArgumentOutOfRangeException`) — a programming error, never a user input
  path: validation of the submitted code happens earlier, in `Application`
  (spec VR-001).
- Sets `UiLanguage`; renews `ConcurrencyStamp`. Nothing else.

### `ClassroomAgent.ControlPlane.Persistence.Owner` (Control Plane)

No change to the class. The `Services` method assigns `UiLanguage` and
`ConcurrencyStamp`.

## 2. Mapping to business concepts

| Property | Business concept | Source |
|---|---|---|
| `AppUser.UiLanguage` | the Admin's / Dean's chosen interface language, followed on every device | `trebovaniya.md` §5 (v51), §3 `AppUser` |
| `Owner.UiLanguage` | the Owner's chosen Control Plane language | `trebovaniya.md` §5, §9 |

## 3. Mapping to API DTOs

| DTO / form (openapi) | Field | Entity side |
|---|---|---|
| `ChooseUiLanguageForm.language` (`uk`/`en`, string) | → `UiLanguage.Uk` / `UiLanguage.En` (Domain or ControlPlane enum), mapped in `Application` / `Services` | `AppUser.UiLanguage` / `Owner.UiLanguage` |
| `ChooseUiLanguageForm.returnPath` | — not persisted | — |
| `LanguageSwitcherModel.currentLanguage` | from the request culture, not from an entity | — |

The session's language claim is written from the accepted code, not by re-reading
the entity; no entity crosses into a controller or view (AD-8).
