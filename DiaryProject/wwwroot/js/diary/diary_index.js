/* ============================================================
   日記列表頁 diary_index.js
   ------------------------------------------------------------
   功能：
   1. 前端搜尋 / 篩選 / 排序 / 分頁
   2. 批次刪除
   3. 點卡片進詳情頁時，保留目前篩選條件
   4. 把目前篩選後的 ID 順序存到 sessionStorage，
      讓詳情頁上一篇 / 下一篇可以依照列表頁順序切換。
   ============================================================ */

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
        datePart.getFullYear(),
        datePart.getMonth(),
        datePart.getDate(),
        hour,
        minute,
        0,
        0
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

function escapeHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
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
        emotions,
        summary: notes || fallbackSummary
    };
}

/* ============================================================
   詳情頁連結：保留篩選條件
   ============================================================ */

function saveCurrentListOrderForDetail() {
    // 將目前篩選與排序後的完整 ID 順序存起來。
    // 詳情頁會讀取這份資料，讓上一篇 / 下一篇完全照目前列表頁順序。
    const ids = filteredDiaries.map(d => d.id);
    sessionStorage.setItem('diaryListNavIds', JSON.stringify(ids));
}

function buildDetailUrl(id) {
    const params = new URLSearchParams();

    params.set('id', String(id));

    // 模板篩選：normal / mood
    if (filterState.template && filterState.template !== 'all') {
        params.set('templateType', filterState.template);
    }

    // 分享狀態：shared / private
    if (filterState.share && filterState.share !== 'all') {
        params.set('visibility', filterState.share);
    }

    // 搜尋關鍵字
    const keyword = (filterState.query || '').trim();
    if (keyword) {
        params.set('keyword', keyword);
    }

    // 排序方向：desc / asc
    if (filterState.sort && filterState.sort !== 'desc') {
        params.set('sortOrder', filterState.sort);
    }

    // 期間篩選：all / week / month / range
    if (filterState.time && filterState.time !== 'all') {
        params.set('period', filterState.time);
    }

    // 日期範圍
    if (filterState.time === 'range') {
        if (filterState.rangeStart) params.set('startDate', filterState.rangeStart);
        if (filterState.rangeEnd) params.set('endDate', filterState.rangeEnd);
    }

    // 標籤篩選，多個標籤用逗號串起來。
    if (Array.isArray(filterState.tags) && filterState.tags.length > 0) {
        params.set('tags', filterState.tags.join(','));
    }

    return `/Diary/DiaryDetail?${params.toString()}`;
}

/* ============================================================
   網址與篩選狀態同步
   ------------------------------------------------------------
   為什麼需要這段：
   1. 從詳情頁返回列表頁時，網址可能還帶著 templateType / visibility / tags。
   2. 使用者重新選「全部」時，要把網址上的舊參數清掉。
   3. 如果 DiaryList 是依網址參數由後端先篩資料，選「全部」時前端資料可能不完整，
      因此需要重新載入列表頁，讓後端重新取得完整資料。
   ============================================================ */

let isApplyingInitialFilterFromUrl = false;

function getFilterQueryString() {
    const params = new URLSearchParams();

    // 模板：all 是預設值，不寫進網址。
    if (filterState.template && filterState.template !== 'all') {
        params.set('templateType', filterState.template);
    }

    // 分享狀態：all 是預設值，不寫進網址。
    if (filterState.share && filterState.share !== 'all') {
        params.set('visibility', filterState.share);
    }

    // 搜尋關鍵字。
    if ((filterState.query || '').trim()) {
        params.set('keyword', filterState.query.trim());
    }

    // 排序：desc 是預設值，不寫；asc 才寫。
    if (filterState.sort && filterState.sort !== 'desc') {
        params.set('sortOrder', filterState.sort);
    }

    // 期間：all 是預設值，不寫。
    if (filterState.time && filterState.time !== 'all') {
        params.set('period', filterState.time);
    }

    // 日期範圍。
    if (filterState.time === 'range') {
        if (filterState.rangeStart) params.set('startDate', filterState.rangeStart);
        if (filterState.rangeEnd) params.set('endDate', filterState.rangeEnd);
    }

    // 標籤，多個用逗號串起來。
    if (Array.isArray(filterState.tags) && filterState.tags.length > 0) {
        params.set('tags', filterState.tags.join(','));
    }

    return params.toString();
}

