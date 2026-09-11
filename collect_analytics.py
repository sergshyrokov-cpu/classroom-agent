import os
import pandas as pd
from google.oauth2 import service_account
from googleapiclient.discovery import build

# Твой файл с ключами (мы выяснили, что он переименован)
KEY_FILE = 'dac-classroom-agent-2c7092ca79cf.json'  
ADMIN_EMAIL = 'admin@dac.ukr.education'

# Расширенные доступы (Scopes) для сбора тем, заданий и материалов
# Оставляем только те 2 доступа, которые прошли проверку в test_agent.py
SCOPES = [
    'https://www.googleapis.com/auth/classroom.courses.readonly',
    'https://www.googleapis.com/auth/classroom.rosters.readonly'
]

def get_teacher_name(service, teacher_id):
    """Получает имя преподавателя по его ID"""
    try:
        profile = service.userProfiles().get(userId=teacher_id).execute()
        return profile.get('name', {}).get('fullName', 'Неизвестно')
    except Exception:
        return f"ID: {teacher_id}"

def main():
    print("Авторизация в Google Workspace...")
    creds = service_account.Credentials.from_service_account_file(
        KEY_FILE, scopes=SCOPES
    ).with_subject(ADMIN_EMAIL)
    
    service = build('classroom', 'v1', credentials=creds)
    
    print("Запрос списка курсов...")
    courses_result = service.courses().list(pageSize=20).execute()
    courses = courses_result.get('courses', [])
    
    if not courses:
        print("Курсы не найдены.")
        return

    report_data = []
    print(f"Найдено курсов: {len(courses)}. Начинаем детальный сбор статистики...\n")

    for idx, course in enumerate(courses, 1):
        course_id = course['id']
        course_name = course['name']
        owner_id = course['ownerId']
        
        print(f"[{idx}/{len(courses)}] Анализируем: {course_name}...")
        
        # 1. Получаем имя преподавателя
        teacher_name = get_teacher_name(service, owner_id)
        
        # 2. Считаем темы (Topics)
        try:
            topics_res = service.courses().topics().list(courseId=course_id).execute()
            topics_count = len(topics_res.get('topic', []))
        except Exception:
            topics_count = 0
            
        # 3. Считаем задания (CourseWork - тесты, домашние работы)
        try:
            cw_res = service.courses().courseWork().list(courseId=course_id).execute()
            coursework_count = len(cw_res.get('courseWork', []))
        except Exception:
            coursework_count = 0
            
        # 4. Считаем материалы (CourseWorkMaterials - лекции, презентации, ссылки)
        try:
            mat_res = service.courses().courseWorkMaterials().list(courseId=course_id).execute()
            materials_count = len(mat_res.get('courseWorkMaterial', []))
        except Exception:
            materials_count = 0
            
        report_data.append({
            'Название курса': course_name,
            'ID курса': course_id,
            'Преподаватель': teacher_name,
            'Кол-во Тем': topics_count,
            'Кол-во Заданий': coursework_count,
            'Кол-во Материалов': materials_count,
            'Ссылка': course.get('alternateLink', '')
        })

    # Сохраняем собранные данные в красивый Excel
    df = pd.DataFrame(report_data)
    output_file = 'classroom_activity_report.xlsx'
    df.to_excel(output_file, index=False)
    
    print(f"\n Наш агент закончил сбор! Файл сохранен тут: {os.path.abspath(output_file)}")

if __name__ == '__main__':
    main()