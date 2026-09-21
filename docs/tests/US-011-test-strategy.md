---
artifact_type: test_strategy
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-21T08:40:10Z
updated_at: 2026-09-21T08:40:10Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-011-check-access.md
    version: null
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 2
  - path: docs/designs/api/US-011-api-design.md
    version: 1
  - path: docs/designs/api/US-011-openapi.yaml
    version: 1
  - path: docs/designs/database/US-011-db-design.md
    version: 1
  - path: docs/designs/database/US-011-entity-model.md
    version: 1
supersedes: null
---

# US-011 Test Strategy — Check access diagnostic

## 1. Scope

Everything the Story adds: the page and the run of US-011 openapi, the eight steps and their outcomes, the two
refusals, the audit rows, the startup self-check, the translation keys, the audit-table migration, and the Google
implementation of the port. Nothing reaches Google or the Control Plane: the port is substituted in the host
(`FakeGoogleAccessProbe`), and the Google implementation is driven through a scripted `HttpMessageHandler` with a
service-account key generated at run time (TC-4).

## 2. Test levels

| Level | Where | What it proves |
|---|---|---|
| Integration over HTTP (real host, real PostgreSQL via Testcontainers, fake port) | `Web/*` | the contract of openapi: status codes, the rendered result, the evaluation order, audit rows, refusals, authorization, translation |
| Integration of the background self-check (real host, fake port, log files) | `Web/BackgroundServices` | FR-010: runs once at start, logs Information/Error, skips with a Warning, calls nothing in read-only mode |
| Schema (real PostgreSQL) | `Web/Persistence`, `Infrastructure/Persistence` | db-design §3.1, §7.1: the two new codes accepted, unknown codes rejected, one more migration, no table and no column added |
| Adapter (no host, scripted transport) | `Infrastructure/Google` | FR-002, FR-003, FR-005, FR-016: one scope per token request, the impersonated subject, minimal GET reads, the mapping of Google's answers onto the closed list, the key's absence stopping everything before a request |
| Structural (existing, unchanged) | `Architecture/*`, `Application/UseCases/GoogleDataPortRuleTests` | the US-007 rule: a use case holding an `IGoogleDataPort` takes the guard. It scans the real Application assembly, so the new use case is covered without a new test |

Read-only enforcement is proven through the substituted port recording **zero** calls — including the token
request — for all three BR-025 causes, both for the run and for the self-check (TC-5, carried US-007 F-5).

## 3. Fixtures

- `AccessCheckHostExtensions.StartSignedInAsync` — a host in a chosen legitimacy state, the fake port registered,
  an Admin signed in, and the connection seeded **after** the start (`SeededConnection.None | Usable |
  ForAnotherDomain`).
- `AccessCheckHostExtensions.StartWithSelfCheckAsync` — a host seeded **before** the start and scripted before it,
  so the self-check sees the state under test.
- `FakeGoogleAccessProbe` — scripted outcome per scope and per read, `NeverAnswers`, `Throws`; records each call
  with whether it happened inside an HTTP request. The self-check runs in the background, so HTTP tests count
  `RequestCalls` and self-check tests count `BackgroundCalls` — no race between the two.
- `SyntheticServiceAccountKey` — a fresh RSA key in Google's JSON key format per test; never written to disk,
  belongs to no project. `DictionarySecretStore` — a secret store that never touches the process environment.
- `AccessCheckTestData` — the names this stage fixes (§5).

## 4. Scenarios

**Positive.** Access in place → 200, verdict `AccessInPlace`, eight rendered steps, six scopes in order, the reads
after the delegations, the stored technical account impersonated, the issued token used by the reads; the
self-check logs `Information`; an English account gets English text with the same data.

