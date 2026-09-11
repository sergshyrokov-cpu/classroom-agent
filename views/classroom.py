import re
from datetime import date, timedelta
import streamlit as st
from streamlit_searchbox import st_searchbox

from db import (
    get_last_sync_time,
    get_db_stats,
    search_course_suggestions,
    search_owner_suggestions,
    get_courses_from_db
)
from google_api import fetch_course_journal, fetch_course_meet_reports
from exports import (
    generate_excel_bytes,
    generate_docx_bytes,
    generate_excel_template_full_bytes
)


@st.dialog("📄 Детальна інформація про курс", width="large")
def show_course_details(course_row):
    course_id = course_row.get('id')
    course_name = course_row.get('name', 'Без назви')
    teacher_name = course_row.get('owner_name') or '—'
    
    st.subheader(f"📚 {course_name}")
    
    tab_info, tab_reports = st.tabs(["ℹ️ Інформація", "📊 Reports"])

    with tab_info:
        col1, col2 = st.columns(2)
        with col1:
            st.write(f"**ID курсу:** `{course_id}`")
            st.write(f"**Розділ (Section):** {course_row.get('section') or '—'}")
            st.write(f"**Статус:** `{course_row.get('course_state')}`")
            st.write(f"**Код приєднання:** `{course_row.get('enrollment_code') or '—'}`")
            st.write(f"**Аудиторія (Room):** {course_row.get('room') or '—'}")
            st.write(f"**Опікуни увімкнені:** {'Так' if course_row.get('guardians_enabled') == 1 else 'Ні'}")
            
        with col2:
            owner_str = f"{teacher_name} ({course_row.get('owner_email')})"
            st.write(f"**Викладач:** {owner_str}")
            st.write(f"**ID Власника:** `{course_row.get('owner_id')}`")
            st.write(f"**Час створення:** {course_row.get('creation_time') or '—'}")
            st.write(f"**Останнє оновлення:** {course_row.get('update_time') or '—'}")
            st.write(f"**ID Календаря:** `{course_row.get('calendar_id') or '—'}`")

        st.divider()
        st.markdown("### 📝 Опис")
        if course_row.get('description_heading'):
            st.markdown(f"**Заголовок опису:** {course_row.get('description_heading')}")
        st.info(course_row.get('description') or "Опис відсутній.")

        st.divider()
        st.markdown("### 📁 Ресурси та Посилання")
        col3, col4 = st.columns(2)
        with col3:
            if course_row.get('alternate_link'):
                st.link_button("🔗 Відкрити курс у Google Classroom", course_row.get('alternate_link'), use_container_width=True)
        with col4:
            folder_id = course_row.get('teacher_folder_id')
            if folder_id:
                drive_url = f"https://drive.google.com/drive/folders/{folder_id}"
                st.link_button("📂 Папка викладача на Google Drive", drive_url, use_container_width=True)

    with tab_reports:
        report_type = st.selectbox("Оберіть звіт:", ["📖 Журнал", "📹 Meet"], key="report_type_select")
        
        if report_type == "📖 Журнал":
            st.markdown("#### 📅 Налаштування періоду звіту")
            col_d1, col_d2 = st.columns(2)
            with col_d1:
                start_d = st.date_input("Початкова дата", value=date(2025, 9, 1), key="journal_start_date")
            with col_d2:
                end_d = st.date_input("Кінцева дата", value=date.today(), key="journal_end_date")
            
            session_journal_key = f"journal_df_{course_id}"

            col_btn1, col_btn2 = st.columns([3, 1])
            with col_btn1:
                if st.button("🚀 Згенерувати журнал", type="primary", use_container_width=True):
                    if start_d > end_d:
                        st.error("Помилка: Початкова дата не може бути пізнішою за кінцеву!")
                    else:
                        with st.spinner("Завантаження даних з Google Classroom API..."):
                            df_j, err = fetch_course_journal(course_id, start_d, end_d)
                            
                        if err:
                            st.warning(f"⚠️ {err}")
                            st.session_state[session_journal_key] = None
                        elif df_j is not None and not df_j.empty:
                            st.session_state[session_journal_key] = df_j

            if session_journal_key in st.session_state and st.session_state[session_journal_key] is not None:
                df_j = st.session_state[session_journal_key]

                with col_btn2:
                    if st.button("❌ Згорнути", use_container_width=True):
                        st.session_state[session_session_key if 'session_session_key' in locals() else session_journal_key] = None
                        st.rerun()

                # Підготовка даних із df_j
                students_list = df_j["Студент"].tolist() if "Студент" in df_j.columns else []
                topic_cols = [c for c in df_j.columns if c != "Студент"]

                st.success(f"✅ Журнал згенеровано! Отримано студентів: {len(students_list)}, занять/тем: {len(topic_cols)}")
                st.dataframe(df_j, use_container_width=True, hide_index=True)
                
                st.divider()
                st.markdown("#### 📥 Завантажити звіт")
                
                export_format = st.selectbox(
                    "Оберіть форму звіту для завантаження:",
                    [
                        "Шаблон Excel (XLSX)",
                        "Лист Excel (XLSX)",
                        "Документ Word (DOCX)",
                        "Документ CSV"
                    ],
                    key=f"export_format_{course_id}"
                )
                
                topics_data = [{"title": col} for col in topic_cols]
                grades_dict = {}
                for _, row in df_j.iterrows():
                    st_name = row.get("Студент")
                    for col in topic_cols:
                        grades_dict[(st_name, col)] = row.get(col)

                journal_struct = {
                    "students": students_list,
                    "topics": topics_data,
                    "grades": grades_dict
                }

                if export_format == "Шаблон Excel (XLSX)":
                    try:
                        bytes_out = generate_excel_template_full_bytes(
                            course_name=course_name,
                            teacher_name=teacher_name,
                            journal_struct=journal_struct
                        )
                        st.download_button(
                            "⬇️ Завантажити Шаблон Excel",
                            bytes_out,
                            f"Звіт_{course_name}.xlsx",
                            mime="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            type="primary"
                        )
                    except Exception as e:
                        st.error(f"Помилка при формуванні шаблону: {e}")

                elif export_format == "Лист Excel (XLSX)":
                    st.download_button(
                        "⬇️ Завантажити Excel",
                        generate_excel_bytes(df_j, "Журнал"),
                        f"journal_{course_id}.xlsx",
                        type="primary"
                    )

                elif export_format == "Документ Word (DOCX)":
                    st.download_button(
                        "⬇️ Завантажити Word",
                        generate_docx_bytes(df_j, f"Журнал: {course_name}"),
                        f"journal_{course_id}.docx",
                        type="primary"
                    )

                elif export_format == "Документ CSV":
                    st.download_button(
                        "⬇️ Завантажити CSV",
                        df_j.to_csv(index=False).encode('utf-8-sig'),
                        f"journal_{course_id}.csv",
                        mime="text/csv",
                        type="primary"
                    )

        elif report_type == "📹 Meet":
            st.markdown("#### 📅 Налаштування періоду звіту")
            st.caption("ℹ️ *Зверніть увагу: за вимогами Google Admin SDK, звіти Meet зберігаються лише за останні 180 днів.*")

            min_meet_date = date.today() - timedelta(days=179)
            default_start_meet = max(min_meet_date, date(date.today().year, date.today().month, 1))

            col_m1, col_m2 = st.columns(2)
            with col_m1:
                start_m = st.date_input(
                    "Початкова дата", 
                    value=default_start_meet, 
                    min_value=min_meet_date,
                    max_value=date.today(),
                    key="meet_start_date"
                )
            with col_m2:
                end_m = st.date_input(
                    "Кінцева дата", 
                    value=date.today(), 
                    min_value=min_meet_date,
                    max_value=date.today(),
                    key="meet_end_date"
                )

            session_meet_key = f"meet_df_{course_id}"

            col_mbtn1, col_mbtn2 = st.columns([3, 1])
            with col_mbtn1:
                if st.button("🚀 Згенерувати звіт Meet", type="primary", use_container_width=True):
                    if start_m > end_m:
                        st.error("Помилка: Початкова дата не може бути пізнішою за кінцеву!")
                    else:
                        with st.spinner("Завантаження даних Meet..."):
                            df_m, err_m = fetch_course_meet_reports(course_id, start_m, end_m)

                        if err_m:
                            st.warning(f"⚠️ {err_m}")
                            st.session_state[session_meet_key] = None
                        elif df_m is not None and not df_m.empty:
                            st.session_state[session_meet_key] = df_m
                        else:
                            st.info("За вказаний період подій Meet не знайдено.")
                            st.session_state[session_meet_key] = None

            if session_meet_key in st.session_state and st.session_state[session_meet_key] is not None:
                df_m = st.session_state[session_meet_key]

                with col_mbtn2:
                    if st.button("❌ Згорнути", use_container_width=True):
                        st.session_state[session_meet_key] = None
                        st.rerun()

                st.success(f"✅ Звіт Google Meet згенеровано! Знайдено записів: {len(df_m)}")
                st.dataframe(df_m, use_container_width=True, hide_index=True)

                st.divider()
                st.markdown("#### 📥 Завантажити звіт Meet")
                export_meet_format = st.selectbox(
                    "Оберіть формат:", 
                    ["Лист Excel (XLSX)", "Документ Word (DOCX)", "Документ CSV"], 
                    key=f"export_meet_{course_id}"
                )

                if export_meet_format == "Лист Excel (XLSX)":
                    st.download_button("⬇️ Завантажити Excel", generate_excel_bytes(df_m, "Meet"), f"meet_{course_id}.xlsx", type="primary")
                elif export_meet_format == "Документ Word (DOCX)":
                    st.download_button("⬇️ Завантажити Word", generate_docx_bytes(df_m, f"Meet: {course_name}"), f"meet_{course_id}.docx", type="primary")
                elif export_meet_format == "Документ CSV":
                    st.download_button("⬇️ Завантажити CSV", df_m.to_csv(index=False).encode('utf-8-sig'), f"meet_{course_id}.csv", mime="text/csv", type="primary")


