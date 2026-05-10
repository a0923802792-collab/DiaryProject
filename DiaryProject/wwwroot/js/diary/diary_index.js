/* ============================================================
       狀態變數
       ============================================================ */
let diaries = [];
let selectedCards = new Set();
let currentPage = 1;
const perPage = 10;
let filteredDiaries = [];

const SYSTEM_TAG_ORDER = ['工作', '美食', '家庭', '旅遊', '感情'];

const filterState = {
    query: '',
    time: 'all',
    rangeStart: '',
    rangeEnd: '',
    tags: [],
    template: 'all',
    share: 'all',
    sort: 'desc'
};

/* ============================================================
   工具函式
   ============================================================ */

function parseDiaryDate(dateText) {
    const text = String(dateText || '').trim();
    let m = /([0-9]{4})年([0-9]{1,2})月([0-9]{1,2})日/.exec(text);
    if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    m = /^([0-9]{4})-([0-9]{1,2})-([0-9]{1,2})$/.exec(text);
    if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    return null;
}

function getSortTimestamp(entry) {
    const datePart = parseDiaryDate(entry.date);
    if (!datePart) return 0;
    const timeRaw = typeof entry.time === 'string' ? entry.time.trim() : '';
    const tm = /^([01]?\d|2[0-3]):([0-5]\d)$/.exec(timeRaw);
    const hour = tm ? Number(tm[1]) : 0;
    const minute = tm ? Number(tm[2]) : 0;
    return new Date(
        datePart.getFullYear(), datePart.getMonth(), datePart.getDate(),
        hour, minute, 0, 0
    ).getTime();
}

function getWeekStart(date) {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const day = d.getDay();
    const offset = day === 0 ? 6 : day - 1;
    d.setDate(d.getDate() - offset);
    d.setHours(0, 0, 0, 0);
    return d;
}

function getWeekEnd(weekStart) {
    const end = new Date(weekStart);
    end.setDate(end.getDate() + 6);
    end.setHours(23, 59, 59, 999);
    return end;
}

function parseMoodPreview(entry) {
    const text = (entry?.body || entry?.excerpt || '').trim();
    const mEmotions = text.match(/情緒：([^｜\n]+)/);
    const emotions = mEmotions?.[1]
        ? mEmotions[1].split('、').map(s => s.trim()).filter(Boolean)
        : [];

    const mEvent = text.match(/有什麼小事發生：([^\n]*)/);
    const mThought = text.match(/第一個想法：([^\n]*)/);
    const mNeed = text.match(/我需要：([^\n]*)/);
    const notes = [mEvent?.[1], mThought?.[1], mNeed?.[1]]
        .map(v => (v || '').trim())
        .filter(Boolean)
        .join(' ');

    const fallbackSummary = (entry?.excerpt || entry?.body || '（尚無內容）').trim();
    return {
        emotions: emotions,
        summary: notes || fallbackSummary
    };
}

/* ============================================================
   標籤篩選
   ============================================================ */

function refreshTagFilterOptions() {
    const menu = document.getElementById('tagFilterMenu');
    const btn = document.getElementById('tagFilterBtn');
    const tagsFromDiaries = Array.from(new Set(
        diaries.flatMap(d => Array.isArray(d.tags) ? d.tags : [])
    ));
    const customTags = tagsFromDiaries
        .filter(tag => !SYSTEM_TAG_ORDER.includes(tag))
        .sort((a, b) => a.localeCompare(b, 'zh-Hant'));
    const merged = [...SYSTEM_TAG_ORDER, ...customTags];

    filterState.tags = filterState.tags.filter(tag => merged.includes(tag));

    menu.innerHTML = '';
    merged.forEach(tag => {
        const row = document.createElement('label');
        row.className = 'filter-menu-item';
        row.innerHTML = `
          <input type="checkbox" value="${tag}" ${filterState.tags.includes(tag) ? 'checked' : ''}>
          <span>${tag}</span>`;
        row.querySelector('input').addEventListener('change', applyTagSelection);
        menu.appendChild(row);
    });
    updateTagFilterButtonText(btn);
}

function updateTagFilterButtonText(btn) {
    if (filterState.tags.length === 0) {
        btn.textContent = '全部 ';
    } else if (filterState.tags.length === 1) {
        btn.textContent = `${filterState.tags[0]} `;
    } else {
        btn.textContent = `已選 ${filterState.tags.length} 項 `;
    }
}

function applyTagSelection() {
    filterState.tags = Array.from(
        document.querySelectorAll('#tagFilterMenu input[type="checkbox"]:checked')
    ).map(el => el.value);
    updateTagFilterButtonText(document.getElementById('tagFilterBtn'));
    applyFilters(true);
}