function syncFilterStateToUrl() {
    const queryString = getFilterQueryString();
    const newUrl = queryString
        ? `${window.location.pathname}?${queryString}`
        : window.location.pathname;

    // 只更新網址，不重新整理頁面。
    window.history.replaceState(null, '', newUrl);
}

function reloadListByCurrentFilterState() {
    const queryString = getFilterQueryString();
    const newUrl = queryString
        ? `${window.location.pathname}?${queryString}`
        : window.location.pathname;

    // 重新載入列表頁，讓後端重新抓資料。
    window.location.href = newUrl;
}

function initFilterStateFromUrl() {
    const params = new URLSearchParams(window.location.search);

    const templateType = params.get('templateType');
    if (templateType === 'normal' || templateType === 'mood') {
        filterState.template = templateType;
    }

    const visibility = params.get('visibility');
    if (visibility === 'shared') {
        filterState.share = 'shared';
    } else if (visibility === 'private') {
        filterState.share = 'private';
    }

    const keyword = (params.get('keyword') || '').trim();
    if (keyword) {
        filterState.query = keyword.toLowerCase();
        const input = document.getElementById('searchInput');
        if (input) input.value = keyword;
    }

    const sortOrder = params.get('sortOrder');
    if (sortOrder === 'asc') {
        filterState.sort = 'asc';
    }

    const period = params.get('period');
    if (['all', 'week', 'month', 'range'].includes(period)) {
        filterState.time = period;
    }

    const startDate = params.get('startDate') || '';
    const endDate = params.get('endDate') || '';
    if (startDate) filterState.rangeStart = startDate;
    if (endDate) filterState.rangeEnd = endDate;

    const tags = (params.get('tags') || '')
        .split(',')
        .map(t => t.trim())
        .filter(Boolean);

    if (tags.length > 0) {
        filterState.tags = tags;
    }

    syncFilterButtonsFromState();
}

function syncFilterButtonsFromState() {
    // 模板按鈕文字與 active 狀態。
    const templateLabelMap = {
        all: '全部',
        normal: '一般模板',
        mood: '心情模板'
    };
    const templateBtn = document.getElementById('templateFilterBtn');
    if (templateBtn) templateBtn.textContent = templateLabelMap[filterState.template] || '全部';
    document.querySelectorAll('#templateFilterWrap .filter-menu-item').forEach(item => {
        item.classList.toggle('active', item.dataset.value === filterState.template);
    });

    // 分享狀態按鈕文字與 active 狀態。
    const shareLabelMap = {
        all: '全部',
        shared: '已分享',
        private: '未分享'
    };
    const shareBtn = document.getElementById('shareFilterBtn');
    if (shareBtn) shareBtn.textContent = shareLabelMap[filterState.share] || '全部';
    document.querySelectorAll('#shareFilterWrap .filter-menu-item').forEach(item => {
        item.classList.toggle('active', item.dataset.value === filterState.share);
    });

    // 排序按鈕文字與 active 狀態。
    const sortLabelMap = {
        desc: '由新到舊',
        asc: '由舊到新'
    };
    const sortBtn = document.getElementById('sortFilterBtn');
    if (sortBtn) sortBtn.textContent = sortLabelMap[filterState.sort] || '由新到舊';
    document.querySelectorAll('#sortFilterWrap .filter-menu-item').forEach(item => {
        item.classList.toggle('active', item.dataset.value === filterState.sort);
    });

    setActiveTimeButton(filterState.time);
}

