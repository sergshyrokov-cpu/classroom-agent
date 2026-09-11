import streamlit as st
from db import init_db
from views.admin import render_admin_page
from views.classroom import render_courses_page, render_placeholder_page

st.set_page_config(
    page_title="DAC AI Assistant",
    page_icon="🎓",
    layout="wide"
)

# Ініціалізація БД при запуску
init_db()

if "sync_state" not in st.session_state:
    st.session_state.sync_state = {
        'is_running': False,
        'current_count': 0,
        'status': 'idle',
        'error': None
    }

# SideBar Меню
with st.sidebar:
    st.title("🤖 DAC AI Assistant")
    st.markdown("---")
    
    main_section = st.radio(
        "Розділ:",
        ["Google Classroom", "Управління БД", "Контакти"],
        key="main_menu_section"
    )
    
    sub_section = None
    if main_section == "Google Classroom":
        sub_section = st.radio(
            "Сутність:",
            ["Курси", "Групи", "Викладачі", "Студенти"],
            key="gc_sub_section"
        )

    if main_section == "Контакти":
        st.markdown("---")
        st.caption("📱 +380677454343")
        st.caption("✉️ serg.shyrokov@gmail.com")
        st.caption("💬 @SerhiiSHYROKOV")

# Маршрутизація (Routing)
if main_section == "Управління БД":
    render_admin_page()

elif main_section == "Google Classroom":
    if sub_section == "Курси":
        render_courses_page()
    else:
        render_placeholder_page(sub_section)