**Negative.** A forgotten scope is named with its full URI; a read waits for its own scope (`NotAttempted`);
run-wide causes (`KeyUnavailable`, `KeyRejected`, `TechnicalAccountUnknown`) stop after one call; read causes
(`TechnicalAccountCannotRead`, `ApiNotEnabled`) sit on their read; `GoogleUnavailable` alone makes `Inconclusive`, a
configuration failure outranks it; nothing is retried; Dean refused by the policy; anonymous GET and POST sent to
sign in with nothing run; POST without antiforgery → 400 with nothing run; posted account or scope ignored.

**Boundary.** The 30-second limit (I-2): Google never answering, the clock advanced past 30 s → the run ends as
`Inconclusive` with no step `Succeeded`. An empty Google answer is success; a read returning data is success.

**Validation.** VR-001/VR-003: extra form fields ignored. VR-002: the exact scope list. VR-004: missing, blank or
unresolvable reference and content that is not a service-account key → `KeyUnavailable` with **no request sent**.
VR-006: rendered values found as data in the page.

**Security.** Allowed/forbidden role (TC-5); anonymous list and antiforgery exemptions unchanged; zero Google calls
in read-only mode for the run and the self-check; no token in the page or cookies; no key reference, setting name or
`private_key` in the page for an Owner-side cause; no technical account, domain, Admin email, scope, token or finding
in any audit row; no technical account, domain or token in any log line; no Workspace admin-role name in any
message (US-010 OD-001); the adapter reaches only the three Google hosts and sends no write method except the token
POST.

**Persistence.** One `succeeded` row per carried-out run whatever it found (I-4), with actor, target and request id;
`refused/read_only_mode` with the connection id; `refused/connection_not_usable` with and without an id; the guard
first (read-only and unconfigured → `read_only_mode`); a run writes nothing but its audit row; the self-check
writes nothing; the schema accepts the two codes and rejects unknown ones; four migrations, five tables, no new
column.

## 5. Names fixed by this stage

Honoured by IMPLEMENTATION, or corrected deliberately together with the tests:

- the setting `Google:ServiceAccountKeyReference` (spec FR-016 left the name open);
- the markup: `id="access-check-verdict"` with `data-verdict`, and one element per step with class
  `access-check-step` and `data-kind`, `data-scope`, `data-outcome` (values are the openapi enum names); the run
  form posts to `/settings/access-check` with an `action` attribute;
- the log events `AccessSelfCheckCompleted` (property `Verdict`) and `AccessSelfCheckSkipped` (property `Reason`);
- the 21 translation keys in `AccessCheckTestData.TextKeys.All`, including `Landing.Settings.AccessCheck`;
- the skeleton types of OD-006 and the adapter's constructor `(ISecretStore, GoogleServiceAccountSettings,
  HttpMessageHandler)`.

The path, the policy `RunAccessCheck`, the audit codes and the outcome and verdict names are the approved designs'.

## 6. Excluded scenarios

- **A live Google call** — forbidden (TC-4). The adapter's wire shapes are Google's documented formats written
  synthetically; whether the real Google answers exactly so is verified at deployment, with the check itself.
- **"The run does not end before 30 s"** — observing that something has *not* happened yet needs a real-time wait,
  which the conventions forbid; the upper bound is tested, the lower bound is not.
- **"The self-check runs only once per start"** — the same problem: absence of a later run cannot be observed
  without waiting. Covered structurally only by the single `Completed` event per start.
- **A real Dean over HTTP** — no Dean can sign in until US-012 (carried US-010 F-1); the policy object is used.

## 7. Known limitations

- The adapter tests prove the mapping on synthetic answers. If Google phrases an error differently in production,
  the mapping's fallback is `GoogleUnavailable` (spec FR-005), which the tests prove — a wrong diagnosis is ruled out,
  an unhelpful one is not.
- Three new cases pass before implementation (§ report): an unknown action is already rejected, no table and no
  column exist — they are regression guards for the migration, not evidence of new behaviour.

## 8. Open Decisions affecting testing

OD-006 (raised here, resolved by the Owner on 2026-09-21, option 1): the compile-only skeleton. No other decision
is open.