/**
 * 判斷這次操作是否需要重新載入列表頁。
 *
 * 你的列表頁如果是從詳情頁帶 query string 回來，例如：
 * /Diary/DiaryList?templateType=mood
 * 後端可能只傳回 mood 的資料。
 *
 * 這時使用者改選「全部」，前端手上的 diaries 仍然只有 mood，
 * 所以一定要重新載入 /Diary/DiaryList，讓後端重新抓完整資料。
 */
function shouldReloadWhenClearingFilter(type, value) {
    const params = new URLSearchParams(window.location.search);

    if (type === 'template' && value === 'all' && params.has('templateType')) return true;
    if (type === 'share' && value === 'all' && params.has('visibility')) return true;
    if (type === 'sort' && value === 'desc' && params.has('sortOrder')) return true;

    return false;
}

/* ============================================================
   標籤篩選
   ============================================================ */

function refreshTagFilterOptions() {
    const menu = document.getElementById('tagFilterMenu');
    const btn = document.getElementById('tagFilterBtn');
    if (!menu || !btn) return;

    const tagsFromDiaries = Array.from(new Set(
        diaries.flatMap(d => Array.isArray(d.tags) ? d.tags : [])
    ));

    const customTags = tagsFromDiaries
        .filter(tag => !SYSTEM_TAG_ORDER.includes(tag))
        .sort((a, b) => a.localeCompare(b, 'zh-Hant'));

    const merged = [...SYSTEM_TAG_ORDER, ...customTags]
        .filter((tag, index, arr) => tag && arr.indexOf(tag) === index);

    filterState.tags = filterState.tags.filter(tag => merged.includes(tag));

    menu.innerHTML = '';

    // 標籤選單最上方加入「全部」。
    // 用途：清除所有標籤勾選，並移除網址上的 tags 參數。
    const allRow = document.createElement('button');
    allRow.type = 'button';
    allRow.className = 'filter-menu-item tag-clear-item';
    allRow.textContent = '全部';
    allRow.addEventListener('click', clearTagFilter);
    menu.appendChild(allRow);

    merged.forEach(tag => {
        const row = document.createElement('label');
        row.className = 'filter-menu-item';
        row.innerHTML = `
          <input type="checkbox" value="${escapeHtml(tag)}" ${filterState.tags.includes(tag) ? 'checked' : ''}>
          <span>${escapeHtml(tag)}</span>`;
        row.querySelector('input').addEventListener('change', applyTagSelection);
        menu.appendChild(row);
    });

    updateTagFilterButtonText(btn);
}

function updateTagFilterButtonText(btn) {
    if (!btn) return;

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

function clearTagFilter() {
    // 清空標籤篩選狀態。
    filterState.tags = [];

    // 取消所有標籤 checkbox。
    document.querySelectorAll('#tagFilterMenu input[type="checkbox"]').forEach(input => {
        input.checked = false;
    });

    updateTagFilterButtonText(document.getElementById('tagFilterBtn'));
    document.getElementById('tagFilterWrap')?.classList.remove('open');

    // 如果目前網址有 tags 參數，代表後端可能只回傳標籤篩選後資料。
    // 因此清除標籤時重新載入，才能取回完整列表。
    const params = new URLSearchParams(window.location.search);
    if (params.has('tags')) {
        reloadListByCurrentFilterState();
        return;
    }

    applyFilters(true);
}

function toggleTagMenu() {
    document.getElementById('tagFilterWrap')?.classList.toggle('open');
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
    const btn = document.getElementById(`${type}FilterBtn`);
    if (btn) btn.textContent = label;

    const wrap = getSingleFilterWrap(type);
    wrap?.querySelectorAll('.filter-menu-item').forEach(item => {
        item.classList.toggle('active', item.dataset.value === value);
    });
    wrap?.classList.remove('open');

    // 如果是從有網址參數的狀態切回「全部」，
    // 需要重新載入列表頁，否則前端手上可能只有後端篩選後的部分資料。
    if (!isApplyingInitialFilterFromUrl && shouldReloadWhenClearingFilter(type, value)) {
        reloadListByCurrentFilterState();
        return;
    }

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

    const totalEl = document.getElementById('totalCount');
    const normalEl = document.getElementById('normalCount');
    const moodEl = document.getElementById('moodCount');

    if (totalEl) totalEl.textContent = diaries.length;
    if (normalEl) normalEl.textContent = normal;
    if (moodEl) moodEl.textContent = mood;
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
    const q = (filterState.query || '').toLowerCase();

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
        const ta = getSortTimestamp(a);
        const tb = getSortTimestamp(b);

        // 若日期時間相同，改用 DiaryId 作為穩定排序依據。
        if (ta === tb) {
            return filterState.sort === 'asc'
                ? Number(a.id) - Number(b.id)
                : Number(b.id) - Number(a.id);
        }

        return filterState.sort === 'asc' ? ta - tb : tb - ta;
    });

    selectedCards = new Set(
        Array.from(selectedCards).filter(id => filteredDiaries.some(d => d.id === id))
    );

    if (resetPage) currentPage = 1;

    const total = Math.max(1, Math.ceil(filteredDiaries.length / perPage));
    if (currentPage > total) currentPage = total;

    // 每次篩選 / 排序後同步網址。
    // 當使用者選「全部」時，這裡會把舊的 templateType / visibility / tags 等參數移除。
    syncFilterStateToUrl();

    renderGrid();
}