function toggleTagMenu() {
    document.getElementById('tagFilterWrap').classList.toggle('open');
}

/* ============================================================
   單選篩選下拉選單（模板 / 分享狀態 / 排序）
   ============================================================ */

function getSingleFilterWrap(type) {
    return document.getElementById(`${type}FilterWrap`);
}

function toggleSingleFilterMenu(type) {
    const targetWrap = getSingleFilterWrap(type);
    document.querySelectorAll('.filter-multi.open').forEach(wrap => {
        if (wrap !== targetWrap) wrap.classList.remove('open');
    });
    targetWrap?.classList.toggle('open');
}

function setSingleFilter(type, value, label, resetPage = true) {
    filterState[type] = value;
    document.getElementById(`${type}FilterBtn`).textContent = label;

    const wrap = getSingleFilterWrap(type);
    wrap?.querySelectorAll('.filter-menu-item').forEach(item => {
        item.classList.toggle('active', item.dataset.value === value);
    });
    wrap?.classList.remove('open');

    applyFilters(resetPage);
}

/* ============================================================
   資料來源 & 統計
   ============================================================ */

function refreshSource(resetPage) {
    diaries = Array.isArray(window.diaryEntries) ? window.diaryEntries.slice() : [];
    refreshTagFilterOptions();
    renderSummaryCounts();
    if (resetPage) currentPage = 1;
    applyFilters(false);
}

function renderSummaryCounts() {
    const normal = diaries.filter(d => d.type !== 'mood').length;
    const mood = diaries.filter(d => d.type === 'mood').length;
    document.getElementById('totalCount').textContent = diaries.length;
    document.getElementById('normalCount').textContent = normal;
    document.getElementById('moodCount').textContent = mood;
}

/* ============================================================
   篩選 & 排序
   ============================================================ */

function matchTimeFilter(entryDate) {
    if (!entryDate) return filterState.time === 'all';
    const now = new Date();
    if (filterState.time === 'all') return true;
    if (filterState.time === 'week') {
        const s = getWeekStart(now);
        return entryDate >= s && entryDate <= getWeekEnd(s);
    }
    if (filterState.time === 'month') {
        return entryDate.getFullYear() === now.getFullYear()
            && entryDate.getMonth() === now.getMonth();
    }
    if (filterState.time === 'range') {
        if (!filterState.rangeStart || !filterState.rangeEnd) return true;
        const s = new Date(filterState.rangeStart + 'T00:00:00');
        const e = new Date(filterState.rangeEnd + 'T23:59:59');
        return entryDate >= s && entryDate <= e;
    }
    return true;
}

function applyFilters(resetPage = true) {
    const q = filterState.query;
    filteredDiaries = diaries.filter(d => {
        const entryDate = parseDiaryDate(d.date);

        const matchesQuery = !q
            || (d.title || '').toLowerCase().includes(q)
            || (d.excerpt || '').toLowerCase().includes(q)
            || (d.body || '').toLowerCase().includes(q)
            || (d.tags || []).some(t => (t || '').toLowerCase().includes(q))
            || (d.date || '').includes(q);

        const matchesTime = matchTimeFilter(entryDate);
        const matchesTag = filterState.tags.length === 0
            || filterState.tags.some(tag => (d.tags || []).includes(tag));
        const matchesTemplate = filterState.template === 'all'
            || d.type === filterState.template;
        const matchesShare = filterState.share === 'all'
            || (filterState.share === 'shared' && !!d.shared)
            || (filterState.share === 'private' && !d.shared);

        return matchesQuery && matchesTime && matchesTag && matchesTemplate && matchesShare;
    });

    filteredDiaries.sort((a, b) => {
        const ta = getSortTimestamp(a), tb = getSortTimestamp(b);
        if (ta === tb) return filterState.sort === 'asc' ? a.id - b.id : b.id - a.id;
        return filterState.sort === 'asc' ? ta - tb : tb - ta;
    });

    selectedCards = new Set(
        Array.from(selectedCards).filter(id => filteredDiaries.some(d => d.id === id))
    );

    if (resetPage) currentPage = 1;
    const total = Math.max(1, Math.ceil(filteredDiaries.length / perPage));
    if (currentPage > total) currentPage = total;
    renderGrid();
}

/* ============================================================
   搜尋
   ============================================================ */

function runSearch() {
    filterState.query = document.getElementById('searchInput').value.trim().toLowerCase();
    applyFilters(true);
}

function clearSearchWhenEmpty(e) {
    if (e.target.value.trim() === '' && filterState.query !== '') {
        filterState.query = '';
        applyFilters(true);
    }
}

function handleSearch(e) {
    if (e.key === 'Enter') runSearch();
}

