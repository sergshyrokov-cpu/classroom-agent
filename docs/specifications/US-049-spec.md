---
artifact_type: specification
story: US-049
version: 1
status: APPROVED
created_at: 2026-10-16T12:20:00Z
updated_at: 2026-10-16T12:50:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-049-encrypt-data-protection-keys.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/decisions/US-049-open-decisions.md
    version: 1
supersedes: null
---

# US-049 Specification — Session-signing keys are encrypted at rest with the service account's Windows protection

## 1. Overview

Both hosts — the installation and the Control Plane — keep their ASP.NET Core
Data Protection keys (the keys that protect session cookies and antiforgery
tokens) in a configured directory. Today they are written there **unencrypted**:
`InstallationSecurityServices.AddInstallationSecurity` and
`ControlPlaneSecurityServices.AddControlPlaneSecurity` call
`PersistKeysToFileSystem` with no `ProtectKeysWith…`, and ASP.NET Core does not
encrypt keys at rest when an explicit directory is configured.

This Story makes every key a host writes encrypted with Windows DPAPI bound to
the Windows account the host runs under, as `trebovaniya.md` §8 v94 requires:

> «Ключи хранятся зашифрованными средствами Windows (DPAPI), привязанными к
> учётной записи, под которой работает служба хоста (раздел 9): скопированный файл
> ключей не прочитать ни под другой учётной записью того же сервера, ни на другом
> компьютере *(v94)*. Незашифрованный ключ не записывается — вне Windows хост не
> запускается; ключи, записанные до v94, читаются, пока не истекут *(v94)*.
> Поэтому первый запуск хоста из консоли — в том числе ввод одноразового кода
> Control Plane — выполняется под учётной записью его службы. Если пароль этой
> учётной записи сбросить принудительно, ключи станут нечитаемыми, и пользователям
> придётся войти заново; данные при этом не теряются *(v94)*.»

The same rule is in SC-7 and in DC-2 steps 2 and 6. Users see nothing.

## 2. Business Goal

A copy of a host's key directory — from a backup taken by mistake, read by
another program on the same computer, or from a stolen disk — must not let
anyone forge a session as an Admin, a Dean or the Owner. The pilot server is
also the Owner's daily-work PC (`trebovaniya.md` §9 v92), so the directory's
access rights alone are too thin a protection (pre-pilot security audit,
finding F-2).

## 3. Business Flow

1. The Owner deploys a host and starts it from a console **under that host's
   service account** (DC-2 step 2 for the Control Plane, step 6 for the
   installation), after confirming the key directory is readable only by that
   account.
2. On its first protect operation the host creates a key and writes it to the
   key directory encrypted with DPAPI for the current Windows user.
3. The Owner registers the host as a Windows service running under the same
   account (DC-14, §9 v92). The service decrypts the keys, so sessions and open
   forms survive every restart.
4. If the host is ever started under another account (or the service account's
   password is reset by force), the existing keys cannot be decrypted: the host
   still starts, creates a new encrypted key, and every user signs in again. No
   data is lost.
5. Outside Windows the host does not start.

## 4. Functional Requirements

- **FR-001 Encrypted with DPAPI, current user.** Each host — the installation
  and the Control Plane — configures its Data Protection key ring to encrypt
  every key it writes with Windows DPAPI bound to the **current Windows user**
  (the account the process runs under), not to the machine (§8 v94; Story scope).
  The key directory, the application names (`ClassroomAgent.Installation`,
  `ClassroomAgent.ControlPlane`), the key lifetime and everything else about the
  key ring stay as they are.
- **FR-002 No unencrypted key is ever written.** On Windows no code path writes
  a key to the key directory in plain form. A key file written by a host after
  this Story carries the DPAPI-encrypted form of the key and does not contain the
  key material in plain text (§8 v94).
- **FR-003 Keys the account cannot decrypt.** When the key directory holds a key
  the current account cannot decrypt (written under another account, on another
  computer, or after a forced password reset), the host (I-1):
  - still starts and serves requests;
  - does not use that key: a session cookie or antiforgery token protected with
    it is not accepted — the request is treated as signed out, a form post gets
    the existing antiforgery refusal page;
  - creates a new key, encrypted per FR-001, and works with it;
  - does not delete, rewrite or move the unreadable key file.
- **FR-004 Keys written before this Story (OD-002 a).** A key already in the
  directory in plain form keeps being read and used until it expires on its own
  schedule; the host does not rewrite it, re-encrypt it or delete it. Every key
  the host creates from then on is encrypted per FR-001 (§8 v94: «ключи,
  записанные до v94, читаются, пока не истекут»).
