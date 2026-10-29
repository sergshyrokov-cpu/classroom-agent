---
artifact_type: database_design
story: US-064
version: 1
status: DRAFT
created_at: 2026-10-29T07:12:51Z
updated_at: 2026-10-29T07:12:51Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/designs/api/US-064-api-design.md
    version: 1
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
supersedes: null
---

# US-064 Database Design — A disabled Dean's open session is refused on the next request

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-064-entity-model.md`**.

Delegation: none. The designer checked directly where the per-request session check reads the
account (`AppUserRepository.GetSessionStateAsync`, port `IAppUserRepository.cs:23`).

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the
Story does not change persistence behaviour. The approved Specification (v1) states it:

- **§1:** the Story "is expected to change tests only".
- **§9:** "No new NuGet package, no migration … DB_DESIGN [is] expected `NOT_APPLICABLE`."
- **FR-001:** tests drive disabling through the existing HTTP endpoint; no test writes `AppUser`
  or the database directly.

The API design (v1) is NOT_APPLICABLE as well.

## 2. What the Story relies on, and where it already lives

| Spec element | Stored data (exists) | Read / written today by |
|---|---|---|
| Disabled state (FR-004…FR-007) | `AppUser.IsDisabled` | written by `AppUser.Disable` / `ReEnable`; read by `GetSessionStateAsync` |
| Security stamp (FR-004, FR-006) | `AppUser.SecurityStamp` | rotated by `Disable`; read by `GetSessionStateAsync` on every request |
| Export audit count (FR-005) | `audit_event` rows, action `journal_exported` | written by `ExportJournalCommand` |

No column, index, constraint, migration or query changes. FR-008 (a defect fix, if a test fails)
is not expected to touch the schema; should it need to, the implementation stops and the change
goes back through this stage.

## 3. Open findings

US-064 has no OPEN findings.
