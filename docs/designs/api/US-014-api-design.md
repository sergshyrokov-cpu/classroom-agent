---
artifact_type: api_design
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T17:00:33Z
updated_at: 2026-09-27T17:00:33Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-014-spec.md
    version: 1
  - path: docs/decisions/US-014-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-014 API Design — Sync courses and rosters

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-014-openapi.yaml`.** Its absence is
this stage's recorded decision, not a missing artifact — the same decision US-007
and US-013 recorded for the same reason. A contract file describing zero operations
would assert a surface this Story does not have. Downstream stages that list
`openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when "the approved Specification
explicitly states the Story does not change public API behavior". The approved
Specification (v1) states exactly that, in four places:

- **FR-018** — "This Story adds **no** endpoint, **no** Razor page, **no**
  authorization policy and **no** route. SC-4's anonymous list is unchanged and the
  US-008 endpoint enumeration test gains no row. Nothing here is reachable from a
  web request at all (NFR-001)."
- **VR-007** — "This Story introduces no HTTP input: no request body, no query or
  route parameter, no uploaded file. Its only external input is the data Google
  returns."
- **§7 S-08** — the Story stores personal data of students and "adds **no** way to
  read it: no endpoint, no page, no export".
- **§10** — every screen showing this data belongs to EPIC-2 and Epic 5, and
  starting a run from the UI "its endpoint, policy and audit row" is US-019.

The cause is the Story's own scope. It adds the **first step inside an existing
background run** (FR-001): no new host, no new service, no new caller. Every
requirement it carries lives below the presentation boundary — a port, three
entities, one migration, an upsert rule, an observation rule, a transaction
boundary and a log line.

**This Story is a stricter NOT_APPLICABLE than US-013's.** US-013 changed which
*states* the existing readiness endpoint reports (its FR-014), a behaviour change
inside an unchanged contract, and §3 of its api-design named that as the one thing
a reviewer should check. US-014 has no equivalent: it does not touch readiness, and
no existing endpoint answers differently because of it.

## 2. What the Story does deliver, and why none of it is a contract

| Specification | Delivered artefact | Why it is not an API surface |
|---|---|---|
| FR-001 | the first pipeline step inside `RunSynchronizationUseCase` | an `Application` use case reached only by the `BackgroundService` US-013 hosts |
| FR-002 | the `IClassroomReader` port and its Google adapter | an outbound port (AD-4). It *calls* an API; it exposes none |
| FR-003, FR-004 | reading courses and rosters, paged | outbound Google reads; VR-005 fixes the paging, and NFR-002's page sizes govern this program's own endpoints, not a Google call |
| FR-005 … FR-007 | `Course`, `ClassroomParticipant`, `CourseMembership` | Domain entities and three tables, read by no endpoint until EPIC-2 |
| FR-008 … FR-011 | upsert, the observation rule, leaving a roster, a course Google stopped returning | persistence behaviour, owned by DB_DESIGN |
| FR-012 | one transaction per course | an `Application` transaction boundary (AD-7) |
| FR-013 | the run counter | a column on an existing table; shown by US-024, not by this Story |
| FR-014 | a failure propagating to US-013's handler | an in-process exception path, never a status code (OD-008) |
| FR-015 | the read-only refusal | the guard US-013 already calls; it has no caller to answer, so it produces no `409` here |
| FR-016 | log lines with counters and internal ids | log events, governed by DC-10 and SC-10 |
| FR-017 | three tables, one migration | schema, owned by DB_DESIGN |
| FR-019, FR-020 | no audit row, no package, no translation key | absences, by definition not a surface |

## 3. Existing contracts this Story must not change

Confirmed against the approved Specification; each is an explicit no-change:

- **Readiness and liveness on the private port** (US-005, DC-11) — untouched.
  Unlike US-013, this Story adds no readiness condition: the sync service's running
  state is already reported, and importing data does not change what "ready" means.
  A reviewer finding a readiness change here should treat it as out of scope.
- **The status-change push receiver** on the private port (US-006) — untouched,
  including the port filter that keeps all three endpoints off the public port
  (SC-9, DC-6).
