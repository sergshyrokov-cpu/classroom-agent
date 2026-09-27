---
artifact_type: open_decisions
story: US-013
version: 1
status: DRAFT
created_at: 2026-09-27T08:41:20Z
updated_at: 2026-09-27T08:41:20Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-013-background-sync-service.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-013 Open Decisions

Story-level Open Decisions for US-013 (Background synchronization service). Every
item is resolved only by a human; the resolution is written next to the item and
nothing is deleted. All seven were raised in the Story and resolved by the Owner
before activation; they are carried here with their `OD-` ids and resolutions
unchanged.

| Id | Raised by | Status |
|---|---|---|
| OD-001 Does this Story import anything, or only run the pipeline | the Story | RESOLVED 2026-09-27 (option 1) |
| OD-002 How often a scheduled run starts, and where the interval lives | the Story | RESOLVED 2026-09-27 (option 1) |
| OD-003 What the schedule does after a failed run | the Story | RESOLVED 2026-09-27 (option 1) |
| OD-004 One `SyncState` row, or a history of runs | the Story | RESOLVED 2026-09-27 (option 1) |
| OD-005 What the service records when it cannot run | the Story | RESOLVED 2026-09-27 (option 1) |
| OD-006 Whether the first run happens at startup | the Story | RESOLVED 2026-09-27 (option 1) |
| OD-007 Is the trigger seam built now or in US-019 | the Story | RESOLVED 2026-09-27 (option 1) |

## OD-001 Does this Story import anything, or only run the pipeline?

The catalog plans US-013 as the service and US-014 / US-015 as the data. A service
with no steps completes a run with zero counters, which is testable but imports
nothing a Dean can see.

Options: (1) host only — the service, the schedule, the run lifecycle,
`SyncState`, the read-only refusal, the logging and the readiness contribution,
with an empty pipeline; (2) merge US-014 into US-013 so the first run imports
courses and rosters, retiring the US-014 catalog entry.

**Resolution:** option 1, decided by the Owner on 2026-09-27. This Story delivers
the host; US-014 adds the first pipeline step. A run completing with **zero
counters is the expected outcome** until then, and neither the Specification nor a
test may treat it as a defect.

**Impact on the Specification:** FR-001, FR-004, FR-005, §10, I-1.

## OD-002 How often a scheduled run starts, and where the interval lives

`trebovaniya.md` §4 Epic 1 says only "периодически (или по запросу)"; no document
fixes a number. §6 notes the schools share one Cloud project's quota and that a
periodic (rather than on-demand) synchronization should not make that a
bottleneck.

Options: (1) an optional installation setting with a default, so the Owner can
tune one school without a rebuild (DC-3 grows by one optional key, like the
default UI language); (2) a constant in the code, as the legitimacy check's 6
hours and 15 minutes are — but those come from BR-024, which fixes them, and
nothing fixes this one; (3) a required setting, whose absence stops the
installation from starting, as the retention period and the time zone do.

**Resolution:** option 1, decided by the Owner on 2026-09-27. DC-3 gains one
optional installation setting; the Specification fixes its name and its default,
and an installation without it starts and synchronizes on that default.

**Impact on the Specification:** FR-003, FR-013, VR-001, §9.

## OD-003 What the schedule does after a failed run

Options: (1) the same interval as after a success — the failures this Story can
see are not the ones worth hurrying, since retrying the call that actually failed
is US-017; (2) a shorter interval after a failure, as the legitimacy check uses 15
minutes against 6 hours; (3) exponential backoff between runs.

**Resolution:** option 1, decided by the Owner on 2026-09-27. One interval, used
after a success and after a failure alike.

**Impact on the Specification:** FR-003, FR-008.

## OD-004 One `SyncState` row, or a history of runs

`trebovaniya.md` §3 describes `SyncState` in the singular — status, counter, last
error, last synchronization time.

Options: (1) one row, updated in place; (2) one row per run, with the current
state read as the latest — a history nobody has asked for, needing a retention
rule of its own that PC-11 does not provide.

**Resolution:** option 1, decided by the Owner on 2026-09-27. One row, updated in
place, as `trebovaniya.md` §3 describes it.

**Impact on the Specification:** FR-005, FR-006, VR-002, I-2, I-3.

## OD-005 What the service records when it cannot run

A run skipped because of read-only mode or a missing `WorkspaceConnection` is
worth knowing about — the Owner reading the log must not mistake silence for
success (the reason US-011 OD-004 gave for its own self-check).

Options: (1) a log line only; (2) nothing at all.

**Resolution:** option 1, decided by the Owner on 2026-09-27. A log line and
nothing else — no `SyncState` write, and BR-026's closed list is not touched.
Option 2 was not available in the form of a third choice: writing the skip into
`SyncState` would be a synchronization write in read-only mode, which BR-026
forbids and which only a new version of `trebovaniya.md` §2 could permit. Which
level the line uses is fixed by the Specification within DC-10 (I-4).

**Impact on the Specification:** FR-007, FR-009, FR-012, S-04.

## OD-006 Whether the first run happens at startup

The legitimacy-check service checks immediately at start. A synchronization run at
start would find the installation read-only, because an installation that has
never completed a successful check **is** read-only (BR-025) — so an unconditional
run at startup would always be skipped on a fresh installation.

Options: (1) wait for the first legitimacy determination after start, then run;
(2) run at startup unconditionally and let the read-only guard skip it, then
follow the interval; (3) do not run at startup — wait one interval.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The first run
happens as soon as the installation knows it is legitimate, which is seconds after
start, so a fresh installation synchronizes without waiting a whole interval.

**Impact on the Specification:** FR-003, FR-011, I-5.

## OD-007 Is the trigger seam built now or in US-019?

AD-5 says a request that starts a synchronization "enqueues work and returns
immediately". The endpoint, its policy and its audit row are US-019.

Options: (1) build the seam now — a coordinator that guarantees one run at a time
and accepts an out-of-schedule request, as `PushCheckCoordinator` does for US-006;
(2) leave it to US-019 and guarantee one-at-a-time against the schedule alone.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The coordinator is
built here and guarantees one run at a time; US-019 adds only the endpoint, its
policy and its audit row. AC-002 needs the one-at-a-time guarantee in any case.

**Impact on the Specification:** FR-004, FR-010, §10.

## `trebovaniya.md` §7 — the open items and this Story

- **Item 10 — the minimum Workspace roles of the technical account** (Проверить
  при внедрении). Untouched: this Story makes no Google call of its own. US-011
  remains the tool item 10 will be verified with.
- **Item 14 — submissions of a removed student** (Проверить при внедрении). A
  Classroom data question owned by the Stories that import submissions (US-015,
  US-018). This Story imports nothing (OD-001), so it neither touches nor is
  blocked by item 14.
- **Item 26 — documentation** (Решить). Untouched.

No item of §7 blocks this Story.

The rest of what this Story implements is **closed** in the requirements: §5
(synchronization must not block the UI — `IHostedService` / `BackgroundService`),
§4 Epic 1 (the periodic run, and `SyncState` holding the time of the last
synchronization, the status and the errors), §3 (`SyncState` itself), §2 (the
read-only rules and the closed list of service writes), §8 (readiness reporting
that the synchronization background service is not running, and the run identifier
in every log line of a run), and BR-025, BR-026, BR-040, BR-043, BR-044, NFR-001.

No new Open Decision was raised by SPECIFICATION. Where the requirements leave a
detail to the implementer rather than to the Owner, it is written as an
**interpretation** in §11 of the Specification, so it can be corrected at
`HUMAN_SPEC_APPROVAL` instead of being discovered in code.