def render_courses_page():
    st.title("🎓 Google Classroom — Курси")
    stats = get_db_stats()
    last_sync = get_last_sync_time()
    total_courses = stats.get('total', 0)

    if not last_sync or total_courses == 0:
        st.warning("⚠️ **Локальна база порожня.** Перейдіть у розділ **Управління БД** та запустіть синхронізацію.")
    else:
        st.success(f"✅ Локальну базу даних підключено ({total_courses} курсів). Актуальна на **{last_sync}**.")

    st.divider()
    st.subheader("🔍 Пошук та фільтрація курсів")

    status_options = {
        f"ALL ({stats.get('total', 0)})": "ALL",
        f"ACTIVE ({stats.get('active', 0)})": "ACTIVE",
        f"ARCHIVED ({stats.get('archived', 0)})": "ARCHIVED",
        f"PROVISIONED ({stats.get('provisioned', 0)})": "PROVISIONED",
        f"DECLINED ({stats.get('declined', 0)})": "DECLINED",
        f"SUSPENDED ({stats.get('suspended', 0)})": "SUSPENDED"
    }
    options_list = list(status_options.keys())

    if "selected_status_label" not in st.session_state or st.session_state.selected_status_label not in options_list:
        st.session_state.selected_status_label = options_list[0]
        
    if "limit_option" not in st.session_state:
        st.session_state.limit_option = 50

    if st.session_state.get("reset_trigger", False):
        st.session_state.selected_status_label = options_list[0]
        st.session_state.limit_option = 50
        st.session_state.search_course_key = None
        st.session_state.search_owner_key = None
        st.session_state.reset_trigger = False

    col_s1, col_s2 = st.columns(2)
    with col_s1:
        selected_course_name = st_searchbox(search_course_suggestions, key="search_course_key", placeholder="🔎 Пошук за назвою...")
    with col_s2:
        selected_owner_id = st_searchbox(search_owner_suggestions, key="search_owner_key", placeholder="👤 Пошук за викладачем...")

    col_f1, col_f2, col_f3 = st.columns([2, 1, 1])
    with col_f1:
        st.session_state.selected_status_label = st.selectbox("Статус курсу:", options_list, index=options_list.index(st.session_state.selected_status_label))
    with col_f2:
        st.session_state.limit_option = st.selectbox("Ліміт:", [25, 50, 100, 200, "Всі"], index=1)
    with col_f3:
        st.write(""); st.write("")
        if st.button("🔄 Скинути фільтри", use_container_width=True):
            st.session_state.reset_trigger = True
            st.rerun()

    state_code = status_options[st.session_state.selected_status_label]
    limit_val = None if st.session_state.limit_option == "Всі" else st.session_state.limit_option

    df_courses = get_courses_from_db(
        state_filter=state_code,
        limit=limit_val,
        search_query=selected_course_name,
        owner_filter=selected_owner_id
    )

    st.markdown(f"**Знайдено курсів:** `{len(df_courses)}`")

    if not df_courses.empty:
        display_df = df_courses[['name', 'section', 'course_state', 'owner_name', 'owner_email', 'update_time']].copy()
        display_df.columns = ['Назва курсу', 'Розділ', 'Статус', 'Викладач', 'Email викладача', 'Останнє оновлення']
        
        event = st.dataframe(display_df, use_container_width=True, hide_index=True, on_select="rerun", selection_mode="single-row")

        selected_rows = event.selection.rows if event and hasattr(event, 'selection') else []
        if selected_rows:
            idx = selected_rows[0]
            show_course_details(df_courses.iloc[idx].to_dict())
    else:
        st.info("Курсів за вашим запитом не знайдено.")


def render_placeholder_page(title):
    st.title(f"🎓 Google Classroom — {title}")
    st.info(f"Розділ **{title}** знаходиться в розробці.")