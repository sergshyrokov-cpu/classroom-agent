---
artifact_type: specification
story: US-017
version: 1
status: APPROVED
created_at: 2026-10-03T14:51:28Z
updated_at: 2026-10-03T14:58:23Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-017-retry-backoff-permission-errors.md
    version: null
  - path: trebovaniya.md
    version: 80
  - path: docs/decisions/US-017-open-decisions.md
    version: 1
supersedes: null
---

# US-017 Specification — Retry, backoff and permission-error handling

## 1. Overview

US-013 built the synchronization run; US-014 and US-015 gave it its reads. Every
one of those Stories deferred the same thing here in writing: telling a passing
Google failure from a configuration one, retrying the first, stopping on the
second, and telling the Admin.

Today (verified in code):

- any exception from a Google port fails the whole run
  (`RunSynchronizationUseCase`, the `catch (Exception)` block), and
  `SyncState.LastError` holds `"RunFailed:" + <exception type name>`;
- the Google client library's retries are switched off
  (`ExponentialBackOffPolicy.None` in `GoogleClassroomReader`), with comments
  citing this Story;
- `GoogleClassroomReader` catches nothing; only `GoogleAccessProbe` (US-011)
  classifies Google answers, into the closed list `AccessCheckStepOutcome`;
- every failed run is logged at `Error` (`SyncRunFailed`), whatever the cause;
- `SyncState` is shown on no screen.

`trebovaniya.md` v80 fixes the policy (Epic 1), the Admin's view (Epic 6) and
the permission (§2 matrix). This Specification turns them into requirements.

## 2. Business Goal

- A `429`, `5xx` or network hiccup no longer costs a whole run (§4 Epic 1).
- A school whose delegation is incomplete stops calling Google at once each run
  instead of failing at an arbitrary point, and does not waste the quota every
  school shares (§6).
- The Admin learns from the program, in their language, what is wrong and who
  must act — the school's super-admin or the Owner (§4 Epic 6, BR-034).
- Once the school fixes delegation, synchronization recovers with no action by
  the Admin (OD-004).

## 3. Business Flow

### 3.1 A short hiccup

A read answers `429` (or `503`, or the connection drops). The adapter waits about
2 s and repeats it; the second attempt succeeds. The run goes on and completes;
the log has one `Warning` for the retried attempt.

### 3.2 Google stays unavailable

Every attempt of one request fails transiently. After the fourth attempt the run
stops as `failed` with the diagnosis "Google unavailable". Courses committed
before it remain. The next run starts after the normal interval.

### 3.3 Delegation is not configured

The first token request answers `unauthorized_client`. No retry. The run stops at
once as `failed` with the diagnosis "scope not authorised"; one `Error` line is
written. The Admin opens the connection page and reads what to tell the
super-admin. The super-admin fixes delegation; the next scheduled run completes;
the page now shows a successful run.

### 3.4 A course disappears mid-run

The course listing returned course X; by the time its roster is read, it has been
deleted in Google and answers `404`. Course X is skipped with a `Warning`; the run
goes on and completes.

### 3.5 Something unforeseen

A read fails in a way none of the classes covers. The run stops as `failed` with
the diagnosis "unexpected error"; neither `SyncState` nor the log holds the
exception's message.

### 3.6 The Admin looks

The connection page shows a "Last synchronization" block: when the last run
started or finished, its status, when the last successful run finished, and,
after a failure, the diagnosis. Before any run it says no run has happened yet.

## 4. Functional Requirements

### FR-001 Failure classes

Every failure of a Google request made by a synchronization run — a token
request or an API request — falls into exactly one class:

| Class | What counts | Retried |
|---|---|---|
| **Transient** | HTTP `429`; any `5xx`; a timeout; a dropped or refused connection | yes (FR-002) |
| **Configuration** | the outcomes `ScopeNotAuthorized`, `TechnicalAccountUnknown`, `TechnicalAccountCannotRead`, `ApiNotEnabled`, `KeyUnavailable`, `KeyRejected` as `GoogleAccessProbe` classifies them today | no |
| **Course gone** | HTTP `404` on a request scoped to one course (roster, coursework, materials, submissions) | no |
| **Unexpected** | anything else, including a `404` on the course listing and an answer the adapter cannot parse | no |

