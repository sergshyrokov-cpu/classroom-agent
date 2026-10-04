---
id: US-019
epic: EPIC-1
title: Trigger a synchronization from the UI
slug: trigger-sync-from-ui
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v81.
# OD-001 … OD-008 were all resolved by the Owner on 2026-10-04, before
# activation.
---

# User Story

As an **Admin** or a **Dean**

I want a "Synchronize" button that asks for a synchronization run right now

So that the Admin can check, right after connecting the school, that the
connection really works, and a Dean can bring the data up to date without
waiting for the next scheduled run.

---

# Business Value

`trebovaniya.md` §2 gives "Запуск синхронизации" to both Admin and Dean
(BR-004) and explains why the Admin has it too: "он должен уметь проверить, что
настройка заработала, сразу после подключения". §4 Epic 1: "Декан может вручную
инициировать синхронизацию (кнопка)". §8: "Кнопка "Синхронизировать" — только
триггерит серверный эндпоинт".

Today a run starts only on the schedule (US-013; the default interval is an
hour). US-013 built the coordinator's out-of-schedule entry point
(`SyncRunCoordinator.Request`, US-013 OD-007) precisely so this Story adds only
the button, its endpoint, its authorization policy and its audit row
(US-013 API design §4). US-013 … US-017 and US-037 list the manual start as out
of scope "— US-019".

---

# Scope

## In scope

- A "Synchronize" button:
  - for the **Admin** — on the connection page (`WorkspaceConnection`), next to
    the "Last synchronization" block US-017 added;
  - for the **Dean** — on the home page, the only screen a Dean has today.
- One state-changing form `POST` behind both buttons, with the antiforgery
  token (API-7) and an authorization policy granted to Admin and Dean only
  (§2, BR-004; deny by default, SC-4).
- The press **enqueues** a request through the existing coordinator entry point
  and answers at once; it never waits for the run and never reports its
  result (BR-040, AD-5).
- A press while a run or a retention purge is in progress is **accepted and
  remembered**; the remembered run starts right after the current work
  (US-013 OD-007, US-037 API design: the answer does not depend on a purge in
  progress). Several presses collapse into one remembered request.
- A confirmation message after the press: "synchronization requested", or
  "requested — it will start after the current synchronization" when work is
  in progress. No result, status or diagnosis is shown to the Dean.
- **Read-only mode:** the button stays visible; a press is refused in
  `Application` (AD-6) with a message naming the reason — grace period expired,
  suspended by the Owner, legitimacy never confirmed (BR-025, API-5), as "Check
  access" does. Nothing is enqueued.
- **Audit (§5, SC-11):** a new `AuditAction` member for a manual start, its
  check-constraint migration, and one audit row per accepted press and per
  press refused in read-only mode; actor = the user who pressed, no personal
  data beyond what SC-11 allows.
- Ukrainian and English texts for the button and every message (NFR-073).

## Out of scope

- A separate JSON endpoint `POST /api/v1/sync` (API-4) — see OD-007.
- Anything a Dean sees about the state or result of a run — US-024.
- Automatic refresh of the "Last synchronization" block — the Admin reloads the
  page (OD-008).
- A rate limit or cooldown on the button (OD-004).
- Starting a Meet pull separately — EPIC-4.
- Any change to how a run itself works: schedule, retries, diagnosis
  (US-013, US-017), incremental behaviour (US-018).

---

# Acceptance Criteria

## AC-001 The Admin requests a run from the connection page

**Given** a signed-in Admin on the connection page of an installation that is
not in read-only mode, with no run or purge in progress

**When** they press "Synchronize"

**Then** a run is requested through the coordinator, the page answers at once
with "synchronization requested", the run starts in the background, and one
audit row of the manual-start action is written with the Admin as actor.

## AC-002 The Dean requests a run from the home page

**Given** a signed-in Dean on the home page, not in read-only mode

**When** they press "Synchronize"

**Then** the same happens as in AC-001 with the Dean as actor, and the Dean is
shown only the confirmation — no status, time or diagnosis of any run.

