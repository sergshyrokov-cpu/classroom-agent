import sqlite3
import os
import pandas as pd
import streamlit as st

DB_FILE = "classroom_cache.db"

def get_connection():
    """Створення з'єднання з БД та додавання підтримки кирилиці для LOWER"""
    conn = sqlite3.connect(DB_FILE, timeout=30.0)
    conn.execute("PRAGMA journal_mode=WAL;")
    conn.execute("PRAGMA synchronous=NORMAL;")
    conn.create_function("PYLOWER", 1, lambda s: str(s).lower() if s is not None else "")
    return conn

def init_db():
    """Ініціалізація структури таблиць та індексів у SQLite"""
    with get_connection() as conn:
        cursor = conn.cursor()
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS courses (
                id TEXT PRIMARY KEY,
                name TEXT,
                section TEXT,
                description_heading TEXT,
                description TEXT,
                room TEXT,
                owner_id TEXT,
                creation_time TEXT,
                update_time TEXT,
                enrollment_code TEXT,
                course_state TEXT,
                alternate_link TEXT,
                course_group_email TEXT,
                teacher_group_email TEXT,
                teacher_folder_id TEXT,
                teacher_folder_title TEXT,
                guardians_enabled INTEGER,
                calendar_id TEXT
            )
        ''')
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT
            )
        ''')
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                email TEXT,
                name TEXT
            )
        ''')
        
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_courses_state ON courses(course_state);")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_courses_owner ON courses(owner_id);")
        cursor.execute("CREATE INDEX IF NOT EXISTS idx_courses_update ON courses(update_time DESC);")

def update_last_sync_time(now_str):
    """Запис поточного часу синхронізації"""
    with get_connection() as conn:
        conn.execute('''
            INSERT INTO metadata (key, value)
            VALUES ('last_sync', ?)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value
        ''', (now_str,))
    st.cache_data.clear()

@st.cache_data(ttl=60)
def get_last_sync_time():
    """Отримання часу останньої синхронізації (кешовано)"""
    if not os.path.exists(DB_FILE):
        return None
    try:
        with get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT value FROM metadata WHERE key = 'last_sync'")
            row = cursor.fetchone()
            return row[0] if row else None
    except Exception:
        return None

def upsert_courses_batch(courses):
    """Швидке точкове оновлення/вставка масиву курсів (UPSERT)"""
    records = []
    for c in courses:
        teacher_folder = c.get('teacherFolder', {})
        records.append((
            c.get('id'),
            c.get('name'),
            c.get('section', ''),
            c.get('descriptionHeading', ''),
            c.get('description', ''),
            c.get('room', ''),
            c.get('ownerId', ''),
            c.get('creationTime', ''),
            c.get('updateTime', ''),
            c.get('enrollmentCode', ''),
            c.get('courseState', ''),
            c.get('alternateLink', ''),
            c.get('courseGroupEmail', ''),
            c.get('teacherGroupEmail', ''),
            teacher_folder.get('id', ''),
            teacher_folder.get('title', ''),
            1 if c.get('guardiansEnabled') else 0,
            c.get('calendarId', '')
        ))
    
    with get_connection() as conn:
        conn.executemany('''
            INSERT INTO courses (
                id, name, section, description_heading, description, room, owner_id,
                creation_time, update_time, enrollment_code, course_state, alternate_link,
                course_group_email, teacher_group_email, teacher_folder_id, teacher_folder_title,
                guardians_enabled, calendar_id
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                name=excluded.name,
                section=excluded.section,
                description_heading=excluded.description_heading,
                description=excluded.description,
                room=excluded.room,
                owner_id=excluded.owner_id,
                creation_time=excluded.creation_time,
                update_time=excluded.update_time,
                enrollment_code=excluded.enrollment_code,
                course_state=excluded.course_state,
                alternate_link=excluded.alternate_link,
                course_group_email=excluded.course_group_email,
                teacher_group_email=excluded.teacher_group_email,
                teacher_folder_id=excluded.teacher_folder_id,
                teacher_folder_title=excluded.teacher_folder_title,
                guardians_enabled=excluded.guardians_enabled,
                calendar_id=excluded.calendar_id
        ''', records)

def save_users_to_db(users_dict):
    """Зберігає словник користувачів у базу"""
    if not users_dict:
        return
        
    records = [(uid, data['email'], data['name']) for uid, data in users_dict.items()]
    with get_connection() as conn:
        conn.executemany('''
            INSERT INTO users (id, email, name) 
            VALUES (?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                email=excluded.email,
                name=excluded.name
        ''', records)

def get_existing_user_ids():
    """Повертає set із вже відомими ID користувачів з БД"""
    if not os.path.exists(DB_FILE):
        return set()
    with get_connection() as conn:
        cursor = conn.cursor()
        cursor.execute("SELECT id FROM users")
        return {row[0] for row in cursor.fetchall()}

@st.cache_data(ttl=60)
def get_db_stats():
    """Статистика по курсах (кешовано)"""
    if not os.path.exists(DB_FILE):
        return {'total': 0, 'active': 0, 'archived': 0, 'provisioned': 0, 'declined': 0, 'suspended': 0}
        
    try:
        with get_connection() as conn:
            df_stats = pd.read_sql_query('''
                SELECT 
                    COUNT(*) as total,
                    SUM(CASE WHEN course_state = 'ACTIVE' THEN 1 ELSE 0 END) as active,
                    SUM(CASE WHEN course_state = 'ARCHIVED' THEN 1 ELSE 0 END) as archived,
                    SUM(CASE WHEN course_state = 'PROVISIONED' THEN 1 ELSE 0 END) as provisioned,
                    SUM(CASE WHEN course_state = 'DECLINED' THEN 1 ELSE 0 END) as declined,
                    SUM(CASE WHEN course_state = 'SUSPENDED' THEN 1 ELSE 0 END) as suspended
                FROM courses
            ''', conn)
            result = df_stats.iloc[0].to_dict()
            return {k: (int(v) if pd.notnull(v) else 0) for k, v in result.items()}
    except Exception:
        return {'total': 0, 'active': 0, 'archived': 0, 'provisioned': 0, 'declined': 0, 'suspended': 0}

def search_course_suggestions(search_term: str):
    if not os.path.exists(DB_FILE):
        return []
    
    if not search_term or len(search_term.strip()) == 0:
        with get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute('''
                SELECT DISTINCT name, section, course_state
                FROM courses
                WHERE name IS NOT NULL AND name != ''
                ORDER BY update_time DESC
                LIMIT 10
            ''')
            rows = cursor.fetchall()
    else:
        term_clean = f"%{search_term.strip().lower()}%"
        with get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute('''
                SELECT DISTINCT name, section, course_state
                FROM courses
                WHERE PYLOWER(name) LIKE ? 
                   OR PYLOWER(section) LIKE ? 
                   OR PYLOWER(description_heading) LIKE ?
                ORDER BY name ASC
                LIMIT 10
            ''', (term_clean, term_clean, term_clean))
            rows = cursor.fetchall()

    suggestions = []
    for r in rows:
        name, section, state = r[0], r[1], r[2]
        section_str = f" [{section}]" if section else ""
        label = f"📚 {name}{section_str} ({state})"
        suggestions.append((label, name))
        
    return suggestions

def search_owner_suggestions(search_term: str):
    if not os.path.exists(DB_FILE):
        return []
    
    if not search_term or len(search_term.strip()) == 0:
        with get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute('''
                SELECT c.owner_id, u.name, u.email, COUNT(c.id) as cnt
                FROM courses c LEFT JOIN users u ON c.owner_id = u.id
                WHERE c.owner_id IS NOT NULL AND c.owner_id != ''
                GROUP BY c.owner_id ORDER BY cnt DESC LIMIT 10
            ''')
            rows = cursor.fetchall()
    else:
        term_clean = f"%{search_term.strip().lower()}%"
        with get_connection() as conn:
            cursor = conn.cursor()
            cursor.execute('''
                SELECT c.owner_id, u.name, u.email, COUNT(c.id) as cnt
                FROM courses c LEFT JOIN users u ON c.owner_id = u.id
                WHERE c.owner_id IS NOT NULL AND (
                    PYLOWER(u.name) LIKE ? OR PYLOWER(u.email) LIKE ? OR PYLOWER(c.owner_id) LIKE ?
                )
                GROUP BY c.owner_id ORDER BY u.name ASC LIMIT 10
            ''', (term_clean, term_clean, term_clean))
            rows = cursor.fetchall()

    suggestions = []
    for r in rows:
        owner_id, name, email, cnt = r[0], r[1], r[2], r[3]
        label = f"{name or 'Невідомо'} ({email or owner_id}) — {cnt} курсів"
        suggestions.append((label, owner_id))
        
    return suggestions

def get_courses_from_db(state_filter=None, limit=None, search_query=None, owner_filter=None):
    if not os.path.exists(DB_FILE):
        return pd.DataFrame()
        
    query = '''
        SELECT 
            c.*, 
            COALESCE(u.name, 'Невідомо') as owner_name,
            COALESCE(u.email, c.owner_id) as owner_email
        FROM courses c
        LEFT JOIN users u ON c.owner_id = u.id
        WHERE 1=1
    '''
    params = []
    
    if state_filter and state_filter != "ALL":
        query += " AND c.course_state = ?"
        params.append(state_filter)
        
    if owner_filter:
        query += " AND c.owner_id = ?"
        params.append(owner_filter)
        
    if search_query:
        clean_term = f"%{search_query.strip().lower()}%"
        query += " AND (PYLOWER(c.name) LIKE ? OR PYLOWER(c.section) LIKE ? OR PYLOWER(c.description_heading) LIKE ? OR PYLOWER(c.room) LIKE ?)"
        params.extend([clean_term, clean_term, clean_term, clean_term])
        
    query += " ORDER BY c.update_time DESC"
    
    if limit:
        query += " LIMIT ?"
        params.append(limit)
        
    with get_connection() as conn:
        df = pd.read_sql_query(query, conn, params=params)
    return df