The configuration rules are those of `GoogleAccessProbe.ClassifyTokenError` and
`ClassifyApiError` (US-011 spec FR-005), shared rather than copied, so one
Google answer gets one name in both places (OD-006). The one deliberate
difference: the probe calls an unclassifiable answer `GoogleUnavailable`;
synchronization calls it **unexpected** (OD-008).

### FR-002 The retry policy

For a transient failure, the **same request** (one page of one listing, or one
token request) is attempted at most **4 times in total**. The pause before the
2nd, 3rd and 4th attempt is nominally **2 s, 8 s and 30 s**, each multiplied by a
random factor in **[0.8, 1.2]** (I-2). If the failed answer carries
`Retry-After`, that delay replaces the nominal pause for that attempt, **capped
at 2 minutes** (§4 Epic 1 v80, I-3). After the 4th failed attempt the failure is
final and transient.

Paging restarts nothing: a retried request is the one that failed, with the same
page token; pages already read are kept.

### FR-003 Where the retry lives

The retry happens inside the Google adapter in `Infrastructure`; no Google type,
status code or header crosses into `Application` (AD-4, AD-5). The client
library's own retry stays switched off, so the policy of FR-002 is the only one.
Pauses are measured with the injected `TimeProvider` and end early, with
cancellation, when the run's `CancellationToken` is cancelled (shutdown). The
random factor comes from an injectable source so tests can fix it.

### FR-004 What the adapter reports upward

A final failure leaves the adapter as one application-level failure carrying its
class and, for configuration, which outcome — declared in `Application`
(`Ports` or `Models`), with no Google detail in it (no message, reason string,
status text or response body). The design chooses the exact type.

### FR-005 What the run does with a failure

- **Course gone** — the course is skipped: nothing of it is written in this run
  (its reads happen before its transaction), the course already in the database
  is left as it was, and the run goes on. Counted as a skipped course.
- **Transient (final)**, **configuration**, **unexpected** — the run stops at
  once. Courses committed before the failure stay (US-015 FR-013, one
  transaction per course). `SyncState` is set `failed` with the diagnosis code
  (FR-006) and the number of courses committed before the stop (I-5).
- The next run is scheduled after the normal interval in every case (OD-004,
  US-013 OD-003 unchanged).

### FR-006 The diagnosis stored in `SyncState`

`SyncState.LastError` holds a **diagnosis code** from this closed list — never
an exception type name, never text from Google:

`ScopeNotAuthorized`, `TechnicalAccountUnknown`, `TechnicalAccountCannotRead`,
`ApiNotEnabled`, `KeyUnavailable`, `KeyRejected`, `GoogleUnavailable` (a final
transient failure), `Unexpected`.

A successful run clears it (as today). A value already stored by an earlier
version (`RunFailed:…`) is displayed as `Unexpected` (I-6). Whether the column
stays as it is or becomes constrained to the list is the DB design's call.

### FR-007 The "Last synchronization" block

The Admin's connection page (`WorkspaceConnection`, US-009) gains a block
showing, from `SyncState`:

- status: never run / running / completed / failed;
- for running: when it started; otherwise when the last run finished;
- when the last successful run finished, or that there has been none;
- for failed: the diagnosis text — what happened and who acts (the school's
  super-admin, or the Owner for `ApiNotEnabled`, `KeyUnavailable`,
  `KeyRejected`), and for `GoogleUnavailable` / `Unexpected` that the next run
  will retry by itself.

Times are shown in the request's UI culture, as the landing page shows the last
legitimacy check (US-039 FR-009). For the six configuration outcomes the text is
the same text "Check access" shows for that outcome (OD-006): the existing
`AccessCheck.Outcome.<Outcome>` translations are reused, not duplicated. New
keys are added only for what "Check access" has no word for (the block's
labels, the statuses, `Unexpected`, and the synchronization reading of
`GoogleUnavailable`, I-7), in both `uk` and `en` (NFR-073).

The block is read from `SyncState` through an `Application` query returning a
DTO (AD-8); no `DbContext` in `Web` (AD-3).

### FR-008 Who sees it

The block is on the connection page only, which already requires the
`ConfigureWorkspaceConnection` policy (Admin). A Dean is refused the page as
today (§2 matrix, v80 row). No new endpoint.

### FR-009 Read-only mode

