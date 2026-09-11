import os
from google.oauth2 import service_account
from googleapiclient.discovery import build

# 1. Указываем путь к нашему файлу с ключами
KEY_FILE = 'dac-classroom-agent-2c7092ca79cf.json'

# 2. Указываем email администратора домена (твой аккаунт)
# Под этим пользователем скрипт будет совершать запросы
ADMIN_EMAIL = 'admin@dac.ukr.education'

# 3. Набор разрешений (scopes), которые мы настроили в админке
SCOPES = [
    'https://www.googleapis.com/auth/classroom.courses.readonly',
    'https://www.googleapis.com/auth/classroom.rosters.readonly'
]

def main():
    print("Пробуем авторизоваться в Google Workspace...")
    
    # Загружаем ключи сервисного аккаунта и настраиваем делегирование (subject)
    creds = service_account.Credentials.from_service_account_file(
        KEY_FILE, 
        scopes=SCOPES
    ).with_subject(ADMIN_EMAIL)
    
    # Строим клиент для работы с API Google Classroom
    service = build('classroom', 'v1', credentials=creds)
    
    print("Авторизация успешна! Запрашиваем список курсов...")
    
    try:
        # Делаем тестовый запрос: получаем список первых 10 курсов в домене
        results = service.courses().list(pageSize=10).execute()
        courses = results.get('courses', [])
        
        if not courses:
            print("Связь установлена, но курсы в Google Classroom не найдены.")
        else:
            print(f"\nУра! Успешно найдено курсов: {len(courses)}")
            print("-" * 50)
            for course in courses:
                print(f" Название: {course['name']}")
                print(f" ID курса: {course['id']}")
                print(f" Ссылка: {course.get('alternateLink')}")
                print("-" * 50)
                
    except Exception as e:
        print("\nПроизошла ошибка при обращении к API Google Classroom:")
        print(e)

if __name__ == '__main__':
    main()