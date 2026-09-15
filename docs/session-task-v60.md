# Задание на новую сессию (после v60)

Проект classroom-agent. Продолжаем после ревью AGENTS.md (закончено, последний коммит b361b9c).

Прочитай сначала:
- заголовок trebovaniya.md (changelog, сейчас v60);
- trebovaniya.md §7 «Открытые вопросы»;
- AGENTS.md.

Задачи по порядку:

1. Проверь, что CLAUDE.md (@AGENTS.md) подгрузил AGENTS.md в эту сессию.
   Если нет — скажи мне, и выясним почему.

2. Решим вопрос 25 (защита от CSRF). Предложи варианты текстом, пронумеруй,
   лучший пометь «(Рекомендую)». Варианты должны отвечать на все вопросы:
   - применяется ли antiforgery-токен ко всем изменяющим формам Razor и к
     REST-эндпоинтам, вызываемым из того же UI (и как токен передаётся в REST —
     например, заголовком);
   - нужен ли токен анонимным формам: вход Декана, вход Владельца, первый запуск
     Control Plane (защита от login CSRF);
   - служебный канал Control Plane ⇄ инсталляция (push-приёмник, проверка
     легитимности, проверка входа Админа) — браузера и cookie там нет; явно
     исключить его из antiforgery, защита — сетевая изоляция (SC-9);
   - значение `SameSite` сессионного cookie **для каждого хоста отдельно**:
     в инсталляции вход Админа идёт через редирект с Google, и `Strict` там
     ломает первую страницу после входа; в Control Plane OAuth нет;
   - правило «GET ничего не меняет»: всё, что изменяет данные (в том числе
     кнопка «Синхронизировать»), выполняется только POST/PUT/DELETE.

   Решение оформи новой версией trebovaniya.md (v61): текст решения — в §8
   (рядом с «Токен сессии — в httpOnly cookie»), пункт 25 из §7 убрать, номер не
   переиспользовать. Перенеси решение в производные документы:
   - docs/architecture/security-conventions.md — SC-2 и/или SC-4;
   - docs/architecture/api-conventions.md — API-7;
   - docs/product/non-functional-requirements.md — NFR-072;
   - docs/architecture/testing-conventions.md — TC-5 (тест: изменяющий запрос
     без токена отклоняется; служебный канал токена не требует);
   - .claude/skills/security-reviewer/SKILL.md — строку в таблице Step 6 и
     заменить заглушку «not yet defined» в Step 7;
   - docs/stories/US-001-owner-first-run-setup.md — затрагивается (формы первого
     запуска и входа — изменяющие и анонимные); обнови AC и отметку версии;
   - AGENTS.md — отметку «Verified against v60» на v61.
   Коммить и пушь в master.

3. Реши OD-001 из US-001 (политика пароля и блокировки входа Владельца: минимальная
   длина, сложность, lockout). Он блокирует HUMAN_SPEC_APPROVAL — вторую стадию
   workflow. Тот же формат: варианты текстом, пронумеровать, лучший пометить
   «(Рекомендую)». Решение перенеси в trebovaniya.md (та же v61, если ещё не
   закоммичена, иначе v62), US-001 (OD-001 — resolved, AC-005/006/008) и
   security-conventions.md SC-4 (строка «lockout policy open» в таблице анонимных
   эндпоинтов). Коммить и пушь в master.

4. Предложи, как начать US-001 через workflow (/so:start). SDK для старта не
   нужен: стадии SPECIFICATION → HUMAN_SPEC_APPROVAL → API_DESIGN → DB_DESIGN
   документные (docs/workflow/stage-map.yaml). Отдельно проверь, установлен ли
   .NET 10 SDK (dotnet --list-sdks) — он понадобится с TEST_WRITING. Если не
   установлен — скажи, что поставить. Если установлен — исправь в trebovaniya.md
   §8 фразу «На машине разработки сейчас стоит SDK 9.x».

Ни один скилл workflow ещё ни разу не запускался: dotnet-implementor,
security-reviewer, test-writer и story-orchestrator только что переписаны, а
spec-writer, openapi-designer и db-designer перенесены из customer-portal-NET,
который тоже не запускался. На первой Story следи за поведением всех семи
(первыми сработают story-orchestrator, spec-writer, openapi-designer и
db-designer) и сообщай о расхождениях со stage-map.yaml.
