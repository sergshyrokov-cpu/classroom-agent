---
artifact_type: api_design
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T19:10:38Z
updated_at: 2026-10-27T19:10:38Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 103
  - path: docs/designs/api/US-032-api-design.md
    version: 1
supersedes: null
---

# US-062 API Design — Data-health banner, «Стан даних» and the Admin's confirmation

Companion to `docs/designs/api/US-062-openapi.yaml`.

Delegated: none. Existing routes (`SignInRoutes`), policies (`InstallationPolicies`), the shared
layout, the Meet-codes form pattern (US-032) and the read-only refusal audit of existing use cases
(`LiftErasureExclusionUseCase`) were read directly; the YAML was checked by a parser.

## 1. Scope of the contract

| Surface | Change | Policy | Writes |
|---|---|---|---|
| Banner in `_Layout.cshtml` | **new** layout element, not an endpoint | session of Admin or Dean | nothing |
| `GET /workspace/data-health` | **new** page | `ViewDataHealth` (Admin, Dean) | nothing |
| `POST /workspace/data-health/course-deletion-confirmation` | **new** form post | `ConfirmCoursesDeleted` (Admin) | marks + one audit row |

No `/api/v1` operation is added or changed; `ContractVersion.Current` stays 1. Nothing calls Google.

## 2. Decisions this stage made

### 2.1 Routes

`/workspace/data-health` sits beside the other teaching pages (`/workspace/journal`,
`/workspace/courses`); the page shows teaching data to both roles. The confirmation is a sub-resource
noun, `…/course-deletion-confirmation`, as US-032's `…/confirmation` and `…/not-a-course-mark`
(API-3: no verbs). Both go into `SignInRoutes` as constants (`DataHealth`,
`DataHealthCourseDeletionConfirmation`) with the TempData key `DataHealthMessage`.

### 2.2 Two new policies

`ViewDataHealth` — Admin ✔, Dean ✔ (§2 row 749). `ConfirmCoursesDeleted` — Admin ✔, Dean ✘ (§2 row
750). Added to `InstallationPolicies` with summaries quoting the rows, as the existing ones. No
existing policy fits: `ViewJournal` would tie the page to an unrelated matrix row, and the Admin-only
policies name other actions.

### 2.3 The banner is a layout element computed in Application

The layout asks one `Application` query for `DataHealthBannerModel` (two counts). How the layout
obtains it — a view component or a filter populating `ViewData` — is IMPLEMENTATION's choice,
provided: the query is the only source (no counting in `Web`, AD-3); it is not called for an
anonymous request or on `/sign-in/change-password` (OD-001 a); a failure is caught at that one call
site, logged at `Warning` with the exception type only, and leaves the model null (spec I-4, §8);
the error page rendered for a signed-in user follows the same rule as any layout page. The counts
equal the page's: the same rules from one place feed both (TEST_WRITING asserts banner = page totals).

### 2.4 Evaluation order of the confirmation

As US-032 §2.5: authentication → restricted session → policy → antiforgery → **read-only guard** →
shape (VR-001) → transaction { state (VR-002) → marks → audit } → commit → `302`. Consequences:

- a Dean is refused `403` before anything is read, read-only or not (AC-011);
- in read-only mode an Admin is told "read-only" even for a malformed or stale form;
- a shape failure is `400` and a stale list `409`; neither writes anything.

### 2.5 Audit of a refused confirmation (non-blocking finding m-1)

The Specification names the success event only (FR-006.2 step 5) and is silent on the read-only
refusal. SC-11 gives every row an outcome "succeeded / refused", and every existing audited Admin
action refused in read-only mode writes a refused row (`LiftErasureExclusionUseCase`,
`EraseParticipantUseCase`, Dean account use cases, Meet-code writes — US-032 §2.5); BR-026 permits
audit rows as a service write. The contract follows that pattern: one refused row, refusal category
read-only, actor the Admin, **no course ids** (unverified input, as US-032). A `400`, a stale `409`
and a Dean's `403` write none (AC-017 "no audit event" covers the stale case). Raised as m-1 so the
Owner sees the choice; reversing it is a one-line change before TEST_WRITING.

### 2.6 Status codes

| Situation | Status | Body |
|---|---|---|
| Confirmation succeeded | `302` → `/workspace/data-health` | TempData `DataHealthMessage` `Confirmed` + count (PRG, as US-032) |
| Antiforgery missing / invalid | `400` | host error page `Error.PageExpired` |
| VR-001 shape failure | `400` | the page, `message` `Invalid` |
| A Dean | `403` | host error page `Error.Forbidden` |
| Read-only mode | `409` | host error page naming the BR-025 reason |
| Stale list or unknown id (VR-002) | `409` | the page, `message` `ListChanged` |
| Not signed in | `302` → `/sign-in` | — |
| Restricted session | `302` → `/sign-in/change-password` | — |

The page re-rendered on `400`/`409` is the current state (a fresh `GetDataHealthQuery`), so the
Admin sees the list as it now is.

### 2.7 Form binding

