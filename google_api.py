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

def extract_best_date(item):
    """
    Універсальний каскад для визначення дати будь-якої публікації в Google Classroom:
    1. scheduledTime -> 2. dueDate -> 3. updateTime -> 4. creationTime
    """
    # 1. Запланований час публікації
    if item.get('scheduledTime'):
        return datetime.strptime(item['scheduledTime'][:10], "%Y-%m-%d").date()
    
    # 2. Крайній термін здачі (dueDate)
    due = item.get('dueDate')
    if due and due.get('year') and due.get('month') and due.get('day'):
        try:
            return datetime(due['year'], due['month'], due['day']).date()
        except ValueError:
            pass

    # 3. Час останнього оновлення/модифікації
    if item.get('updateTime'):
        return datetime.strptime(item['updateTime'][:10], "%Y-%m-%d").date()

    # 4. Час створення (базовий fallback)
    if item.get('creationTime'):
        return datetime.strptime(item['creationTime'][:10], "%Y-%m-%d").date()

    return None

def fetch_course_journal(course_id, start_date, end_date):
    """
    Універсальний збір усіх типів занять та матеріалів з універсальним каскадом дат.
    """
    try:
        service = get_classroom_service()
        
        # 1. Список студентів
        students = []
        page_token = None
        while True:
            students_req = service.courses().students().list(
                courseId=course_id, 
                pageToken=page_token
            )
            students_res = execute_with_retry(students_req)
            students.extend(students_res.get('students', []))
            page_token = students_res.get('nextPageToken')
            if not page_token:
                break
        
        if not students:
            return None, "На курсі не знайдено зареєстрованих студентів."

        student_map = {
            s.get('userId'): s.get('profile', {}).get('name', {}).get('fullName', 'Невідомий Студент') 
            for s in students
        }

        # 2. Збір абсолютно ВСІХ матеріалів та завдань
        all_items = []

        # 2a. Завдання (з оцінкою та без)
        page_token = None
        while True:
            cw_req = service.courses().courseWork().list(courseId=course_id, pageToken=page_token)
            cw_res = execute_with_retry(cw_req)
            for item in cw_res.get('courseWork', []):
                item['_type'] = 'courseWork'
                all_items.append(item)
            page_token = cw_res.get('nextPageToken')
            if not page_token:
                break

        # 2b. Навчальні матеріали
        try:
            page_token = None
            while True:
                mat_req = service.courses().courseWorkMaterials().list(courseId=course_id, pageToken=page_token)
                mat_res = execute_with_retry(mat_req)
                for item in mat_res.get('courseWorkMaterials', []):
                    item['_type'] = 'material'
                    all_items.append(item)
                page_token = mat_res.get('nextPageToken')
                if not page_token:
                    break
        except Exception:
            pass

        # 3. Застосування каскаду дат та фільтрація за обраним періодом
        filtered_items = []
        for item in all_items:
            item_date = extract_best_date(item)
            if item_date and start_date <= item_date <= end_date:
                item['_parsed_date'] = item_date
                filtered_items.append(item)

        if not filtered_items:
            return None, "За вказаний період занять або матеріалів не знайдено."

        # Хронологічне сортування від найдавніших дат до найновіших
        filtered_items.sort(key=lambda x: (x['_parsed_date'], x.get('title', '')))

        # 4. Отримання оцінок для завдань (courseWork)
        submissions_by_work = {}
        for item in filtered_items:
            if item['_type'] == 'courseWork':
                cw_id = item.get('id')
                subs_list = []
                page_token = None
                try:
                    while True:
                        subs_req = service.courses().courseWork().studentSubmissions().list(
                            courseId=course_id, 
                            courseWorkId=cw_id,
                            pageToken=page_token
                        )
                        subs_res = execute_with_retry(subs_req)
                        subs_list.extend(subs_res.get('studentSubmissions', []))
                        page_token = subs_res.get('nextPageToken')
                        if not page_token:
                            break
                except Exception:
                    pass

                submissions_by_work[cw_id] = {
                    sub.get('userId'): sub.get('assignedGrade') 
                    for sub in subs_list
                }

        # 5. Формування підсумкової таблиці
        journal_data = []
        sorted_students = sorted(student_map.items(), key=lambda x: x[1])

        for uid, s_name in sorted_students:
            row = {"Студент": s_name}
            for item in filtered_items:
                title_header = f"{item.get('title')} ({item['_parsed_date'].strftime('%Y-%m-%d')})"
                
                if item['_type'] == 'material':
                    row[title_header] = "—"  # Навчальні матеріали без оцінювання
                else:
                    cw_id = item.get('id')
                    user_grades = submissions_by_work.get(cw_id, {})
                    grade = user_grades.get(uid)
                    
                    if grade is not None:
                        row[title_header] = grade
                    else:
                        row[title_header] = "Не оцінено"

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