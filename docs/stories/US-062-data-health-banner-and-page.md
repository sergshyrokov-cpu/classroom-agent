---
id: US-062
epic: EPIC-1
title: Data-health banner and the «Стан даних» page, with the Admin's confirmation of deleted courses
slug: data-health-banner-and-page
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-27 from trebovaniya.md v102 (`33174fe`,
# derived rules BR-047, BR-048, BR-049 in `236832a`). Third of the v99 Stories
# ("Story 3"); carries the Admin's confirmation (BR-049, v101) moved here from
# US-059, and the empty-list "since" date (v102 item 3). Before the pilot.
# AMENDED 2026-10-27 for trebovaniya.md v103 (`5cea77b`, BR-047 in `bcc5447`):
# the technical problem «нет связи с Control Plane» (Owner chose amending this
# Story over a separate one, 2026-10-27). AC-013, AC-014 added.
---

# User Story

As a **Dean**

I want every page to tell me, quietly, how many problems the data has, and one
page to list them with what to do about each

So that I do not trust data that synchronization could not keep current, and I
know whether to talk to a teacher or to the Admin.

As an **Admin**

I want the technical problems in detail, and to confirm that courses a new
technical account cannot see were really deleted

So that I can fix what is mine and clear a problem that has a known cause.

---

# Business Value

Synchronization can succeed and still leave things the Dean must know (v99):
points above the maximum, items or courses gone from Classroom, a course that is
not updated, a run that failed, no synchronization for a day. Deans trust the data
and will not look for these themselves (Owner 2026-10-26). The banner and the page
make them visible; the hints say who fixes what.

---

# Scope

**In scope — problems (BR-047):**

- **teaching**, per course: points above the maximum (BR-067); items "not in
  Classroom" (BR-045); course skipped for out-of-range or negative values (BR-043)
  — hint «курс не обновляется: в Classroom введено недопустимое значение баллов,
  обратитесь к преподавателю»; course "not in Classroom" (BR-048) — hint «курс
  удалён или скрыт в Classroom, уточните у преподавателя»;
- **technical**: the last run failed (a permission error included); no successful
  run for more than 24 hours, or none yet — not raised in read-only mode; a course
  skipped for an unknown state; Google returned an empty course list while courses
  are stored — «Google не повертає жодного курсу з <дати> — перевірте роль
  технічного акаунта» (BR-048, v102); the technical account does not see courses
  the previous one saw — one problem for all such courses, archived ones included
  (BR-049); **no connection with the Control Plane** (v103) — the last successful
  legitimacy check (BR-081) was more than 24 hours ago while the grace period
  (BR-025) still runs: «Нет связи с сервисом с <даты последней успешной
  проверки>; с <даты конца grace period> программа перейдёт в режим «только
  чтение», если связь не восстановится»; a shorter outage raises nothing; once
  read-only mode starts the problem disappears and the read-only message replaces
  it; it is fixed by the Owner, not the Admin, so the Admin's hint is «сообщите
  Владельцу», with the `upgrade_required` reason when that is the cause (BR-027);
- **counting**: one course × one kind of problem = 1; a problem without a course
  = 1; archived courses are not counted (except inside the single BR-049 problem,
  which counts once);
- **current state only**: a problem disappears when its cause is gone; no expiry,
  no "read" mark.

**In scope — banner:** at the top of every installation page of a signed-in Admin
or Dean, while any problem exists: «N технических проблем, M учебных» linking to
«Стан даних»; restrained — colour only, no sound, pop-up or blinking.

**In scope — «Стан даних» page:**

- for each teaching problem: the course, the kind and where exactly (item, student,
  points and maximum; for a skipped course — since when it is not updated), linking
  to the course's journal; a never-loaded skipped course shown by the name from
  Google's course list (BR-046);
- archived courses with problems listed separately, without counting;
- next to the teaching problems, the Dean's hint «Если с преподавателем решить не
  удалось, обратитесь к Администратору» (v100);
- technical problems: the Dean sees that each exists and «сообщите
  Администратору»; the Admin sees the details, worded as in the synchronization
  state (Epic 6), and for BR-049 the list of the courses;
- permission matrix §2: both roles see the banner and the page (v99).

**In scope — the Admin's confirmation (BR-049, §2: Admin ✔, Dean ✘):** on
«Стан даних» the Admin selects courses "not visible to the technical account" and
confirms «удалены в Classroom»: each gets the mark "not in Classroom" dated by the
confirmation and follows BR-048 from then on; the action is audited with the course
ids (SC-11) and refused in read-only mode (AD-6, BR-026).

**In scope — empty-list date (v102):** the date since which course lists have come
back empty in a row is stored (today `SyncState.LastCourseListEmpty` is a flag); the
first empty run of a streak sets it, later empty runs keep it, a non-empty list
clears it; with its EF Core migration (PC-2).

**In scope — common:** built from stored data only, never calling Google, so the
banner and page are visible in read-only mode; every text from the translation
files, Ukrainian and English (NFR-073); tests with synthetic data (TC-4), allowed
and forbidden role tests for the page and the confirmation (TC-5).

**Out of scope:**

- marks on the course list, card, journal, report, export (US-060) and in Meet
  reports (US-061);
- narrowing problems to the courses a Dean is assigned to (dean scope, later
  edition; in v1 every Dean sees all courses);
- history of problems (log and audit hold it);
- notifications outside the installation (e-mail etc.).

---

# Acceptance Criteria

## AC-001 The banner counts problems

**Given** a course with two above-maximum grades on different items, the same
course with an item "not in Classroom", another course "not in Classroom", and the
last run failed

**When** the Dean opens any page

**Then** the banner says «1 техническая проблема, 3 учебных» and links to «Стан
даних».

## AC-002 No problem, no banner

**Given** no problem of any kind

**When** the Dean opens any page

**Then** there is no banner.

## AC-003 Archived courses are listed, not counted

**Given** an archived course with an above-maximum grade and no other problem

**When** the Dean opens a page and «Стан даних»

**Then** there is no banner, and the page lists the course in the separate archived
list with the problem's details.

## AC-004 A problem disappears with its cause

**Given** an above-maximum grade shown as a problem

**When** a run loads the corrected grade

**Then** the problem is gone from the banner and the page.

## AC-005 Teaching details and hints for the Dean

**Given** an above-maximum grade, an item "not in Classroom", a course skipped for
out-of-range values loaded before, a never-loaded course skipped for out-of-range
values, and a course "not in Classroom"

**When** the Dean opens «Стан даних»

**Then** each is listed with its course (the never-loaded one by its name from
Google's list), kind and details — item, student, points and maximum for the
grade; since when for the skipped course — with a link to the course's journal
where one exists; the skipped and the "not in Classroom" courses carry their hints;
the hint to contact the Admin is shown next to the teaching problems.

## AC-006 Technical problems: hint for the Dean, details for the Admin

**Given** the last run failed with a permission error, and a course skipped for an
unknown state

**When** the Dean and then the Admin open «Стан даних»

**Then** the Dean sees both problems with «сообщите Администратору» and no details;
the Admin sees both with their details.

## AC-007 Staleness

**Given** the last successful run was 25 hours ago (synthetic clock)

**When** the Dean opens a page

**Then** there is a technical problem «no successful synchronization for more than
24 hours»; with no successful run ever, the same problem says none yet; in
read-only mode neither is raised.

## AC-008 An empty course list since a date

**Given** stored courses and three runs in a row reading an empty course list, the
first at E1

**When** the Admin opens «Стан даних»

**Then** there is one technical problem saying Google has returned no course since
E1, and no staleness problem; after a run reading a non-empty list the problem is
gone.

## AC-009 Courses not visible to the technical account are one problem

**Given** three courses marked "not visible to the technical account", one of them
archived

**When** the Admin and the Dean open «Стан даних»

**Then** there is one technical problem counted once; the Admin sees the three
courses listed; the Dean sees the problem and «сообщите Администратору».

## AC-010 The Admin confirms deletion

**Given** the data of AC-009

**When** the Admin selects two of the courses and confirms «удалены в Classroom»

**Then** those two carry the mark "not in Classroom" dated by the confirmation, an
audit event with their ids is written, the third stays "not visible"; the two now
count as teaching problems unless archived.

## AC-011 Only the Admin confirms, and not in read-only mode

**Given** the data of AC-009

**When** a Dean posts the confirmation, and the Admin posts it in read-only mode

**Then** the Dean is refused as forbidden, the Admin is refused by the read-only
rule, and no mark changes.

## AC-012 Visible in read-only mode, in both languages

**Given** read-only mode and problems of both kinds

**When** a user with English chosen opens a page and «Стан даних»

**Then** the banner and the page are shown, all texts in English, names as Google
returned them.


## AC-013 No connection with the Control Plane during the grace period

**Given** the last successful legitimacy check at C1, the clock at C1 + 25 hours
(synthetic), the grace period still running

**When** the Dean and then the Admin open a page and «Стан даних»

**Then** the banner counts one technical problem; the page says there has been no
connection with the service since C1 and that read-only mode starts at the end of
the grace period (C1 + 7 days) if it is not restored; the Dean sees «сообщите
Администратору», the Admin sees «сообщите Владельцу»; at C1 + 23 hours there is no
such problem.

## AC-014 The problem gives way to read-only mode

**Given** the data of AC-013 and the clock past the end of the grace period

**When** the Admin opens a page and «Стан даних»

**Then** there is no "no connection" problem; the read-only message is shown
instead; with `upgrade_required` as the cause during the grace period, the Admin's
details name that reason.
---

# Open Decisions

All resolved by the Owner on 2026-10-27 before activation (option a) each).

## OD-001 Which pages carry the banner

"Every installation page" (BR-047). Pages a signed-in user sees are meant; the
sign-in page and error pages have no user.

- **a) (recommended)** — every page rendered for a signed-in Admin or Dean except
  the forced password change: that page exists only to change the password, and
  its link would lead nowhere until the password is changed.
