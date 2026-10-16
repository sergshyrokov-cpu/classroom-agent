---
artifact_type: specification_review
story: US-053
version: 1
status: APPROVED
created_at: 2026-10-16T15:50:00Z
updated_at: 2026-10-16T15:50:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-053-failed-sync-run-recorded.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/specifications/US-053-spec.md
    version: 1
  - path: docs/specifications/US-053-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-053-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-053 Specification Review

## Summary

**PASS** — 0 Critical, 0 Major, 4 Minor. The Specification is faithful to
`trebovaniya.md` §4 Epic 6 "Состояние синхронизации" (v80) and to the earlier
Specifications whose rules it restores (US-013 FR-006/S-10, US-017 FR-005/
FR-006/FR-007/FR-010, US-031 FR-010/FR-011, US-032 FR-006). Every Story AC is
covered; the one addition (AC-007) is disclosed as derived. The code facts in §1
were checked against the source and hold. The digest is honest; one claim in it
is stated more firmly than the evidence allows (m-3).

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-053-failed-sync-run-recorded.md` | — |
| Specification | `docs/specifications/US-053-spec.md` | 1 (DRAFT) |
| Digest | `docs/specifications/US-053-spec-digest.ru.md` | 1 (DRAFT) |
| Open Decisions | `docs/decisions/US-053-open-decisions.md` | 1 (DRAFT) |
| Requirements | `trebovaniya.md` | 96 |

Inputs are current: the Specification names Story (unversioned) and
`trebovaniya.md` v96; the digest names Specification v1.

## Fidelity

Checked against the cited text, not the citation:

- **§2 Business Goal** — `trebovaniya.md` line 1172, §4 Epic 6 "Состояние
  синхронизации" (v80): status and, on failure, human-readable diagnosis. Quoted
  correctly. The Spec correctly notes the Story's "§6" is §4.
- **FR-001** — US-017 FR-005: "Transient (final), configuration, unexpected — the
  run stops at once"; US-031 FR-010; US-032 FR-006 (linking failure → `failed`,
  `Unexpected`, step Linking). Consistent; OD-001 a keeps it.
- **FR-002/FR-003** — US-013 FR-006: "Each write commits only the `SyncState` row.
  Nothing else may be staged when it commits"; S-10 (US-013 spec line 444).
  Correct source; the rule is general, which supports I-1.
- **FR-004** — US-017 FR-005 (counter = courses committed before the stop),
  FR-006 (closed diagnosis list, `Unexpected`), US-031 FR-010 (step recorded,
  watermark unchanged). Code: `DiagnosisOf` maps anything but a classified
  `GoogleReadFailedException` to `Unexpected` (`RunSynchronizationUseCase.cs:368`),
  so a DB rejection is `Unexpected` today. `ProcessedCount` is incremented only
  after a course's transaction returns (`:538`), so AC-001's counter 1 is right.
- **FR-005** — US-017 FR-010 (unexpected stop: `Error`, code and exception type
  only). Code: the host's `LogRunException` is the escape line
  (`SynchronizationBackgroundService.cs:156-160`); `LogOutcome` is reached only
  when the use case returns. Correct.
- **FR-006** — US-017 FR-007, US-031 FR-011: block read via
  `GetLastSynchronizationQuery` / DTO. Correct.
- **FR-009** — US-013 FR-010: cancellation is a normal stop, not written. Code:
  the `OperationCanceledException` catch precedes the general catch (`:127`).
- **§7 Security** — BR-026 / US-013 FR-006 (protected write path, no growth of
  `PermittedServiceWrites`), SC-10, SC-13. SC-11 lists no "failed run" action;
  "no audit event" is correct.
- **§1 code facts** — verified: one `try` over three steps with
  `FailRun` + `SaveChangesAsync` in `catch (Exception)` (`:96-140`);
  `ExecuteInTransactionAsync` disposes (rolls back) the transaction on failure
  and does not touch the change tracker (`UnitOfWork.cs:106-118`); the only
  detaches are the `MeetingCodeLink`/`AuditEvent` and `AppUser`/`AuditEvent`
  ones (`UnitOfWork.cs:37-102`); one DI scope per run (host `:146`).

No rule without a source; no contradiction.

## Story Coverage

| Story AC | Spec |
|---|---|
| AC-001 | AC-001 (adds "does not throw", which is the Story's in-scope host-line item) |
| AC-002 | AC-002 |
| AC-003 | AC-003 (vacuous-red note carried from Story Notes) |
| AC-004 | AC-004 (level per I-6) |
| AC-005 | AC-005 (adds "no `FailedStep`" — follows US-031 FR-010 "a successful run clears it") |
| AC-006 | AC-006 (adds "account tests" — matches the Story's US-048 mention) |
| — | AC-007 derived, disclosed (I-3) |

Story "In scope" bullets: all four covered (FR-004, FR-003, FR-005, §5/§9).
"Out of scope" bullets: all carried into §10. Nothing beyond scope except AC-007,
which traces to US-013 FR-006 and is disclosed.

## Consistency

- Permission matrix: untouched (block stays Admin-only, US-017 FR-008).
- Read-only (BR-026): unchanged; the discard is not a new service write.
- Localization (NFR-073): no new key; existing `Unexpected`/step texts reused.
- Audit (SC-11): no change.
- US-032 / US-048 detaches kept (FR-008, I-4) — no conflict with their specs.
- OD-001 a consistent with the Story's resolution.

## Open Items

`trebovaniya.md` §7 has no item on how a failed run is recorded; none silently
resolved. OD-001 resolved by the Owner (a) on 2026-10-16, carried unchanged.

## Interpretations

The Specification lists I-1…I-6; the reviewer found no unlisted one. All six are
named in digest §2 (items 1, 2, 4, 6, 7 correspond to I-1, I-3, I-4, I-5, I-6;
I-2 is a mechanism choice inside AD-3, not user-visible, and is covered by digest
lead "всё недописанное отбрасывается").

| Id | Choice | Alternative | In digest |
|---|---|---|---|
| I-1 | discard on any failure in the run's `catch` | discard only on `DbUpdateException` | §2.1 |
| I-2 | via `IUnitOfWork`, keep `SyncState` tracked; do not repair entries saved inside a rolled-back tx | second `DbContext` for the failure save | lead (behaviour only) |
| I-3 | derived AC-007 | leave non-DB failures untested | §2.2 |
| I-4 | keep US-032/US-048 detaches | refactor them | §2.4 |
| I-5 | test chooses how to force rejection, no src seam | — | §2.6 |
| I-6 | AC-004 via the Application query | page-level test | §2.7 |

## Digest Fidelity

Every digest statement traces to the Specification (FR/AC/I ids given inline).
No Open Decision is hidden (none open; OD-001 stated as the Owner's). Plain
Russian, judgeable by a non-developer. See m-3 for one claim stated as fact.

## Findings

### m-1 (Minor) — AC-007 has no named trigger that avoids a production seam

Spec AC-007, I-5, §7 "No test seam". I-5 says how a test may force a *database*
rejection; AC-007 needs a *non-database* exception thrown inside a course's
transaction after changes were staged. All Google reads happen before the
transaction (`RunSynchronizationUseCase.cs:494-507`), so a substituted Google
port cannot throw there. What remains is something inside `ImportCourseAsync`
— e.g. a Domain guard refusing a synthetic value, or the erasure fence. TEST_WRITING
must find such a trigger in synthetic data; if none exists without a seam in `src/`,
the test report says AC-007 is unreachable today rather than adding one (§7 wins
over AC-007).

### m-2 (Minor) — FR-005 (host log line) is not asserted by any AC

Traceability puts FR-005 under AC-001, but AC-001 asserts only that the use case
returns a failed outcome. The host's choice between `LogOutcome` and
`LogRunException` follows from that by existing code, and US-017 tests cover
`LogOutcome`'s failure line. Acceptable; a host-level test of the "escaped" line
not being written is optional. Recorded so IMPLEMENTATION_VERIFICATION does not
read FR-005 as directly tested.

### m-3 (Minor) — Digest states the partial-write risk as established

Digest lead and §2.2 ("сегодня может записать половину курса уже вне общей
транзакции"; "может навсегда застрять") — the Story says the diagnosis comes
from code reading, "not yet proven by a test", and the row stays `running` until
the next run (forever only if the cause repeats). The code supports the claim
(the failure save commits the whole context; nothing detaches the staged
entries), so it is not wrong, but the red phase of AC-001/AC-007 is where it is
proven. The Owner should read it as "by code reading, to be confirmed by the
first tests".

### m-4 (Minor) — I-2 relies on the run ending right after the failure save

I-2: entries saved inside a rolled-back transaction remain tracked as
`Unchanged` though they are not in the database; leaving them is safe only
because the run's scope ends after the failure save (verified, one scope per
run). If a later Story lets the run continue after a failure (OD-001 b, deferred
to after the pilot), this assumption breaks and those entries must be discarded
too. Worth one comment at the discard in the implementation.

## Verdict

**PASS.** No Critical or Major finding. m-1…m-4 are advisory and go to the
Owner at `HUMAN_SPEC_APPROVAL`.

Delegation: none — all sources and code were read by the reviewer.