/* ============================================================
   渲染：格線 + 分頁
   ============================================================ */

function renderGrid() {
    const grid = document.getElementById('diaryGrid');
    const emptyState = document.getElementById('emptyState');
    const noResult = document.getElementById('noResult');
    const pagination = document.getElementById('pagination');

    if (diaries.length === 0) {
        selectedCards.clear();
        grid.innerHTML = '';
        noResult.style.display = 'none';
        emptyState.style.display = 'block';
        pagination.innerHTML = '';
        updateBatchBar();
        return;
    }

    emptyState.style.display = 'none';
    const start = (currentPage - 1) * perPage;
    const pageItems = filteredDiaries.slice(start, start + perPage);

    if (filteredDiaries.length === 0) {
        grid.innerHTML = '';
        noResult.style.display = 'block';
    } else {
        noResult.style.display = 'none';
        grid.innerHTML = pageItems.map(d => `
          <div class="diary-card-item">
            <div class="diary-card${d.type === 'mood' ? ' mood' : ''}${selectedCards.has(d.id) ? ' selected' : ''}"
                 onclick="openCard(event,${d.id})">
              <div class="card-header">
                <span class="card-date">${d.date}</span>
                <div class="card-tags">
                  ${(Array.isArray(d.tags) && d.tags.length
                ? d.tags.slice(0, 3).map(tag => `<span class="card-tag">${tag}</span>`).join('')
                : '<span class="card-tag">未分類</span>')}
                  ${(Array.isArray(d.tags) && d.tags.length > 3)
                ? `<span class="card-tag more">+${d.tags.length - 3}</span>`
                : ''}
                </div>
                <div class="card-close${selectedCards.has(d.id) ? ' checked' : ''}"
                     onclick="toggleSelect(event,${d.id})">
                  ${selectedCards.has(d.id) ? '✓' : '✕'}
                </div>
              </div>
              ${d.type === 'mood'
                ? `<div class="card-mood-emotions">${(() => {
                    const p = parseMoodPreview(d);
                    if (p.emotions.length > 0) return p.emotions.map(e => (Array.from(e)[0] || '🙂')).join(' ');
                    return d.emoji || '🙂';
                })()
                }</div>`
                : `<div class="card-title">${d.title || '（無標題）'}</div>`
            }
              <div class="card-excerpt">${d.type === 'mood'
                ? parseMoodPreview(d).summary
                : (d.excerpt || d.body || '（尚無內容）')
            }</div>
              <div class="card-footer">
                <span class="card-media"><span class="icon">🖼️</span>${Number(d.images) || 0}</span>
                <span class="card-media"><span class="icon">🎨</span>${Number(d.drawings) || 0}</span>
                <a class="card-edit" href="/Diary/DiaryEdit?id=${d.id}"
                   onclick="event.stopPropagation()" title="編輯">✏️</a>
              </div>
            </div>
            <div class="card-reactions${d.shared ? '' : ' is-hidden'}">
              <span class="reaction-title">回應</span>
              ${d.shared
                ? (Array.isArray(d.reactions) && d.reactions.length
                    ? d.reactions.map(r => `<span class="reaction-chip">${r}</span>`).join('')
                    : `<span class="reaction-chip">尚無回應</span>`)
                : `<span class="reaction-chip muted">未分享</span>`}
            </div>
          </div>
        `).join('');
    }
    renderPagination();
    updateBatchBar();
}

function renderPagination() {
    const total = Math.ceil(filteredDiaries.length / perPage);
    const pg = document.getElementById('pagination');
    if (total <= 1) { pg.innerHTML = ''; return; }
    let html = `<button class="page-btn nav" onclick="goPage(${currentPage - 1})">《</button>`;
    for (let i = 1; i <= total; i++) {
        html += `<button class="page-btn${i === currentPage ? ' active' : ''}" onclick="goPage(${i})">${i}</button>`;
    }
    html += `<button class="page-btn nav" onclick="goPage(${currentPage + 1})">》</button>`;
    pg.innerHTML = html;
}

