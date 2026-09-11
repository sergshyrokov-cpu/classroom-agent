import os
from google.oauth2 import service_account
from googleapiclient.discovery import build
from google.auth.transport.requests import Request

SERVICE_ACCOUNT_FILE = "dac-classroom-agent-2c7092ca79cf.json"
ADMIN_EMAIL = "admin@dac.ukr.education"
SCOPES = ['https://www.googleapis.com/auth/admin.reports.audit.readonly']

try:
    base_creds = service_account.Credentials.from_service_account_file(
        SERVICE_ACCOUNT_FILE, 
        scopes=SCOPES
    )
    creds = base_creds.with_subject(ADMIN_EMAIL)
    
    if not creds.valid:
        creds.refresh(Request())
        
    service = build('admin', 'reports_v1', credentials=creds)
    
    # Робимо тестовий виклик
    res = service.activities().list(
        userKey='all', 
        applicationName='meet', 
        maxResults=1
    ).execute()
    
    print("✅ УСПІХ! Доступ є. Записів знайдено:", len(res.get('items', [])))

except Exception as e:
    print("❌ ПОМИЛКА:", e)