## AC-003 A press during a run or a purge is remembered

**Given** a run or a retention purge in progress

**When** an Admin or a Dean presses "Synchronize"

**Then** the request is accepted, the message says it will start after the
current work, one audit row is written, and exactly one further run starts
after the current work ends.

## AC-004 Repeated presses collapse into one run

**Given** several presses, by one or several users, while a run is in progress

**Then** each press is answered and audited, and only one further run starts
after the current one.

## AC-005 Read-only mode refuses the press

**Given** an installation in read-only mode (any of the three reasons, BR-025)

**When** an Admin or a Dean presses "Synchronize"

**Then** nothing is enqueued, no Google call is made, the user sees a message
naming the reason, the refusal is enforced in `Application` and not by the UI,
and one audit row records the refused manual start.

## AC-006 Only Admin and Dean may request a run

**Given** an anonymous request, or a request without a valid antiforgery token

**When** it posts to the endpoint

**Then** it is refused and nothing is enqueued or audited as a manual start;
each protected endpoint has an allowed-role and a forbidden-role test (TC-5).

## AC-007 The UI is bilingual

**Given** a user whose UI language is Ukrainian or English

**Then** the button and every message of this Story appear in that language
(NFR-073).

## AC-008 Tests never reach Google

Every test substitutes the Google ports; whether a run started is observed
through the coordinator, not by real synchronization against Google (TC-4).

---

# Open Decisions

All eight were resolved by the Owner on 2026-10-04, before activation (each as
the recommended option). Resolutions are kept next to the question.

## OD-001 Where the button lives

Options: (a) Admin — on the connection page next to "Last synchronization";
Dean — on the home page; (b) both on the home page only; (c) the Dean's button
waits for US-024.

**Resolution:** (a).

## OD-002 What the Dean sees after a press

Options: (a) only the confirmation "synchronization requested"; (b) also the
time of the last successful run.

**Resolution:** (a) — the diagnosis is Admin-only (US-017 AC-007), and the
state screen is US-024.

## OD-003 A press while a run or a purge is in progress

Options: (a) accept and remember it; the run starts after the current work;
(b) answer "already running" and remember nothing.

**Resolution:** (a) — the behaviour the coordinator already has (US-013
OD-007, US-037 API design).

## OD-004 Many presses in a row

Options: (a) they collapse into one remembered request; no rate limit;
(b) a cooldown, e.g. once a minute per user.

**Resolution:** (a).

## OD-005 What is audited

Options: (a) every accepted press and every press refused in read-only mode;
(b) accepted presses only.

**Resolution:** (a) — as "Check access" (`AccessCheckRun`) records a run and a
refusal.

## OD-006 The button in read-only mode

Options: (a) visible; a press is refused with a message naming the reason;
(b) hidden (server-side refusal still required, AD-6).

**Resolution:** (a) — as "Check access".

## OD-007 A JSON API endpoint

Options: (a) none — a form `POST` like every other screen today; (b) a JSON
`POST /api/v1/sync` per API-4.

**Resolution:** (a). Note for SPECIFICATION: API-4 lists
`POST /api/v1/sync` → `202 Accepted` "with the `SyncState` id", but a
remembered request has no `SyncState` id yet; the Specification records how
this Story relates to that row of API-4.

## OD-008 Automatic refresh of "Last synchronization"

Options: (a) none — the Admin reloads the page; (b) the page polls the state.

**Resolution:** (a).

---

# Notes

- The coordinator entry point (`Request`) always accepts; the read-only check
  belongs in front of it, in the `Application` use case, so a refused press
  never reaches the coordinator. The background run keeps its own read-only
  skip (US-013 FR-007).
- What a press does when no `WorkspaceConnection` is saved yet: the run then
  behaves as a scheduled run would without a connection; the Specification
  states which, from US-013.
- A Dean with a temporary password is held on the forced password change page
  (US-012) and cannot reach the home page; no special case is needed.
- The audit row's exact target type and refusal category reuse the existing
  `AuditTargetType` / `AuditRefusalCategory` where they fit; the DB design
  decides whether either enum grows.
