---
id: US-024
epic: EPIC-5
title: Database statistics and last synchronization view
slug: database-statistics
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v90.
# Scope decided with the Owner: OD-001 … OD-010 in US-024 discovery (2026-10-12).
---

# User Story

As a **Dean** or an **Admin**

I want to see how much data the installation holds, when it was last
synchronized with Google and whether that went well, and to start a
synchronization from the same place

So that I know whether what I see in the journals and reports is current
before I rely on it.

---

# Business Value

`trebovaniya.md` §4, Epic 5: "Раздел показывает: общую статистику локальной БД
(кол-во курсов, пользователей, встреч Meet и т.п.), время последней синхронизации,
кнопку "Синхронизировать"". §2 matrix, "Раздел "Управление БД" (статистика)" and
"Запуск синхронизации": both roles. Today only the Admin sees the state of
synchronization, on the connection page (US-017, v80); the Dean has a button
(US-019) but no way to tell how fresh the data is. Epic 6 (v80) states that the
statistics screen is separate from the connection page. This is the only Story
of Epic 5 besides the retention purge (US-037).

---

# Scope

## In scope

- **Page** (OD-001) — one new page, "Database". The home page gets a new section
  with one link to it, placed last among the sections for both roles: after
  "Reports and templates", before the Admin-only section.
- **Counters** (OD-002), each one number computed when the page opens:
  - courses, broken down by the five statuses of §3
    (ACTIVE / ARCHIVED / PROVISIONED / DECLINED / SUSPENDED), and their total;
  - teachers and students (OD-003);
  - coursework items;
  - submissions;
  - Meet meetings;
  - Meet codes awaiting a decision — codes with neither a link to a course nor
    the "not a course" mark (§3 `MeetingCodeLink`, v88).
- **People** (OD-003) — a teacher is anyone currently in the roster of at least
  one course with the teacher role, a student likewise with the student role
  (§3 `CourseMembership`, the "currently in the roster" set; the US-022 OD-002
  rule). A person who has left every course is not counted, though their record
  may still await the retention purge (US-037). A person who is a teacher in one
  course and a student in another is counted in both.
- **Synchronization block** (OD-004), the same for both roles, from `SyncState`:
  - the time of the last successful synchronization;
  - the current state — in progress, completed, or failed;
  - the time of the last full run (v90);
  - the time up to which Meet meetings are loaded (v87).
  Before the first run, each time reads "not yet" rather than being blank.
  The `SyncState` counter of processed courses is not shown.
- **Failed run** (OD-005) — both roles see "failed" without the cause. The Dean
  sees a hint that the details are with the Admin; the Admin sees a link to the
  connection page, where the diagnosis lives (US-017). No diagnosis text, failed
  step or error appears on this page.
- **"Synchronize" button** (OD-006) — on this page for both roles, the same
  request as US-019: the same entry point, confirmation messages, read-only
  refusal and audit row. The existing buttons on the home page (Dean) and on the
  connection page (Admin) stay as they are. After the press the user returns to
  this page.
- **Read-only mode** (OD-007) — the page works and shows everything. The button
  stays visible and a press is refused exactly as in US-019 (AC-005 there).
  Opening the page writes nothing.
- **No auto-refresh** (OD-008) — the page shows the state at the moment it was
  opened; a reload shows a newer one.
- **Roles** — Dean and Admin (§2 matrix), with an authorization policy for the
  "Database management" row of §2. Allowed-role and forbidden-role tests (TC-5);
  both v1 roles are allowed, so the forbidden case is the unauthenticated
  request.
- **Address** — no query parameters besides what the US-019 confirmation message
  already needs; no personal data in it.
- **Translations** (NFR-073) for the page, the home-page section and link, every
  counter label, the course statuses, the synchronization states, "not yet", the
  hint, the link to the connection page and the button messages.

## Out of scope

- Any diagnosis of a failed run on this page (OD-005); it stays on the connection
  page, Admin only (US-017 AC-007).