`CourseDeletionConfirmationForm.courseIds` — a repeated form field, one per ticked checkbox, bound as
strings and parsed in `Application` (US-027 §2.7). Duplicates are removed before the 1…1000 bound
(VR-001, I-6). The checkbox value is the internal `Course` id: the course is stored and the Admin
sees its list; no Google id travels in the form.

### 2.8 The view model is role-shaped in Application

`GetDataHealthQuery` takes the caller's role and returns `DataHealthPageModel` with
`TechnicalProblem.details` null for a Dean (spec §7, AD-8). The two X6 dates and X4's date are
title-level members, present for both roles (I-2, FR-007 X4). OD-004 a): an archived X3 entry for a
Dean has no course name and no journal link. `isAdmin` only switches H3/H5 and shows the form; it
is not the enforcement.

### 2.9 Not paginated (deviation recorded)

API-8 paginates growing collections. This page exists to show every problem the banner counts;
paginating it would hide some. Per-course rows are bounded by the school's courses, T1 rows by
entry errors teachers fix. Recorded as `x-pagination-deviation`, as US-012 §2.10 and US-032 §2.6.

### 2.10 Order on the page

Technical first (FR-004.2), in the order X1, X2, X6, X4, X5, then X3 per course in culture order —
problems without a course before per-course ones. Teaching and archived per FR-004.3/FR-004.4.

## 3. Operation notes

### `GET /workspace/data-health`
Reads only; the read-only guard is not called. `readOnly` set from `GetLegitimacyModeQuery` and
rendered with `Landing.ReadOnly.*` (FR-004.5, I-3). Page-search hint: the ordinary NFR-075 variant.
Language-switcher return path: the bare page path (no query string, no personal data).

### Banner
See §2.3. Plural forms via the existing localization; keys `DataHealth.Banner.*`.

### `POST …/course-deletion-confirmation`
Order in §2.4. Marks and the audit row are one transaction; how it excludes a concurrent
synchronization changing the same courses is DB_DESIGN's decision (spec §8). Log: `Information` with
actor id and the number of courses on success; `Warning` with the rule name on a refusal — no course
names (SC-10).

## 4. Authentication and authorization model

Cookie session of US-008. `GET` — `ViewDataHealth`; its TC-5 forbidden case is the anonymous request
(`302` to `/sign-in`) and the restricted session. `POST` — `ConfirmCoursesDeleted`; forbidden case a
Dean (`403`, AC-011), plus anonymous and restricted. SC-4 anonymous list unchanged; the endpoint
enumeration tests see both paths as protected, and the SC-14 test sees both as `no-store`. The
antiforgery token is a hidden form field.

## 5. Error model

Server-rendered pages only; the API-6 JSON body does not apply. New keys in `SharedResource`
(uk, en): `DataHealth.*` — title, banner forms, problem titles and hints of FR-007 and OD-003, section
headings, `DataHealth.Message.Confirmed` / `Invalid` / `ListChanged`, the confirm button. Reused:
`Error.PageExpired`, `Error.Forbidden`, `Landing.ReadOnly.*`, `LastSync.Diagnosis.*`. No message
contains a course name or id supplied by the request.

## 6. Acceptance Criterion → operation map

| AC | Surface | Status codes |
|---|---|---|
| AC-001 | banner | 200 |
| AC-002 | banner (absent) | 200 |
| AC-003 | banner, `getDataHealth` archived | 200 |
| AC-004 | banner, `getDataHealth` | 200 |
| AC-005 | `getDataHealth` teaching | 200 |
| AC-006 | `getDataHealth` technical, role-shaped | 200 |
| AC-007 | `getDataHealth` X2 | 200 |
| AC-008 | `getDataHealth` X4 | 200 |
| AC-009 | `getDataHealth` X5 | 200 |
| AC-010 | `confirmCoursesDeleted` | 302 |
| AC-011 | `confirmCoursesDeleted` | 403, 409 |
| AC-012 | banner, `getDataHealth` in English, read-only | 200 |
| AC-013 | banner, `getDataHealth` X6 | 200 |
| AC-014 | `getDataHealth` read-only notice | 200 |
| AC-015 | banner absent on `/sign-in/change-password` | 200 |
| AC-016 | — (synchronization and migration; DB_DESIGN) | — |
| AC-017 | `confirmCoursesDeleted` stale | 409 |

TC-3 for the two routes: every status of §2.6 (`302` ×3 causes, `400` ×2, `403`, `409` ×2) and the
`GET`'s `200` and `302` ×2 have a test.

## 7. Compatibility

Additive: two routes, two policies, one layout element. No existing page, view model or message
changes, except that every layout page gains the optional banner.

## 8. Open questions for later stages

None blocking. For DB_DESIGN: the counting queries and any index (spec §9); the `SyncState`
empty-list date column and migration (FR-008); the domain operation confirming a
`NotVisibleToTechnicalAccount` mark into `NotInClassroom` (FR-006.2 step 4); the concurrency of the
confirmation transaction; the `AuditAction` value `CoursesConfirmedDeleted` and how the row carries
several course ids.