function goPage(p) {
    const total = Math.ceil(filteredDiaries.length / perPage);
    if (p < 1 || p > total) return;
    currentPage = p;
    renderGrid();
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

/* ============================================================
   卡片互動：開啟、選取
   ============================================================ */

function openCard(event, id) {
    if (event.target.classList.contains('card-close')) return;
    if (event.target.classList.contains('card-edit') || event.target.closest('a')) return;
    window.location.href = `/Diary/DiaryDetail?id=${id}`;
}

function toggleSelect(event, id) {
    event.stopPropagation();
    if (selectedCards.has(id)) selectedCards.delete(id);
    else selectedCards.add(id);
    renderGrid();
}

/* ============================================================
   批次操作列
   ============================================================ */

function updateBatchBar() {
    const n = selectedCards.size;
    const info = document.getElementById('selectedInfo');
    const del = document.getElementById('batchDeleteBtn');
    const all = document.getElementById('selectAllBtn');
    const cancel = document.getElementById('cancelSelectBtn');
    if (n > 0) {
        info.style.display = 'inline';
        info.textContent = `已選 ${n} 篇`;
        del.style.display = 'inline-block';
        all.style.display = 'inline-block';
        cancel.style.display = 'inline-block';
    } else {
        info.style.display = 'none';
        del.style.display = 'none';
        all.style.display = 'none';
        cancel.style.display = 'none';
    }
}

function selectAll() {
    const start = (currentPage - 1) * perPage;
    filteredDiaries.slice(start, start + perPage).forEach(d => selectedCards.add(d.id));
    renderGrid();
}

function cancelSelection() {
    selectedCards.clear();
    renderGrid();
}

/* ============================================================
   刪除 Modal
   ============================================================ */

function showDeleteModal() {
    const n = selectedCards.size;
    document.getElementById('deleteModalText').textContent =
        `確定要刪除這 ${n} 篇日記嗎？\n移除後無法復原。`;
    document.getElementById('deleteModal').classList.add('show');
}

async function confirmDelete() {
    const ids = Array.from(selectedCards);
    if (ids.length === 0) {
        hideModal('deleteModal');
        return;
    }

    const token = document.querySelector('#batchDeleteTokenForm input[name="__RequestVerificationToken"]')?.value || '';
    const formData = new FormData();
    ids.forEach(id => formData.append('ids', String(id)));
    formData.append('__RequestVerificationToken', token);

    try {
        const response = await fetch(window.deleteDiariesUrl || '/Diary/DeleteDiaries', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });

        if (!response.ok) throw new Error('delete failed');

        const removed = new Set(ids);
        diaries = diaries.filter(d => !removed.has(d.id));
        window.diaryEntries = diaries.slice();
        selectedCards.clear();
        refreshSource(true);
        hideModal('deleteModal');
        showToast('已刪除日記');
    } catch (error) {
        hideModal('deleteModal');
        showToast('刪除失敗，請稍後再試。');
    }
}

function hideModal(id) {
    document.getElementById(id).classList.remove('show');
}

/* ============================================================
   時間篩選按鈕
   ============================================================ */

function setActiveTimeButton(type) {
    document.querySelectorAll('.diary-list-page .filter-btn').forEach(btn => {
        const active =
            (btn.textContent.includes('全部') && type === 'all') ||
            (btn.textContent.includes('本週') && type === 'week') ||
            (btn.textContent.includes('本月') && type === 'month') ||
            (btn.textContent.includes('特定範圍') && type === 'range');
        btn.classList.toggle('active', !!active);
    });
}

function setTimeFilter(el, type) {
    document.querySelectorAll('.diary-list-page .filter-btn').forEach(b => b.classList.remove('active'));
    el.classList.add('active');
    if (type === 'range') {
        document.getElementById('rangeStart').value = filterState.rangeStart;
        document.getElementById('rangeEnd').value = filterState.rangeEnd;
        document.getElementById('rangeModal').classList.add('show');
        return;
    }
    filterState.time = type;
    filterState.rangeStart = '';
    filterState.rangeEnd = '';
    applyFilters(true);
}

function confirmRangeFilter() {
    const start = document.getElementById('rangeStart').value;
    const end = document.getElementById('rangeEnd').value;
    if (!start || !end) { showToast('請選擇開始與結束日期'); return; }
    if (start > end) { showToast('日期區間不正確'); return; }
    filterState.time = 'range';
    filterState.rangeStart = start;
    filterState.rangeEnd = end;
    hideModal('rangeModal');
    setActiveTimeButton('range');
    applyFilters(true);
}

function clearRangeFilter() {
    filterState.time = 'all';
    filterState.rangeStart = '';
    filterState.rangeEnd = '';
    document.getElementById('rangeStart').value = '';
    document.getElementById('rangeEnd').value = '';
    hideModal('rangeModal');
    setActiveTimeButton('all');
    applyFilters(true);
}

/* ============================================================
   Toast 通知
   ============================================================ */

function showToast(msg) {
    const t = document.getElementById('toast');
    t.textContent = msg;
    t.classList.add('show');
    setTimeout(() => t.classList.remove('show'), 2500);
}

document.addEventListener('click', function (e) {
    document.querySelectorAll('.filter-multi.open').forEach(wrap => {
        if (!wrap.contains(e.target)) wrap.classList.remove('open');
    });
});

refreshSource(true);