- The processed-courses counter of the last run (OD-004).
- Automatic refresh of the page (OD-008).
- The size of the database on disk (OD-009) — an Owner concern, not the school's.
- Export of the statistics (OD-010) — no requirement asks for it.
- Counters of application accounts, report templates or audit rows (OD-002).
- Counting people who have left every course, or any record of a person that is
  not a current membership (OD-003).
- Any change to synchronization itself, to the US-019 request, or to the existing
  buttons and the connection page.
- Any history of past runs — `SyncState` is one row updated in place (US-013
  OD-004).

---

# Acceptance Criteria

## AC-001 Page and link

**Given** a signed-in Dean or Admin

**Then** the home page has a "Database" section, last among the sections both
roles see, with a link to the Database page.

## AC-002 Course counters

**Given** courses in each of the five statuses

**Then** the page shows the number of courses of each status and their total.

## AC-003 People counters

**Given** a person teaching a course, a person studying in a course, a person
teaching one course and studying in another, and a person who has left the
roster of every course

**Then** the first and the third are counted as teachers, the second and the
third as students, and the fourth is not counted.

## AC-004 Teaching and Meet counters

**Then** the page shows the number of coursework items, of submissions, of Meet
meetings, and of Meet codes with neither a link to a course nor the "not a
course" mark.

## AC-005 Synchronization block

**Given** a `SyncState` with a last successful run, a last full run and a Meet
loaded-up-to time

**Then** the page shows those three times and the current state — in progress,
completed or failed — the same to both roles, and does not show the processed
counter.

## AC-006 Before the first run

**Given** no synchronization has ever run

**Then** each time reads "not yet" and the counters show zero.

## AC-007 Failed run

**Given** the last run failed

**Then** both roles see "failed" and no cause; the Dean sees a hint that the
Admin has the details, and the Admin sees a link to the connection page.

## AC-008 Button

**Given** an installation not in read-only mode

**When** a Dean or an Admin presses "Synchronize" on this page

**Then** a run is requested exactly as by the US-019 button, one audit row is
written as US-019 writes it, and the user is back on this page with the US-019
confirmation message.

## AC-009 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the page shows its counters and the synchronization block, the button
is visible, a press is refused with the US-019 message naming the reason, and
opening the page writes nothing.

## AC-010 Roles

**Then** a Dean and an Admin can open the page, and an unauthenticated request is
refused; a press without a valid antiforgery token is refused.

## AC-011 Bilingual

**Then** the page, the home-page section, the labels, statuses, states, the hint
and the messages are in the user's UI language.

## AC-012 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner before activation (US-024 discovery, 2026-10-12),
all as recommended:

- **OD-001** A separate "Database" page; its home-page section is last among the
  sections both roles see.
- **OD-002** Counters: courses by status and in total, teachers, students,
  coursework, submissions, Meet meetings, Meet codes awaiting a decision. No
  application accounts or templates.
- **OD-003** People are counted by current roster membership, teachers and
  students separately (the US-022 rule); people who left every course are not
  counted.
- **OD-004** The synchronization block shows the last successful run, the current
  state, the last full run and the Meet loaded-up-to time, to both roles; not the
  processed counter.
- **OD-005** A failed run shows "failed" without a cause; a hint for the Dean, a
  link to the connection page for the Admin.
- **OD-006** A "Synchronize" button on this page for both roles; the existing
  buttons are unchanged.
- **OD-007** In read-only mode the page works; the button behaves as in US-019.
- **OD-008** No auto-refresh.
- **OD-009** No database size on disk.
- **OD-010** No export.

---

# Notes

- §4 says "пользователей". In v1 the people the school works with are synced
  participants, not accounts (§2, Epic 7), so "users" here means teachers and
  students; application accounts are managed on the Admin's pages.
- The processed counter is left out because after v90 an incremental run skips
  unchanged archived courses (US-018), so the counter is smaller than the course
  count and would read as missing data (US-014 api-design §6, US-015 api-design).
- The teacher counter for ACTIVE courses need not equal the US-022 list with its
  default filter: this page counts teachers of a course in any status.
- "Failed" without a cause keeps US-017 AC-007 and US-019 OD-002: the diagnosis
  is Admin-only and lives on the connection page.
- Times are shown the way the other pages of the installation show them.
