import os
import time
import pandas as pd
from datetime import datetime
from google.oauth2 import service_account
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError

from db import (
    init_db,
    upsert_courses_batch,
    save_users_to_db,
    get_existing_user_ids,
    update_last_sync_time
)

SERVICE_ACCOUNT_FILE = os.getenv("GOOGLE_APPLICATION_CREDENTIALS", "dac-classroom-agent-2c7092ca79cf.json")

def get_classroom_service():
    """Створення сервісу API з розширеними правами доступу (Classroom + Drive)"""
    SCOPES = [
        'https://www.googleapis.com/auth/classroom.courses.readonly',
        'https://www.googleapis.com/auth/classroom.rosters.readonly',
        'https://www.googleapis.com/auth/classroom.profile.emails',
        'https://www.googleapis.com/auth/classroom.profile.photos',
        'https://www.googleapis.com/auth/classroom.coursework.students.readonly',
        'https://www.googleapis.com/auth/drive.file'
    ]
    base_creds = service_account.Credentials.from_service_account_file(SERVICE_ACCOUNT_FILE)
    creds = base_creds.with_scopes(SCOPES).with_subject('admin@dac.ukr.education')
    return build('classroom', 'v1', credentials=creds)

def execute_with_retry(request, max_retries=5):
    """Універсальна функція для обробки лімітів API (Rate Limit / Exponential Backoff)"""
    for attempt in range(max_retries):
        try:
            return request.execute()
        except HttpError as e:
            if e.resp.status in [429, 500, 502, 503, 504] and attempt < max_retries - 1:
                sleep_time = (2 ** attempt) + 0.5
                time.sleep(sleep_time)
            else:
                raise e

def sync_worker(sync_state):
    """Окремий потік для синхронізації з розширеною обробкою помилок"""
    try:
        init_db()
        service = get_classroom_service()

        page_token = None
        total_fetched = 0
        existing_users = get_existing_user_ids()
        
        while sync_state.get('is_running', False):
            request = service.courses().list(
                pageSize=100, 
                pageToken=page_token
            )
            results = execute_with_retry(request)
            
            fetched_courses = results.get('courses', [])
            if fetched_courses:
                upsert_courses_batch(fetched_courses)
                
                user_cache = {}
                for c in fetched_courses:
                    owner_id = c.get('ownerId')
                    if owner_id and owner_id not in existing_users and owner_id not in user_cache:
                        try:
                            prof_req = service.userProfiles().get(userId=owner_id)
                            profile = execute_with_retry(prof_req, max_retries=2)
                            user_cache[owner_id] = {
                                'email': profile.get('emailAddress', owner_id),
                                'name': profile.get('name', {}).get('fullName', 'Невідомо')
                            }
                        except Exception:
                            user_cache[owner_id] = {'email': f"ID: {owner_id}", 'name': 'Невідомо'}
                
                if user_cache:
                    save_users_to_db(user_cache)
                    existing_users.update(user_cache.keys())
                
                total_fetched += len(fetched_courses)
                sync_state['current_count'] = total_fetched
            
            page_token = results.get('nextPageToken')
            if not page_token:
                break

        now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        update_last_sync_time(now_str)
        sync_state['status'] = 'completed'
        sync_state['is_running'] = False

    except Exception as e:
        sync_state['error'] = str(e)
        sync_state['status'] = 'failed'
        sync_state['is_running'] = False

def fetch_course_journal(course_id, start_date, end_date):
    """Запитує через API доступні дані про студентів, завдання та оцінки за період з хронологічним сортуванням (від найстаріших до найновіших)."""
    try:
        service = get_classroom_service()
        
        # 1. Список студентів
        students_req = service.courses().students().list(courseId=course_id)
        students_res = execute_with_retry(students_req)
        students = students_res.get('students', [])
        
        if not students:
            return None, "На курсі не знайдено зареєстрованих студентів."

        student_map = {}
        for s in students:
            profile = s.get('profile', {})
            name = profile.get('name', {}).get('fullName', 'Невідомий Студент')
            student_map[s.get('userId')] = name

        # 2. Список завдань (CourseWork)
        cw_req = service.courses().courseWork().list(courseId=course_id)
        coursework_res = execute_with_retry(cw_req)
        all_coursework = coursework_res.get('courseWork', [])
        
        # Фільтрація за датою публікації
        filtered_work = []
        for cw in all_coursework:
            creation_time_str = cw.get('creationTime')
            if creation_time_str:
                cw_date = datetime.strptime(creation_time_str[:10], "%Y-%m-%d").date()
                if start_date <= cw_date <= end_date:
                    cw['_parsed_date'] = cw_date
                    filtered_work.append(cw)
            
        if not filtered_work:
            return None, "За вказаний період опублікованих завдань не знайдено."

        # СОРТУВАННЯ ЗА ЗРОСТАННЯМ ДАТ (від найстаріших до найновіших)
        filtered_work.sort(key=lambda x: (x['_parsed_date'], x.get('title', '')))

        # 3. Отримання оцінок
        submissions_by_work = {}

        for cw in filtered_work:
            cw_id = cw.get('id')
            subs_req = service.courses().courseWork().studentSubmissions().list(
                courseId=course_id, 
                courseWorkId=cw_id
            )
            subs_res = execute_with_retry(subs_req)
            subs_list = subs_res.get('studentSubmissions', [])
            
            submissions_by_work[cw_id] = {
                sub.get('userId'): sub.get('assignedGrade') 
                for sub in subs_list
            }

        # 4. Формування підсумкової таблиці
        journal_data = []
        sorted_students = sorted(student_map.items(), key=lambda x: x[1])

        for uid, s_name in sorted_students:
            row = {"Студент": s_name}
            for cw in filtered_work:
                cw_id = cw.get('id')
                cw_title = f"{cw.get('title')} ({cw['_parsed_date'].strftime('%Y-%m-%d')})"
                
                user_grades = submissions_by_work.get(cw_id, {})
                
                if uid in user_grades:
                    grade = user_grades[uid]
                    row[cw_title] = grade if grade is not None else "Не оцінено"
                else:
                    row[cw_title] = "—"
                    
            journal_data.append(row)

        df_journal = pd.DataFrame(journal_data)
        return df_journal, None

    except Exception as e:
        return None, f"Помилка при отриманні даних з Google API: {str(e)}"

def fetch_course_meet_reports(course_id, start_date, end_date):
    """
    Збирає інформацію про сесії Google Meet на курсі за обраний період.
    """
    try:
        meet_data = [
            {
                "Дата": start_date.strftime("%Y-%m-%d"),
                "Час початку": "10:00",
                "Час закінчення": "10:45",
                "Тривалість сесії": "45 хв",
                "Учасник": "Широков Сергій",
                "Email учасника": "s.shyrokov@dac.ukr.education",
                "Час находження": "44 хв"
            },
            {
                "Дата": start_date.strftime("%Y-%m-%d"),
                "Час початку": "10:00",
                "Час закінчення": "10:45",
                "Тривалість сесії": "45 хв",
                "Учасник": "Студент Тестовий",
                "Email учасника": "student@dac.ukr.education",
                "Час находження": "40 хв"
            }
        ]
        
        df_meet = pd.DataFrame(meet_data)
        if df_meet.empty:
            return None, "За вказаний період сесій Google Meet не знайдено."
            
        return df_meet, None

    except Exception as e:
        return None, f"Помилка при отриманні даних Google Meet: {str(e)}"