- b) — every page of the signed-in layout without exception.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

## OD-002 The empty-list date of an installation upgraded while the flag is set

At migration a stored `LastCourseListEmpty = true` has no date.

- **a) (recommended)** — take the start of the last run as the date: the latest
  certain instant; it understates the duration, never invents a longer one.
- b) — leave it without a date until the streak ends.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

---

# Notes

- **Control Plane problem (v103):** `LegitimacyState.LastSuccessfulCheckAt` and
  `Compatibility`, the grace-period rule `Domain/Rules/GracePeriod.cs`; no schema
  change expected for it.
- **Data already stored:** marks and skip records (US-058, US-059), `SyncState`
  (status, last error, last success), `Submission` grades vs `CourseWork.MaxPoints`
  for above-maximum, `Course.State` for archived.
- **Cost of the banner:** it is computed on every page; the spec and design decide
  how (one counting query, no Google call). An above-maximum check over all
  submissions on every request may need care.
- **Audit:** the confirmation event is already listed in SC-11 (v101) and §5
  ("подтверждение … удалены в Classroom (id курсов)").
- Expected stages: API_DESIGN applies (the confirmation is a POST; the page is a
  Razor page), DB_DESIGN applies (the empty-list date).
- Suggested order: after US-060, so the journal links land on pages that already
  show the marks; not a hard dependency.
- No new NuGet package, no new requirements version.