Unchanged: in read-only mode no run starts (US-007, US-013), so nothing is
retried. The connection page and its new block remain viewable; the block
changes nothing.

### FR-010 Logging

Per §8, with the run id on every synchronization line and only internal
identifiers:

| Event | Level |
|---|---|
| a transient failure that will be retried (attempt number, pause) | `Warning` |
| a course skipped because it is gone (course Google id) | `Warning` |
| a course skipped because its name is blank (course Google id) | `Warning` |
| a run stopped by a final transient failure (`GoogleUnavailable`) | `Warning` |
| a run stopped by a configuration failure (diagnosis code) | `Error` |
| a run stopped by an unexpected failure (diagnosis code and exception **type** name only) | `Error` |

The existing `SyncRunFailed` (always `Error`) is split or re-levelled to match;
the design chooses event ids. No log line holds a Google message, reason string,
response body, URL with a query, or header value.

### FR-011 Bounded Google-supplied values in logs (US-014 F-1, US-015 F-1)

Every value that comes from Google and is written to a log line — course and
submission ids, a raw course state, a raw submission state — is truncated to
**64 characters** before logging (I-4). This covers the existing
`SyncCourseSkipped` and `SyncSubmissionStateUnrecognised` lines and every line
this Story adds.

### FR-012 A blank course name (US-014 I-5)

A course whose name from Google is empty or whitespace is **skipped** before its
reads and its transaction, with the `Warning` of FR-010, and counted as skipped;
the run goes on. A course already in the database keeps its stored name.
`Course`'s own guard (`ArgumentException` on a blank name) stays as a domain
invariant.

### FR-013 What does not change

"Check access" and the startup self-check stay single-shot (US-011 FR-005). The
schedule, the read-only guard, the per-course transaction, the age rule and the
retention purge are untouched. No new endpoint, no package, no audit event (no
user action is added; SC-11).

## 5. Acceptance Criteria

| Id | Criterion | Source |
|---|---|---|
| AC-001 | A transient failure retried successfully: run completes, data as without the failure, one `Warning` per retried attempt | Story AC-001 |
| AC-002 | Retries bounded: exactly 4 attempts, pauses ~2/8/30 s within ±20 %, `Retry-After` honoured up to 2 min; run `failed` with `GoogleUnavailable`; committed courses remain; next run after the normal interval | Story AC-002 |
| AC-003 | Configuration failure: 1 attempt, run stops at once, diagnosis code stored, one `Error`, next run after the normal interval | Story AC-003 |
| AC-004 | `404` on a course's read: course skipped with `Warning`, others imported, run completes | Story AC-004 |
| AC-005 | Unexpected failure: run `failed` with `Unexpected`; no Google message or exception message in `SyncState` or logs | Story AC-005 |
| AC-006 | Admin sees the block: statuses, times in UI culture, diagnosis worded as "Check access", "never run" state, both languages | Story AC-006 |
| AC-007 | Dean refused the connection page | Story AC-007 |
| AC-008 | Read-only: no run, no retry; page and block viewable | Story AC-008 |
| AC-009 | Google-supplied values in log lines truncated to 64 characters | Story AC-009 |
| AC-010 | Blank course name skipped with `Warning`, run completes | Story AC-010 |
| AC-011 | No test reaches Google; pauses driven by the injected `TimeProvider`, no real sleeping | Story AC-011 |
| AC-012 | Shutdown during a retry pause ends the run promptly; nothing further is requested | derived, FR-003 |
| AC-013 | A legacy `RunFailed:…` value is displayed as "unexpected error" | derived, FR-006 |
| AC-014 | The retry and classification are proven through the real adapter over a scripted HTTP handler, not only through the in-memory reader | derived, FR-003 (lesson from US-015) |

## 6. Validation Rules

### VR-001 Google answers are external input

Status codes, `Retry-After` and error bodies are read only to classify; none is
stored or logged. `Retry-After` is honoured in both forms (seconds, HTTP date);
a value that is negative, unparseable or in the past means "use the nominal
pause"; a value above 2 minutes means 2 minutes.

### VR-002 The diagnosis code

Exactly one of the eight codes of FR-006. Anything else read from `SyncState`
displays as `Unexpected`. Length is far below the existing 512 limit.

### VR-003 Blank course name

`string.IsNullOrWhiteSpace(name)` — the same test the domain guard applies.