/* ============================================================
   搜尋
   ============================================================ */

function runSearch() {
    const input = document.getElementById('searchInput');
    filterState.query = (input?.value || '').trim().toLowerCase();
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

    if (!grid || !emptyState || !noResult || !pagination) return;

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

        grid.innerHTML = pageItems.map(d => {
            const moodPreview = parseMoodPreview(d);
            const tagsHtml = Array.isArray(d.tags) && d.tags.length
                ? d.tags.slice(0, 3).map(tag => `<span class="card-tag">${escapeHtml(tag)}</span>`).join('')
                : '<span class="card-tag">未分類</span>';

            const moreTagHtml = Array.isArray(d.tags) && d.tags.length > 3
                ? `<span class="card-tag more">+${d.tags.length - 3}</span>`
                : '';

            const titleHtml = d.type === 'mood'
                ? `<div class="card-mood-emotions">${
                    moodPreview.emotions.length > 0
                        ? moodPreview.emotions.map(e => escapeHtml(Array.from(e)[0] || '🙂')).join(' ')
                        : escapeHtml(d.emoji || '🙂')
                  }</div>`
                : `<div class="card-title">${escapeHtml(d.title || '（無標題）')}</div>`;

            const excerptHtml = d.type === 'mood'
                ? escapeHtml(moodPreview.summary)
                : escapeHtml(d.excerpt || d.body || '（尚無內容）');

            const reactionsHtml = d.shared
                ? (Array.isArray(d.reactions) && d.reactions.length
                    ? d.reactions.map(r => `<span class="reaction-chip">${escapeHtml(r)}</span>`).join('')
                    : `<span class="reaction-chip">尚無回應</span>`)
                : `<span class="reaction-chip muted">未分享</span>`;

            return `
              <div class="diary-card-item">
                <div class="diary-card${d.type === 'mood' ? ' mood' : ''}${selectedCards.has(d.id) ? ' selected' : ''}"
                     onclick="openCard(event, ${Number(d.id)})">
                  <div class="card-header">
                    <span class="card-date">${escapeHtml(d.date)}</span>
                    <div class="card-tags">
                      ${tagsHtml}
                      ${moreTagHtml}
                    </div>
                    <div class="card-close${selectedCards.has(d.id) ? ' checked' : ''}"
                         onclick="toggleSelect(event, ${Number(d.id)})">
                      ${selectedCards.has(d.id) ? '✓' : '✕'}
                    </div>
                  </div>

                  ${titleHtml}

                  <div class="card-excerpt">${excerptHtml}</div>

                  <div class="card-footer">
                    <span class="card-media"><span class="icon">🖼️</span>${Number(d.images) || 0}</span>
                    <span class="card-media"><span class="icon">🎨</span>${Number(d.drawings) || 0}</span>
                    <a class="card-edit" href="/Diary/DiaryEdit?id=${Number(d.id)}"
                       onclick="event.stopPropagation()" title="編輯">✏️</a>
                  </div>
                </div>

                <div class="card-reactions${d.shared ? '' : ' is-hidden'}">
                  <span class="reaction-title">回應</span>
                  ${reactionsHtml}
                </div>
              </div>
            `;
        }).join('');
    }

    renderPagination();
    updateBatchBar();
}

