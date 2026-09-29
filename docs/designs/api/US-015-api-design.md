---
artifact_type: api_design
story: US-015
version: 2
status: DRAFT
created_at: 2026-09-28T12:37:36Z
updated_at: 2026-09-28T13:03:14Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/decisions/US-015-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-015 API Design — Sync coursework and submissions

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-015-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — the same decision
US-007, US-013 and US-014 recorded for the same reason. A contract file
describing zero operations would assert a surface this Story does not have.
Downstream stages that list `openapi` among their inputs read this document
instead.

## 0. Why this artifact is at version 2

Version 1 recorded **specification version 1** in its `inputs`. The Specification
is now **v2** — the natural keys are scoped by their parent — so by the staleness
contract of `artifact-schema.md` version 1 of this document was **stale** and
would have blocked `DB_DESIGN`.

The stage therefore **re-ran** rather than having v1's input version edited in
place: recording that a stage read a version it never read would defeat the
contract it exists to serve. The same choice was made for US-014.

**The reassessment, done rather than assumed.** The v2 correction changes a
**unique index and an upsert key** — `course_work` on
`(course_id, resource, google_id)` and `submission` on
`(course_work_id, google_id)`. Neither is an API concern:

- no endpoint, path, request body or response body exists in this Story to carry
  an id at all (FR-019);
- the keys are Google-side natural keys used by synchronization, not resource
  identifiers a client would ever send — API-3 identifies a resource by its
  surrogate `Id`, and no path template is introduced here;
- the Specification's four no-surface statements (FR-019, S-01, S-08, §10) were
  **re-read against v2** and are unchanged from v1.

The verdict is unchanged: **NOT_APPLICABLE**. The stale input is cleared.

**One forward-looking consequence v2 adds for EPIC-3**, recorded here so it is
not re-derived: because a Google `courseWork.id` is unique only *within its
course* and a `studentSubmission.id` only *within its course work*, a future
endpoint **must not address either resource by its Google id alone**. It uses the
surrogate `Id` (PC-3, API-3), or a path nested under the course — a flat
`/coursework/{googleId}` would be ambiguous across courses. This is the API-side
shadow of the same fact that produced v2, and it binds US-025 and US-026.

## 1. Why the stage does not apply

`stage-map.yaml` makes `API_DESIGN` optional when "the approved Specification
explicitly states the Story does not change public API behavior". The approved
Specification (v1) states it in four places, and each was re-read against this
verdict rather than assumed:

| Where | What it states |
|---|---|
| **FR-019** | The Story adds no endpoint, no Razor page, no authorization policy and no route; SC-4's anonymous list is unchanged and the US-008 endpoint enumeration test gains no row |
| **S-01** | No endpoint, page, policy or route is added |
| **S-08** | The imported data has **no read path** until Epic 3 — no controller, page or view model exposes a `CourseWork` or a `Submission` |
| **§10 Out of Scope** | Every screen that would show this data is US-024 … US-030 |

The Story's own Scope says the same: "this Story adds no page, no endpoint and no
translation key".

## 2. What the Story does deliver, and why none of it is a contract

| Delivered | Why it has no API dimension |
|---|---|
| Two entities and two tables (`CourseWork`, `Submission`) with one migration | Persistence only — `DB_DESIGN` owns it (FR-018) |
| New reads on `IClassroomReader` for coursework, materials and submissions | An **outbound** port to Google, not an inbound HTTP surface. API conventions govern what this program serves, not what it calls (AD-4) |
| The §5 age rule for a not-yet-imported course (FR-011) | A rule inside the synchronization use case; no response changes |
| The off-roster membership for a submitter never seen on a roster (FR-007) | A write inside the same use case |
| The submission-state vocabulary of VR-004 (six values plus the unrecognised marker) | A stored value with a check constraint. It becomes an API concern only when a journal DTO exposes it — **US-025**, see §4 |
| `Retention:Years` becoming a required setting (FR-012) | Configuration, read at startup. See §3, where it is examined rather than waved away |

## 3. Existing contracts this Story must not change

Re-checked one by one, because a "no change" verdict is only worth the checking
behind it:

- **Readiness and liveness on the private port** (US-005, DC-11) — untouched.
  This Story does not touch readiness at all, unlike US-013, which changed which
  **states** the existing readiness endpoint reports. A readiness change appearing
  in this Story is out of scope.
