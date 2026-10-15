---
id: US-049
epic: EPIC-6
title: Session-signing keys are encrypted at rest with the service account's Windows protection
slug: encrypt-data-protection-keys
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from the pre-pilot security audit, finding F-2
# (docs/reviews/audits/2026-10-pre-pilot-security-audit.md); to be reviewed and
# edited by the Owner before activation. Aligned with trebovaniya.md v94.
# Option 1 chosen by the Owner on 2026-10-15: DPAPI bound to the Windows
# account the service runs under, plus the ACL check at deployment.
---

# User Story

As the **Owner** of the service

I want the keys that sign session cookies and antiforgery tokens to be
encrypted on disk so that only the Windows account the service runs under can
read them

So that a copy of the key folder — taken from a backup by mistake, by another
program on the same computer, or from a stolen disk — cannot be used to forge a
sign-in as an Admin, a Dean or the Owner.

---

# Business Value

`trebovaniya.md` §8 (v64) keeps each host's Data Protection keys in their own
directory, "с доступом только у процесса приложения", outside the database and
the backups. The pre-pilot security audit (finding F-2) found that both hosts
store these keys **unencrypted**: `PersistKeysToFileSystem` is called without
any `ProtectKeysWith…`, and ASP.NET Core does not encrypt keys at rest when an
explicit directory is configured, on Windows too. The folder's access rights
are the only protection.

Whoever reads that folder can forge a session cookie for any account of that
host, the Owner's included. The pilot server is also the Owner's daily-work PC
(`trebovaniya.md` §9, v92), where other programs run under other accounts, so
one access-rights setting is too thin a protection for that asset.

Windows Data Protection API (DPAPI), bound to the Windows account the service
runs under (§9 v92: a dedicated account per service), makes a copied key file
useless anywhere else and to any other account on the same computer. It is part
of ASP.NET Core — no new package. Users see nothing. Losing the keys only signs
users out, which §8 already accepts.

---

# Scope

**In scope:**

- both hosts — the installation and the Control Plane — encrypt their Data
  Protection keys with DPAPI bound to the current Windows user (not the machine);
- a host running on Windows never writes an unencrypted key;
- the deployment instructions (DC-2, and the deployment kit's README outside the
  repository) say that the first console run — the Control Plane's first-run
  setup with the one-time code, and any console start of the installation — is
  done **under the service's own Windows account**, and that the folder's access
  rights are checked at deployment;
- what happens to keys written before this Story (OD-002);
- tests.

**Out of scope:**

- where the key folder is, its access rights as such, and that it is not backed
  up (§8 v64, DC-13) — unchanged;
- the service-account key and the OAuth client secret (SC-7) — a different secret
  store, unchanged;
- encryption on an operating system other than Windows — the pilot and §9 v92
  run on Windows (OD-003).

---

# Acceptance Criteria

## AC-001 New keys are written encrypted

**Given** either host started on Windows with an empty key folder

**When** it creates its first key

**Then** the key file in the folder holds no plain key material — it carries
the DPAPI-encrypted form — and the host works: a user can sign in and submit a
form.

## AC-002 Another Windows account cannot use the keys

**Given** keys written by a host running under one Windows account

**When** the same host is started under a different Windows account

**Then** it cannot decrypt those keys: earlier session cookies and antiforgery
tokens are not accepted (users must sign in again), and nothing is exposed.
What the host does with the unreadable keys is fixed by the Specification
(OD-002).

## AC-003 Restarting under the same account keeps users signed in

**Given** keys written by a host running under its service account

**When** the host restarts under the same account

**Then** a session cookie issued before the restart is still accepted, as today
(§8 v64: "без сохранения ключей каждый перезапуск … разлогинивал бы всех").

## AC-004 The deployment instructions say under which account to run first

**Given** `deployment-conventions.md` DC-2 (and, outside the repository, the
deployment kit's README)

**When** the Owner follows them

**Then** the first console run of each host is done under that host's service
account, and checking the key folder's access rights is a named step.

---

# Open Decisions

## OD-001 Record the rule in the requirements first?

**Question.** The key-ring rule lives in `trebovaniya.md` §8 (v64). Encrypting
the keys at rest is a security rule, not a coding detail.

- **(a) Yes — a new requirements version adds it to §8 (and SC-7, DC-2) before
  this Story is activated (recommended)** — the rule then stays where every
  later review looks for it, the way US-045 did (v89).
- (b) No — the Story alone defines it.

**Resolution (the Owner, 2026-10-15):** (a) — a new version of `trebovaniya.md`
adds the rule to §8 (with SC-7 and DC-2 following it) before this Story is
activated.

## OD-002 Keys already on disk without encryption

**Question.** A host deployed before this Story has unencrypted keys. What
happens to them?

- **(a) Nothing special — the host keeps reading them and writes every new key
  encrypted; the old ones expire on their own schedule (90 days) (recommended
  for an installed host).** Since the pilot has not been deployed yet, the
  deployment kit will simply start with an empty folder, so in practice no
  unencrypted key ever exists on the pilot server.
- (b) The deployment instructions say to delete the key folder once after the
  upgrade (everyone signs in again).

**Resolution (the Owner, 2026-10-15):** (a) — keys already on disk keep being
read and expire on their own schedule; every new key is written encrypted.

## OD-003 A host started on a non-Windows system

**Question.** DPAPI exists only on Windows. Tests run on Windows today, and §9
v92 deploys to Windows.

- **(a) The host refuses to start outside Windows with a clear log line
  (recommended)** — no path ever writes an unencrypted key.
- (b) Outside Windows the keys stay unencrypted, as today.

**Resolution (the Owner, 2026-10-15):** (a) — outside Windows the host refuses
to start and logs the reason; no path writes an unencrypted key.

---

# Notes

- Origin: pre-pilot security audit 2026-10-15, finding F-2.
- Code today: `InstallationSecurityServices.AddInstallationSecurity` and
  `ControlPlaneSecurityServices.AddControlPlaneSecurity` call
  `PersistKeysToFileSystem` without `ProtectKeysWith…`.
- DPAPI bound to the user needs that account's profile loaded; a Windows service
  running under a dedicated account has it. This is a point for the
  Specification and the deployment step, not a new requirement.
- Expected stages: API_DESIGN NOT_APPLICABLE; DB_DESIGN NOT_APPLICABLE.
- No new NuGet package.
- After it ships, rebuild the deployment kit (`classroom-agent-deploy`) before
  pilot stage 3.