- **The installation's public port** — the sign-in pages, settings pages and
  Dean-account pages of US-008 … US-012: untouched. No route added, none changed, no
  page gains a field. In particular **no course or roster is exposed anywhere**:
  EPIC-2 builds those screens, and until it does, the imported personal data has no
  read path at all (S-08).
- **The legitimacy-check service channel** and `ClassroomAgent.Contracts` —
  unchanged; `ContractVersion.Current` stays as US-005 set it. This Story sends
  nothing to the Control Plane and receives nothing new from it (S-05: the imported
  data reaches only the installation's own database).
- **The Control Plane's own pages and endpoints** (US-001 … US-006) — untouched.

## 4. What later Stories inherit

Recorded here so the next API design does not re-derive it:

- **EPIC-2 (US-020 … US-022)** is the first consumer. Its contracts read the three
  entities this Story creates and must return DTOs, never entities (AD-8), and must
  paginate: a course list and a roster both grow unbounded, so API-8's default page
  size 20 / maximum 100 applies (NFR-002). Note that this is a *different* paging
  from VR-005's, which is how the program reads from Google.
- **US-019** still inherits what US-013 §4 recorded — the coordinator entry point,
  "Запуск синхронизации" belonging to **both** Admin and Dean (§2, BR-004), the
  audit row for a *manual* start (SC-11), and the `409` with the API-6 body in
  read-only mode. This Story changes none of it.
- **US-024** shows `SyncState`, whose counter now means "courses processed"
  (FR-013, OD-005). A screen that labels it anything else would misreport it.
- **Epic 4** must resolve a Meet participant's address through the roster of the
  course **on the meeting's date** (PC-12, BR-051). OD-011 made the participant's
  email non-unique precisely because that is the matching rule; an Epic 4 contract
  that matches an address globally against the participant table would reintroduce
  the defect OD-011 avoided.

## 5. Acceptance Criterion → operation map

None. No Acceptance Criterion of US-014 (AC-001 … AC-009) is satisfied by an HTTP
operation, and none asserts anything about one. AC-008 is the only criterion that
mentions a boundary at all, and it does so as a prohibition: nothing leaves the
installation, and the imported data gains no read path (Specification §12, S-05,
S-08).

## 6. Auth model

Unchanged. No endpoint is added, so no authorization policy is added; the policies
US-012 registered and the deny-by-default fallback stand as they are (SC-4).

A synchronization run has **no caller and no principal** — it is background work,
which is why §5 describes an audit actor of `system` for such work and why this
Story writes no audit row at all (FR-019). The data is read from Google as the
technical account of `WorkspaceConnection`, never as a person and never through an
Admin's OAuth session (BR-015, BR-031, S-03), so no user identity reaches the
Google call either.

## 7. Error model

Unchanged at the HTTP boundary. Inside the process, Specification §8 is the
authority:

- a read-only refusal and a missing connection remain *skipped* outcomes, not
  errors — there is no caller to answer with `409` (FR-015);
- a Classroom read failure propagates and becomes a failed run recorded in
  `SyncState`; this Story neither classifies nor retries it (FR-014, OD-008), so no
  error taxonomy is introduced that a later contract would have to expose;
- an unrecognised `courseState` skips one course and logs at `Warning` (OD-010) —
  again no status code, because nothing is asking.

No new error code, no new error body, no change to the API-6 shape.

## 8. Compatibility

No contract changes, so nothing to version and nothing for a client to adapt to. No
existing endpoint answers differently after this Story — including readiness, which
US-013 did change and this Story does not.

## 9. Open questions

None raised by this stage. All eleven Open Decisions are resolved (open-decisions
artifact v1): OD-001 … OD-009 before activation, OD-010 and OD-011 at
`HUMAN_SPEC_APPROVAL`. None leaves an API question open, and neither of the two
resolved at the gate has an API dimension — both are schema decisions.

DB_DESIGN owns what this stage deliberately does not: the three tables, the unique
indexes on the Google identifiers, the closed vocabularies for the course state and
the membership role with their check constraints, the non-unique index on the
participant's email (OD-011), the string bounds VR-002 leaves to it with truncation
rather than refusal, and the single migration (FR-017, VR-002 … VR-004).
