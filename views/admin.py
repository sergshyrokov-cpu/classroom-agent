import time
import threading
import streamlit as st
from db import get_last_sync_time, get_db_stats
from google_api import sync_worker

def render_admin_page():
    st.title("⚙️ Управління базою даних")
    st.markdown("---")
    
    stats = get_db_stats()
    last_sync = get_last_sync_time()
    total_courses = stats.get('total', 0)
    
    col_info1, col_info2 = st.columns(2)
    with col_info1:
        st.info(f"🕒 **Остання синхронізація:**\n\n`{last_sync if last_sync else 'Ніколи'}`")
    with col_info2:
        st.success(f"📊 **Всього курсів у БД:**\n\n`{total_courses}`")

    st.markdown("### 🔄 Оновлення даних")
    st.write("Синхронізація підтягує актуальні дані про курси та профілі викладачів з Google Classroom API.")

    sync_st = st.session_state.sync_state

    if not sync_st['is_running']:
        with st.popover("🚀 Синхронізувати базу", use_container_width=False):
            st.markdown("### ⚠️ Підтвердження")
            st.write("У вашому домені знаходиться понад **7 200 курсів**.")
            
            if st.button("Так, розпочати оновлення", type="primary", use_container_width=True):
                sync_st['is_running'] = True
                sync_st['current_count'] = 0
                sync_st['status'] = 'running'
                sync_st['error'] = None
                
                t = threading.Thread(target=sync_worker, args=(sync_st,), daemon=True)
                t.start()
                st.rerun()

    if sync_st['is_running']:
        st.subheader("⏳ Йде процес фонової синхронізації...")
        
        ESTIMATED_TOTAL = 7500
        cnt = sync_st['current_count']
        percent = min(int((cnt / ESTIMATED_TOTAL) * 100), 100)
        
        st.progress(percent)
        st.info(f"Завантажено курсів: **{cnt}**...")

        col_stop, _ = st.columns([1, 4])
        with col_stop:
            if st.button("⏹ Зупинити", type="secondary"):
                sync_st['is_running'] = False
                sync_st['status'] = 'stopped'
                st.warning("Синхронізацію перервано користувачем.")
                st.rerun()

        time.sleep(1)
        st.rerun()

    else:
        if sync_st['status'] == 'completed':
            st.success("🎉 Успішно оновлено всі дані курсів!")
            sync_st['status'] = 'idle'
        elif sync_st['status'] == 'failed':
            st.error(f"❌ Помилка синхронізації: {sync_st['error']}")
            sync_st['status'] = 'idle'