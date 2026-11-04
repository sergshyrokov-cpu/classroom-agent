---
id: US-075
epic: EPIC-1
title: A short hint under each kind of data problem, and banner links to each section
slug: data-problem-hints
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-04 from the Owner's proposal of the same day.
# Aligned with trebovaniya.md v110 (Epic 1, BR-047). Does not hold up the pilot.
---

# User Story

As a **Dean or Admin**

I want to see under each data problem what it means and who fixes it where

So that I can act on it without asking anyone what it is.

---

# Business Value

The «Стан даних» page says what is wrong, but for most kinds of problem not what to do. A first-time Dean meets
"Бали вище максимуму" with no word on who corrects it. Clear hints also shorten the note for Deans and Admins
planned before the pilot.

---

# Scope

**In scope:**

1. **The banner.** "N technical problems" and "M teaching" become two links, each to its own section of the page.
   A link does not wrap inside itself. A part with zero is plain text with no link.
2. **A hint under each kind of problem**, shown at once, with no click and no script, from the translation files.
   The agreed texts are in the table below.
3. **The Dean and technical problems:** as today, only the hint to tell the Admin, with no details. The title of
   the empty-list problem ends at its date for a Dean; the Admin's stays whole (OD-001).
4. **Archived courses** get no hint on fixing: Google allows no change there.

**Out of scope:** hints already in place, which stay; an expandable or pop-up form; a diagnosis of the particular
case; any new data, right, endpoint or audit event; a way to dismiss a problem.

## Hint texts

One-line texts end without a period; texts of several sentences keep them (NFR-078).

| Problem | Who sees the new hint | Ukrainian | English |
|---|---|---|---|
| Points above the maximum | Dean, Admin | Зазвичай це помилка введення: викладач виправляє оцінку або максимум завдання в Classroom, і після наступної синхронізації проблема зникає | Usually an entry error: the teacher corrects the grade or the item's maximum in Classroom, and the problem goes after the next synchronization |
| Item not in Classroom | Dean, Admin | Завдання видалено або повернуто в чернетку в Classroom, оцінки в програмі збережено. Якщо це помилка, зверніться до викладача. | The item was deleted or returned to draft in Classroom; its grades stay in the program. If that was a mistake, contact the teacher. |
| No successful run for 24 hours | Admin | Перегляньте стан синхронізації на сторінці підключення. Якщо запуски не починаються, повідомте Власника. | Look at the synchronization state on the connection page. If runs do not start, tell the Owner. |
| No successful run yet | Admin | Перевірте налаштування на сторінці підключення. Перша синхронізація великої школи може тривати понад добу. | Check the settings on the connection page. A large school's first synchronization can take over a day. |
| Google returns no course | Admin | З меншими ролями Google курсів не показує. Роль технічного акаунта змінює супер-адміністратор школи в консолі адміністратора Google. | With lesser roles Google shows no courses. The school's super-admin changes the technical account's role in the Google Admin console. |
| Technical account does not see courses | Admin | Якщо акаунт змінено помилково, поверніть попередній або дайте новому потрібну роль, і позначка зникне сама. Якщо курси справді видалено, позначте їх і підтвердьте. | If the account was changed by mistake, restore the previous one or give the new one the needed role, and the mark goes by itself. If the courses really were deleted, select them and confirm. |
| Course in an unknown state | Admin | Google повернув стан курсу, якого ця версія програми не знає, і курс не оновлюється. Повідомте Власника. | Google returned a course state this version of the program does not know, and the course is not updated. Tell the Owner. |

Problems that already have their hint keep it: the last run failed (diagnosis and link), no connection with the
Control Plane, a skipped course, a course not in Classroom, the Dean's "contact the Admin". The two hints that
name the connection page carry the existing link to it, for the Admin only.

---

# Acceptance Criteria

## AC-001 Two links in the banner

**Given** N and M both above zero

**When** any installation page opens

**Then** each part is a link to its own section of «Стан даних», the comma is outside the links, and neither link
breaks inside itself on a narrow screen.

## AC-002 A zero part is not a link

**Given** N or M is zero

**Then** that part is plain text with its usual wording, and the other part is a link.

## AC-003 Teaching hints

**Given** a current course with points above the maximum, and one with items not in Classroom

**When** a Dean or an Admin opens the page, in both languages

**Then** the hint of each kind is shown once for each course that has it, as in the table.

## AC-004 Technical hints for the Admin

**Given** each technical kind in the table

**When** an Admin opens the page, in both languages

**Then** the hint is shown with the kind's details, and the connection page link is where the table says.

## AC-005 The Dean sees no technical details

**Given** the same technical problems

**When** a Dean opens the page

**Then** none of the new technical hints or links appears, and the Dean sees the hint to tell the Admin, as today.
The title of the empty-list problem ends at its date, with no instruction to check the role (OD-001 a).

## AC-006 Archived courses

**Given** an archived course with such problems

**Then** the archived section shows no hint on fixing, and its marks are as before.

## AC-007 Works without scripts and in read-only mode

**Given** scripts off, or read-only mode

**Then** hints and banner links behave the same; no hint promises an action the mode blocks. The Specification
states how the confirmation sentence behaves when the confirmation is blocked (BR-026).

## AC-008 Nothing else changed

**Given** the counts, the kinds of problem and the rights

**Then** they are as before; the page calls no Google API and writes no audit event. Every new text exists in both
translation files, and a person checks the page on a computer and a phone.

---

# Open Decisions

## OD-001 The title of the empty-list problem for a Dean

Today a Dean reads the same title as the Admin: "Google не повертає жодного курсу з <дата> — перевірте роль
технічного акаунта". That is an instruction the Dean cannot carry out, and it is a detail of a technical problem.

- **a) (recommended)** A Dean's title ends at the date; the Admin's stays. The Dean then reads what happened and
  "Повідомте Адміністратора".
- b) Leave it as it is. One text less, but the Dean keeps an instruction meant for the Admin.

**Resolved 2026-11-04T14:38:44Z by the Owner: a).**

---

# Notes

- **The section anchors exist:** `data-health-technical`, `data-health-teaching` (`DataHealth/Index.cshtml`). The
  banner is in `_Layout.cshtml`, and its part wordings already exist as translation keys.
- **"Admin or Owner":** the unknown-state problem is listed as technical, fixed by the Admin in `trebovaniya.md`, but
  in practice only a program update fixes it, which is the Owner's; the hint says so without changing who sees what.
- **Not in this Story:** an item deliberately deleted in Classroom stays counted in the banner for good, since the
  mark never clears and there is no "read" mark. The Owner decided on 2026-11-04 to see how many there are in the
  pilot and decide after two pilot weeks.
- No new endpoint or schema is expected: API and database design should be empty deltas.
