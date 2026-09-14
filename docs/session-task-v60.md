# Задание на новую сессию (после v60)

Проект classroom-agent. Продолжаем после ревью AGENTS.md (закончено, последний коммит b361b9c).

Прочитай сначала:
- заголовок trebovaniya.md (changelog, сейчас v60);
- trebovaniya.md §7 «Открытые вопросы»;
- AGENTS.md.

Задачи по порядку:

1. Проверь, что CLAUDE.md (@AGENTS.md) подгрузил AGENTS.md в эту сессию.
   Если нет — скажи мне, и выясним почему.

2. Решим вопрос 25 (защита от CSRF): antiforgery-токен для изменяющих форм
   Razor и для REST-эндпоинтов, вызываемых из того же UI, и значение SameSite
   у сессионного cookie. Предложи варианты текстом, пронумеруй, лучший пометь
   «(Рекомендую)». Решение оформи новой версией trebovaniya.md (v61) и перенеси
   в производные документы: security-conventions.md (SC-2 или SC-4), строку
   чек-листа в .claude/skills/security-reviewer/SKILL.md (Step 6) и US-001, если
   его затрагивает. Коммить в master.

3. Проверь, установлен ли .NET 10 SDK (dotnet --list-sdks). Если да —
   предложи, как начать US-001 через workflow (/so:start). Если нет — скажи,
   что поставить.

Скиллы dotnet-implementor, security-reviewer, test-writer и story-orchestrator
только что переписаны и ни разу не запускались. На первой Story следи за их
поведением и сообщай о расхождениях со stage-map.yaml.
