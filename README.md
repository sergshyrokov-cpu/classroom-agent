# classroom-agent

Python/Streamlit-прототип для синхронизации данных Google Classroom (домен
`dac.ukr.education`) в локальную БД и формирования отчётов для учебной части.

Это черновой прототип. Целевая переработка в стабильную .NET-систему описана
в [`trebovaniya.md`](trebovaniya.md).

## Возможности

- Синхронизация курсов, участников, групп, заданий/материалов и оценок через
  Google Classroom API (service account + domain-wide delegation)
- Локальный кэш данных в SQLite (`db.py`)
- Просмотр курсов, групп, преподавателей, студентов в веб-интерфейсе (Streamlit)
- Экспорт журналов успеваемости в Excel/Word (`exports.py`)
- Сбор аналитики посещаемости (`collect_analytics.py`)

## Структура проекта

- `app.py` — точка входа Streamlit-приложения
- `google_api.py` — клиент Google Classroom API
- `db.py` — инициализация и работа с локальной БД (SQLite)
- `exports.py` — генерация отчётов Excel/Word
- `collect_analytics.py` — сбор аналитики посещаемости
- `views/` — страницы Streamlit-интерфейса
- `trebovaniya.md` — требования к целевой .NET-реализации

## Установка

```bash
pip install -r requirements.txt
```

## Настройка

1. Создайте service account в Google Cloud с domain-wide delegation и
   доступом к Google Classroom API для домена Workspace.
2. Сохраните JSON-ключ service account локально (файл в `.gitignore`,
   **не коммитить**) и укажите путь через переменную окружения:
   ```bash
   export GOOGLE_APPLICATION_CREDENTIALS=path/to/service-account.json
   ```
   По умолчанию ожидается файл `dac-classroom-agent-*.json` в корне проекта.
3. Impersonation-пользователь (администратор Workspace, от имени которого
   идут запросы) задан в `google_api.py`.

## Запуск

```bash
streamlit run app.py
```

## Статус

Прототип используется как основа для анализа требований — см.
[`trebovaniya.md`](trebovaniya.md) для описания целевой архитектуры
(ASP.NET Core, Clean Architecture, Control Plane / Data Plane).