- **FR-005 Not on Windows — no start (OD-003 a).** A host started on an
  operating system other than Windows refuses to start before it can write any
  key, and reports the reason (I-3):
  - the installation through its existing start-up refusal — a `Fatal` line in
    its log file, written before the host exists
    (`InstallationLogging.WriteStartupRefusal`), and the process fails;
  - the Control Plane through its existing start-up failure path, the same one a
    missing required setting uses — the process fails with an exception whose
    message states the reason.

  The message names the cause — the Data Protection keys are encrypted with
  Windows DPAPI, which this operating system does not have — and contains no
  path secret or key material. The decision is made from the operating system the
  process runs on; no setting, variable or argument can switch it off. Its effect
  must be provable by a test running on Windows (the platform is supplied to the
  decision the way US-047 supplies `runsAsService`; the mechanism is a technical
  choice).
- **FR-006 Restart under the same account keeps sessions.** A host restarted
  under the account that wrote its keys decrypts them and accepts session
  cookies and antiforgery tokens issued before the restart, as today
  (§8 v64: «Без сохранения ключей каждый перезапуск … разлогинивал бы всех»).
- **FR-007 Deployment instructions.** `deployment-conventions.md` DC-2 already
  states (since the v94 document update) that the first console run of each host
  — the Control Plane's first-run setup with the one-time code (step 2) and any
  console start of the installation (steps 2 and 6) — is done under that host's
  service account, and that the key directory's access rights are confirmed
  before the first start. This Story changes no repository document for it
  (I-4). The deployment kit's README (`classroom-agent-deploy`, outside the
  repository) is brought into line when the kit is rebuilt before pilot stage 3
  (Story Notes) — outside this Story's commit.

## 5. Acceptance Criteria

The ids of the Story are kept; AC-005 and AC-006 make the resolutions of
OD-002 and OD-003 testable.

- **AC-001 New keys are written encrypted.** *Given* either host started on
  Windows with an empty key directory, *when* it creates its first key, *then*
  the key file holds no plain key material — it carries the DPAPI-encrypted form
  — and the host works: a user can sign in and submit a form. (FR-001, FR-002)
- **AC-002 Another Windows account cannot use the keys.** *Given* keys written
  by a host under one Windows account, *when* the same host is started under a
  different account, *then* it cannot decrypt them: earlier session cookies and
  antiforgery tokens are not accepted (users sign in again), nothing is exposed,
  and the host behaves as FR-003 states. (FR-003) — Verified automatically with a
  key the test account cannot decrypt standing in for "another account"; a run
  under a genuine second account is a manual check (I-2).
- **AC-003 Restarting under the same account keeps users signed in.** *Given*
  keys written by a host, *when* it restarts under the same account, *then* a
  session cookie issued before the restart is still accepted. (FR-006)
- **AC-004 The deployment instructions say under which account to run first.**
  *Given* DC-2, *when* the Owner follows it, *then* the first console run of each
  host is done under that host's service account, and checking the key
  directory's access rights is a named step. (FR-007) — Verified by inspection of
  DC-2 (I-4).
- **AC-005 Old unencrypted keys keep working.** *Given* a key directory holding
  a still-valid key in plain form, *when* a host starts, *then* a session cookie
  protected with that key is accepted, the plain key file is left as it is, and
  any new key the host writes is encrypted. (FR-004)
- **AC-006 No start outside Windows.** *Given* a host whose start-up decision is
  told it is not running on Windows, *when* it starts, *then* it refuses to start,
  reports the reason per FR-005, and no file is written to the key directory.
  (FR-005)

## 6. Validation Rules

- **VR-001** The DPAPI binding is to the current user only; a machine-wide
  binding (`protectToLocalMachine: true`) or any other key encryptor
  (certificate, DPAPI-NG) does not satisfy FR-001.
- **VR-002** A key file written after this Story is valid only if its key
  element is the encrypted form recognised by ASP.NET Core's DPAPI decryptor and
  no plain key-material element is present (FR-002). Tests check the file's
  content, not only that the host works.
- **VR-003** The operating-system decision of FR-005 accepts no configuration
  input: no setting, environment variable or command-line argument disables or
  forces it.
- **VR-004** The existing validation of the key directory setting
  (`DataProtection:KeyDirectory` — required, a creatable, writable directory,
  relative paths resolved against the content root; US-047 FR-004) is unchanged.

## 7. Security Requirements

- **S-01** The rule is §8 v94 / SC-7: keys encrypted at rest with DPAPI bound to
  the service's Windows account; a key written unencrypted is a finding.
- **S-02** No key material, DPAPI blob, or decrypted key appears in a log line,
  an error page or a response — including the log lines produced when a key
  cannot be decrypted (FR-003) and the start-up refusal (FR-005) (SC-10).