function renderPagination() {
    const total = Math.ceil(filteredDiaries.length / perPage);
    const pg = document.getElementById('pagination');

    if (!pg) return;

    if (total <= 1) {
        pg.innerHTML = '';
        return;
    }

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

    saveCurrentListOrderForDetail();
    window.location.href = buildDetailUrl(id);
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

    if (!info || !del || !all || !cancel) return;

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
    const text = document.getElementById('deleteModalText');
    const modal = document.getElementById('deleteModal');

    if (text) {
        text.textContent = `確定要刪除這 ${n} 篇日記嗎？\n移除後無法復原。`;
    }

    modal?.classList.add('show');
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
    document.getElementById(id)?.classList.remove('show');
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
        const startInput = document.getElementById('rangeStart');
        const endInput = document.getElementById('rangeEnd');

        if (startInput) startInput.value = filterState.rangeStart;
        if (endInput) endInput.value = filterState.rangeEnd;

        document.getElementById('rangeModal')?.classList.add('show');
        return;
    }

    filterState.time = type;
    filterState.rangeStart = '';
    filterState.rangeEnd = '';

    // 如果目前網址有期間篩選，改回「全部」時重新載入，
    // 讓後端重新抓完整資料。
    const params = new URLSearchParams(window.location.search);
    if (!isApplyingInitialFilterFromUrl && type === 'all' && (params.has('period') || params.has('startDate') || params.has('endDate'))) {
        reloadListByCurrentFilterState();
        return;
    }

    applyFilters(true);
}

function confirmRangeFilter() {
    const start = document.getElementById('rangeStart')?.value || '';
    const end = document.getElementById('rangeEnd')?.value || '';

    if (!start || !end) {
        showToast('請選擇開始與結束日期');
        return;
    }

    if (start > end) {
        showToast('日期區間不正確');
        return;
    }

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

    const startInput = document.getElementById('rangeStart');
    const endInput = document.getElementById('rangeEnd');

    if (startInput) startInput.value = '';
    if (endInput) endInput.value = '';

    hideModal('rangeModal');
    setActiveTimeButton('all');

    const params = new URLSearchParams(window.location.search);
    if (params.has('period') || params.has('startDate') || params.has('endDate')) {
        reloadListByCurrentFilterState();
        return;
    }

    applyFilters(true);
}

/* ============================================================
   Toast 通知
   ============================================================ */

function showToast(msg) {
    const t = document.getElementById('toast');
    if (!t) return;

    t.textContent = msg;
    t.classList.add('show');

    setTimeout(() => t.classList.remove('show'), 2500);
}

/* ============================================================
   點擊空白處關閉篩選選單
   ============================================================ */

document.addEventListener('click', function (e) {
    document.querySelectorAll('.filter-multi.open').forEach(wrap => {
        if (!wrap.contains(e.target)) wrap.classList.remove('open');
    });
});

/* ============================================================
   初始化
   ------------------------------------------------------------
   先讀取網址參數，再載入資料。
   這樣從詳情頁返回列表頁時，畫面按鈕狀態會和網址條件一致。
   ============================================================ */

isApplyingInitialFilterFromUrl = true;
initFilterStateFromUrl();
refreshSource(true);
isApplyingInitialFilterFromUrl = false;