### VR-004 Log truncation

At most 64 UTF-16 code units; no ellipsis is required. Applies after the value
leaves Google, before it enters a log template.

## 7. Security Requirements

- **Google access stays read-only** (Hard Stop); a retry repeats a read, never
  anything else.
- **No raw Google text** in `SyncState`, logs or the page (SC-10, §5, §8).
- **Authorization:** the block lives on a page already protected by
  `ConfigureWorkspaceConnection` (Admin); Dean forbidden (§2 v80; SC-4 deny by
  default). Allowed-role and forbidden-role tests (TC-5).
- **No new personal data** anywhere: the block shows times, a status and a
  diagnosis; log lines carry run and Google ids only.
- **Quota:** retries are bounded (FR-002) and configuration failures are never
  retried, protecting the quota all schools share (§6).
- **Read-only** enforcement unchanged and still in `Application` (AD-6).
- No new outbound destination (SC-13): retries go to the same Google endpoints.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| transient, recovered | run continues |
| transient, final | run `failed`, `GoogleUnavailable`, `Warning` |
| configuration | run `failed`, outcome code, `Error` |
| course gone | course skipped, `Warning`, run continues |
| blank course name | course skipped, `Warning`, run continues |
| unexpected | run `failed`, `Unexpected`, `Error` with exception type name only |
| shutdown during a pause | `OperationCanceledException` path as today; no `failed` diagnosis invented |
| `SyncState` holds an unknown value | displayed as `Unexpected` |

## 9. Non-Functional Requirements

- Worst case for one request: 3 pauses of at most 2 minutes each.
- Tests run with a manual `TimeProvider`; no test sleeps (TC-4,
  [lesson: signal, don't poll]).
- UI strings only from translation files, `uk` and `en` (NFR-073).
- `TreatWarningsAsErrors`; no new package.

## 10. Out of Scope

The manual "Synchronize" button (US-019); the database statistics screen and
anything a Dean sees about synchronization (US-024); incremental sync (US-018);
Meet event reads (their Story applies this policy); retries inside "Check
access" or the self-check; a different interval after a failure; configurable
retry parameters.

## 11. Interpretations (for the gate)

- **I-1** "One call" in v80 is one HTTP request — one page or one token request —
  not a whole paged listing.
- **I-2** "Случайный разброс" is fixed as ±20 % of the nominal pause.
- **I-3** A `Retry-After` above 2 minutes is waited as 2 minutes, then the
  attempt is made (not treated as an immediate final failure).
- **I-4** The log bound is 64 characters — longer than any Google id or state
  seen, short enough to stop log growth.
- **I-5** A failed run records the number of courses committed before it
  stopped, not 0 as today; this is what the counter means (§3 `SyncState`).
- **I-6** Old `RunFailed:…` values are not migrated; they display as
  `Unexpected` and disappear with the next successful run.
- **I-7** `GoogleUnavailable` gets a synchronization-specific text ("Google did
  not answer after several attempts; the next run will try again"), because the
  "Check access" text describes a single check. The six configuration outcomes
  reuse "Check access" texts unchanged.
- **I-8** A run skipped because the connection is unusable or read-only mode is
  on does not touch `SyncState` (as today); the block keeps showing the last
  real run.

## 12. Open Decisions

OD-001 … OD-009 were resolved by the Owner on 2026-10-03 before activation and
are carried unchanged into `docs/decisions/US-017-open-decisions.md`. Writing
this document raised no new Open Decision; the choices it had to make are listed
as interpretations I-1 … I-8 for the gate to confirm.

## 13. Traceability

| AC | FR / VR |
|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-010 |
| AC-002 | FR-002, FR-005, FR-006, VR-001 |
| AC-003 | FR-001, FR-004, FR-005, FR-006, FR-010 |
| AC-004 | FR-001, FR-005, FR-010 |
| AC-005 | FR-001, FR-006, FR-010 |
| AC-006 | FR-007, VR-002 |
| AC-007 | FR-008 |
| AC-008 | FR-009 |
| AC-009 | FR-011, VR-004 |
| AC-010 | FR-012, VR-003 |
| AC-011 | FR-003, §9 |
| AC-012 | FR-003, §8 |
| AC-013 | FR-006, VR-002, I-6 |
| AC-014 | FR-003, FR-004 |
