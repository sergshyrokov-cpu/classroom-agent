---
artifact_type: open_decisions
story: US-047
version: 2
status: APPROVED
created_at: 2026-10-14T12:19:55Z
updated_at: 2026-10-14T12:43:32Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-047-windows-service.md
    version: null
  - path: trebovaniya.md
    version: 92
supersedes: null
---

# US-047 Open Decisions — Run the Control Plane and the installation as Windows services

Two, both carried from the Story and both resolved by the Owner on 2026-10-14.
No new Open Decision was raised while writing the Specification.
`trebovaniya.md` §7 (v92) has no open item this Story depends on.

## OD-001 How AC-001 … AC-003 are verified

An automated test cannot register a real Windows service: that needs
administrator rights and a real Service Control Manager, which the test suite
does not have, and the Definition of Done asks every AC to map to a passing test.

- a) **(Recommended)** Automated tests cover what can run without the Service
  Control Manager — the host is wired for the Windows service lifetime, AC-004,
  AC-005, AC-006, AC-007 and AC-008. AC-001 … AC-003 are checked once by hand on
  a Windows machine (install, start, stop, Windows shutdown), and the result is
  recorded in the `implementation_report` as a manual verification with its
  steps.
- b) An automated test that installs a real service, skipped when not run as
  administrator. Rejected in spirit: the Definition of Done forbids skipped
  tests.

**Resolution (Owner, 2026-10-14): a)** — automated tests for everything that runs
without the Service Control Manager; AC-001 … AC-003 verified once by hand and
recorded in the `implementation_report` with their steps.

Impact: spec FR-008, §6 test approach, traceability rows AC-001 … AC-003.

## OD-002 Where a service's environment variables are set

Secrets reach the application as environment variables (`EnvironmentSecretStore`,
US-008 OD-004): the service-account key and the OAuth client secret. On the pilot
server the Owner also works daily under a standard Windows account, which must
not be able to read them (`trebovaniya.md` §9, SC-7).

- a) **(Recommended)** Each service runs under its own dedicated local Windows
  account, and the secrets are that account's own environment variables —
  readable by that account and administrators only. To be confirmed on the
  pilot server during this Story: that a service started under that account
  sees them; if it does not, fall back to b).
- b) The service's own `Environment` value in the registry
  (`HKLM\SYSTEM\CurrentControlSet\Services\<name>`), with that key's access
  narrowed to administrators, SYSTEM and the service account.
- c) Machine-wide environment variables. Rejected: every user of the server can
  read them.

**Resolution (Owner, 2026-10-14): a)** — a dedicated local account per service,
secrets as that account's own environment variables; confirmed on the pilot
server during this Story, falling back to b) if the service does not see them.

Impact: operational only — no code. Spec FR-009 (the manual verification of
AC-001 records whether the service saw the account's variables, and which
option was used).

---

## Raised by TEST_WRITING

### OD-003 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. Spec I-1 needs a point where a test supplies the
service-detection result, and it must not be a setting, an environment variable
or an argument (VR-002). The tests call
`Program.BuildApplication(string[] args, bool runsAsService)` on both hosts
(`ClassroomAgent.Web.Program` and the Control Plane's `Program`): it builds the
host without running it; the entry point then calls it with the real detection
result and runs the returned application. The method does not exist yet, and
the test-writer may not change production code. US-020 … US-044 resolved the
same gap the same way.

Options: (a) a compile-only skeleton — the two method declarations only, each
throwing `NotImplementedException`; `Main` / the entry point, the package
references and every existing behaviour unchanged; IMPLEMENTATION owns and
completes it; (b) no skeleton — the tests do not compile until IMPLEMENTATION
and the red phase cannot be verified.

**Resolution (Owner, 2026-10-14): a)**.