- **S-03** The key directory stays where it is: outside the repository, the
  database and backups (§8 v64, SC-7, DC-13). Nothing in this Story copies,
  exports or backs up a key.
- **S-04** No new endpoint, no new anonymous surface, no change to cookies,
  authorization policies, antiforgery or session timeouts (SC-2, SC-4).
- **S-05** No new NuGet package: DPAPI key encryption is part of ASP.NET Core
  Data Protection, already in the shared framework (AGENTS.md Technology Stack).
- **S-06** The service-account key and the OAuth client secret (SC-7 secret
  store) are not touched.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Key cannot be decrypted (other account, other computer, forced password reset) | Host starts; key skipped; new encrypted key created; old cookies → signed out (redirect to sign-in, or `401` API body under `/api/v1` as today); old antiforgery token → existing refusal page (FR-003) |
| Plain key from before this Story | Read and used until it expires (FR-004) |
| Host started outside Windows | Start refused with the reason (FR-005); no key written |
| Key directory missing / not writable | Unchanged — existing start-up refusal (VR-004) |

## 9. Non-Functional Requirements

- No user-visible change; no new translation entry (log lines are not
  translated — NFR-073 concerns the UI).
- No measurable start-up or request cost beyond one DPAPI call per key read
  and written.
- Runs on Windows 10 Pro (the pilot server) and on the development and test
  machines, which are Windows (§9 v92).

## 10. Out of Scope

- Where the key directory is, its access rights as such, and that it is not
  backed up (§8 v64, DC-13) — unchanged.
- The service-account key and the OAuth client secret (SC-7).
- Encryption on another operating system — the host refuses to start there
  (OD-003).
- Deleting or re-encrypting keys already on disk (OD-002).
- Rewriting DC-2 — already done with v94 (I-4); the deployment kit's README,
  which lives outside the repository.
- Creating the Windows service accounts and setting the directory's access
  rights — deployment steps (DC-2, DC-14), not code.

## 11. Open Decisions and Interpretations

All Open Decisions of the Story are resolved (OD-001 a, OD-002 a, OD-003 a —
`docs/decisions/US-049-open-decisions.md`). No new Open Decision.

Interpretations, for the reviewer and the Owner:

- **I-1 Unreadable keys are skipped, not deleted (FR-003).** The Story leaves
  this to the Specification (AC-002). §8 v94 says users then sign in again and
  no data is lost, and DC-2 step 2 says the service "creates new ones and signs
  everyone out" — so the host neither fails nor deletes the file. Leaving the
  file keeps a later run under the right account able to read it; it is ignored
  meanwhile.
- **I-2 How AC-002 is proved.** An automated test cannot switch Windows
  accounts. The tests use a key file the test account cannot decrypt (a DPAPI
  blob that does not decrypt) as the stand-in for "written under another
  account" — the host sees exactly the same failure. A run under a genuine
  second account is a manual check, recorded in the implementation report §3.1
  and done on the pilot server at deployment, like US-047 FR-009 and US-050
  AC-004.
- **I-3 How each host reports the non-Windows refusal (FR-005).** Each host uses
  its existing start-up failure path: the installation writes a `Fatal` line to
  its log file (as for an invalid setting); the Control Plane has no log file
  before its host is built, so — exactly like a missing required setting — the
  process fails with an exception whose message gives the reason (visible on the
  console, and in the Windows Application event log for a service). Adding a new
  pre-host log writer to the Control Plane only for this case is not done.
- **I-4 AC-004 needs no change in the repository.** The v94 update already put
  the "first console run under the service account" and "confirm the directory is
  readable only by that account" steps into DC-2 steps 2 and 6. AC-004 is
  checked by reading DC-2; the deployment kit's README is updated when the kit
  is rebuilt before pilot stage 3.

## 12. Traceability

| AC | Functional requirement | Validation / security |
|---|---|---|
| AC-001 | FR-001, FR-002 | VR-001, VR-002, S-01 |
| AC-002 | FR-003 | S-02, I-1, I-2 |
| AC-003 | FR-006 | — |
| AC-004 | FR-007 | I-4 |
| AC-005 | FR-004 | OD-002 |
| AC-006 | FR-005 | VR-003, S-02, I-3, OD-003 |

## 13. Notes

- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint, request or response
  changes); DB_DESIGN NOT_APPLICABLE (no schema change).
- DPAPI bound to a user needs that account's profile loaded; a Windows service
  under a dedicated account has it (Story Notes) — a deployment point, not a
  code requirement.
- Delegation: none — the requirement lookups (§8 v94, §9 v92, §7 v96, SC-7,
  DC-2, DC-3) and the code facts (both `Add…Security` methods, the start-up
  refusal paths) were read directly.