- **The Control Plane push receiver** (US-006) — untouched.
- **The public-port pages of US-008 … US-012** (sign-in, workspace connection,
  instructions, check access, Dean accounts) — untouched.
- **The service channel and `ClassroomAgent.Contracts`** — untouched;
  `ContractVersion` stays **1**.
- **The Control Plane** — untouched.

### The one thing that deserved a second look: FR-012

FR-012 makes `Retention:Years` a required installation setting: absent,
unparsable or non-positive, and the installation **refuses to start** (DC-3,
PC-11, §5). That is a behaviour change at deployment, so it was checked against
this verdict explicitly.

It is **not** an API change. No endpoint answers differently: a process that does
not start serves nothing at all, and no response body, status code or route is
affected. It is also not a health-check change — DC-11's endpoints keep their
contract; they simply never run in a misconfigured installation, exactly as they
already never run when the installation id, the Control Plane address or the
OAuth client id is missing (DC-3, US-005, US-008).

Recorded here so `SECURITY_REVIEW` and `DC-3` maintenance do not have to
rediscover it: **the deployment note belongs in `deployment-conventions.md` DC-3**
(the key name, as US-013 added `Sync:IntervalMinutes`), not in an API contract.

## 4. What later Stories inherit

Recorded so EPIC-3 does not re-derive it:

- **US-025 (journal for a period) is the first consumer** and owns the two
  obligations this Story's Open Decisions carried out of the import layer:
  1. the **cell rule for `NEW`** — BR-056 does not describe it, and a journal that
     renders `NEW` as «не сдано» without that rule being written is deciding it
     silently (OD-011);
  2. the **rendering of an unrecognised state** — its own thing, never «не сдано»
     and never a grade (OD-005).
- **A journal DTO returns raw points with the coursework's maximum**, never a
  converted school grade — the conversion lives in report templates (PC-13,
  VR-007). A DTO that returned a converted number would move a school's grading
  scale into the API.
- **Grades are personal data.** Any future endpoint exposing a `Submission` is
  Admin/Dean only, deny-by-default like every other (SC-2, SC-4), and a grade
  never appears in a log line or an error body (SC-10, S-06).
- **Pagination applies when the journal is exposed** (API-8: default 20, maximum
  100). Submissions are the largest collection in the system —
  courses × assignments × students (NFR-002) — so an unpaginated journal
  endpoint would be a finding.
- **DTOs, never entities** (AD-8), mapped in `Application`.
- **Neither coursework nor a submission is addressed by its Google id alone**
  (§0): those ids are unique only within their parent, so an endpoint uses the
  surrogate `Id` or a course-nested path.
- **US-024 (statistics)** still shows the `SyncState` counter labelled "courses
  processed" — unchanged by this Story (FR-014, US-014 OD-005). A course skipped
  by the age rule is not counted and leaves no trace in `SyncState` (I-5), so a
  future screen must not present the counter as "everything Google has".

## 5. Acceptance Criterion → operation map

No Acceptance Criterion of this Story maps to an HTTP operation.

| AC | Surface |
|---|---|
| AC-001 … AC-005, AC-010 | Synchronization pipeline + persistence — none |
| AC-006 | Read-only enforcement in `Application` (SC-5, AD-6) — none |
| AC-007, AC-008 | Run state, logging and audit — none |
| AC-009 | Tests — none |

## 6. Auth model

Unchanged. The Story adds no endpoint, so it declares no authorization policy and
SC-4's closed list of anonymous access does not grow (FR-019, S-01). The
synchronization run is a background service with no HTTP caller; its protection
is the read-only guard in `Application`, not an endpoint policy (FR-016, S-02).

## 7. Error model

Unchanged. No new response body exists, so `api-conventions.md` API-6's error
shape is neither extended nor used. Failures inside the run are recorded in
`SyncState` as `RunFailed:<type>` (FR-015) and reach no HTTP client — a
diagnosable Admin-facing message is US-017.

## 8. Compatibility

No contract changes, so no compatibility question arises. `ContractVersion` stays
1. No existing client, page or test needs amendment, and the US-008 endpoint
enumeration test gains no row (FR-019).

## 9. Open questions

None from this stage.

Carried from the Specification, neither of which is an API question: OD-010
(`trebovaniya.md` §7 item 14 stays a verify-at-onboarding item; the Story's
dependence on it is resolved, the item itself is not closed) and OD-011 (the
six-value vocabulary, whose journal consequences are recorded in §4 above).
