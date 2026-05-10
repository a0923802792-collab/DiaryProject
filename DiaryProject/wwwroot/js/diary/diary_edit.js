/* ============================================================
   日記新增 / 編輯頁
   - 管理日期時間、標籤、模板切換、媒體上傳與繪圖。
   - 表單送出時統一整理 hidden 欄位，交由 DiaryController 儲存。
   ============================================================ */

let isDirty = false;
let leaveTarget = null;
let currentTemplate = 'normal';
let mediaItems = [];
const initialMediaItems = Array.isArray(window.initialMediaItems) ? window.initialMediaItems : [];
let isEditMode = false;
let shareType = 'private';
let pendingDeleteAction = null;
const deletedCustomTags = new Set();
const MAX_MEDIA_COUNT = 10;
const STORAGE_SOFT_LIMIT_BYTES = 4_200_000;
const STORAGE_HARD_LIMIT_BYTES = 4_700_000;

/* ============================================================
   媒體資料與容量檢查
   ============================================================ */

function normalizeMediaItem(item) {
    if (!item) return null;
    if (typeof item === 'string') {
        const src = item.trim();
        return src ? { kind: 'image', src } : null;
    }
    if (typeof item === 'object') {
        const src = typeof item.src === 'string' ? item.src.trim() : '';
        if (!src) return null;
        const kind = item.kind === 'drawing' ? 'drawing' : 'image';
        return { kind, src };
    }
    return null;
}

function getMediaSummary() {
    const summary = mediaItems.reduce((acc, item) => {
        if (item.kind === 'drawing') acc.drawings += 1;
        else acc.images += 1;
        return acc;
    }, { images: 0, drawings: 0 });
    summary.total = summary.images + summary.drawings;
    return summary;
}

function estimateStringBytes(text) {
    return String(text || '').length * 2;
}

function estimateDataUrlBytes(dataUrl) {
    return estimateStringBytes(dataUrl || '');
}

function estimateFileDataUrlBytes(file) {
    const rawSize = Number(file?.size) || 0;
    // Base64 約為原始檔案大小的 4/3，另加 data URL 與 JSON 的額外字元。
    return Math.ceil((rawSize * 4) / 3) + 256;
}

function getStorageProjection(extraBytes) {
    const added = Math.max(0, Number(extraBytes) || 0);
    return { projected: added };
}

function setMediaWarningText(message, level = 'warning') {
    document.querySelectorAll('[data-media-warning]').forEach(el => {
        el.textContent = message || '';
        el.classList.remove('warning', 'danger', 'show');
        if (!message) return;
        el.classList.add(level === 'danger' ? 'danger' : 'warning', 'show');
    });
}

function refreshStorageWarning() {
    const status = getStorageProjection(0);
    if (status.projected >= STORAGE_HARD_LIMIT_BYTES) {
        setMediaWarningText('儲存空間幾乎已滿，請刪除部分圖片或繪圖。', 'danger');
        return;
    }
    if (status.projected >= STORAGE_SOFT_LIMIT_BYTES) {
        setMediaWarningText('儲存空間使用偏高，建議清理舊媒體。', 'warning');
        return;
    }
    setMediaWarningText('');
}

function canReserveStorage(extraBytes, options = {}) {
    const { silentSoftWarning = false, silentHardWarning = false } = options;
    const status = getStorageProjection(extraBytes);
    if (status.projected >= STORAGE_HARD_LIMIT_BYTES) {
        if (!silentHardWarning) {
            setMediaWarningText('儲存空間幾乎已滿，請刪除部分圖片或繪圖。', 'danger');
        }
        return false;
    }
    if (!silentSoftWarning && status.projected >= STORAGE_SOFT_LIMIT_BYTES) {
        setMediaWarningText('儲存空間使用偏高，建議清理舊媒體。', 'warning');
    } else if (!silentSoftWarning) {
        setMediaWarningText('');
    }
    return true;
}

/* ============================================================
   日期與時間初始化
   ============================================================ */
const days = ['日', '一', '二', '三', '四', '五', '六'];
const datePicker = document.getElementById('datePicker');
const dateLabel = document.getElementById('dateLabel');
const calendarBtn = document.getElementById('calendarBtn');
const timeSelect = document.getElementById('timeSelect');
const initialDiaryDateRaw = document.getElementById('diaryDateHidden')?.value || '';
const initialDiaryTimeRaw = document.getElementById('diaryTimeHidden')?.value || '';
let selectedDate = new Date();

function tryParseIsoDate(text) {
    const raw = String(text || '').trim();
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(raw);
    if (!m) return null;
    const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    return Number.isNaN(d.getTime()) ? null : d;
}

function tryParseTimeText(text) {
    const raw = String(text || '').trim();
    const m = /^([01]?\d|2[0-4]):([0-5]\d)$/.exec(raw);
    if (!m) return null;
    const hh = Number(m[1]);
    const mm = Number(m[2]);
    if (hh === 24 && mm !== 0) return null;
    return `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`;
}

function setTimeSelectValue(timeText) {
    const normalized = tryParseTimeText(timeText);
    if (!normalized || !timeSelect) return false;
    if (!timeSelect.querySelector(`option[value="${normalized}"]`)) {
        const opt = document.createElement('option');
        opt.value = normalized;
        opt.textContent = normalized;
        timeSelect.appendChild(opt);
    }
    timeSelect.value = normalized;
    return true;
}

function toDateInputValue(d) {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
}

function renderSelectedDate() {
    document.getElementById('dayNum').textContent = selectedDate.getDate();
    document.getElementById('dayName').textContent = '星期' + days[selectedDate.getDay()];
    dateLabel.textContent = `${selectedDate.getFullYear()} 年 ${selectedDate.getMonth() + 1} 月 ${selectedDate.getDate()} 日`;
    datePicker.value = toDateInputValue(selectedDate);
    const diaryDateHidden = document.getElementById('diaryDateHidden');
    if (diaryDateHidden) diaryDateHidden.value = toDateInputValue(selectedDate);
}

function buildTimeOptions() {
    const options = [];
    for (let h = 0; h <= 23; h += 1) {
        const hh = String(h).padStart(2, '0');
        options.push(`<option value="${hh}:00">${hh}:00</option>`);
        options.push(`<option value="${hh}:30">${hh}:30</option>`);
    }
    options.push('<option value="24:00">24:00</option>');
    timeSelect.innerHTML = options.join('');
}

function setDefaultCurrentTime() {
    const now = new Date();
    let hour = now.getHours();
    const minute = now.getMinutes();
    let roundedMinute = minute >= 30 ? 30 : 0;
    if (minute >= 45) {
        hour += 1;
        roundedMinute = 0;
    }
    if (hour >= 24) {
        timeSelect.value = '24:00';
        return;
    }
    timeSelect.value = `${String(hour).padStart(2, '0')}:${String(roundedMinute).padStart(2, '0')}`;
}

const params = new URLSearchParams(window.location.search);
const diaryIdHiddenInput = document.getElementById('diaryId');
const templateTypeHiddenInput = document.getElementById('templateType');
const serverDiaryId = Number(diaryIdHiddenInput?.value || 0);
const serverTemplateType = (templateTypeHiddenInput?.value || 'normal').trim() === 'mood' ? 'mood' : 'normal';
const serverDiaryDate = tryParseIsoDate(initialDiaryDateRaw);

isEditMode = serverDiaryId > 0 || params.get('edit') === '1';
currentTemplate = serverTemplateType;

buildTimeOptions();
if (serverDiaryDate) {
    selectedDate = serverDiaryDate;
}
if (!setTimeSelectValue(initialDiaryTimeRaw)) {
    setDefaultCurrentTime();
}
datePicker.max = toDateInputValue(new Date());
renderSelectedDate();
switchTemplate(currentTemplate);
isDirty = false;

calendarBtn.addEventListener('click', function () {
    if (typeof datePicker.showPicker === 'function') datePicker.showPicker();
    else datePicker.click();
});
datePicker.addEventListener('change', function () {
    if (!datePicker.value) return;
    const chosen = new Date(datePicker.value + 'T00:00:00');
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (chosen > today) {
        showToast('不能選擇未來日期。');
        datePicker.value = toDateInputValue(selectedDate);
        return;
    }
    selectedDate = chosen;
    renderSelectedDate();
    markDirty();
});
timeSelect.addEventListener('change', markDirty);

// 來源頁面：draft 表示從草稿進入，其餘回到列表或詳情頁。
const editFrom = params.get('from') || '';       // 'draft' | ''

// 依照目前狀態決定返回目標。
function buildBackDest() {
    if (editFrom === 'draft') return '/Diary/DiaryList';
    if (isEditMode && serverDiaryId > 0) {
        return `/Diary/DiaryDetail?id=${serverDiaryId}`;
    }
    return '/Diary/DiaryList';
}

renderCustomTags();

function markDirty() { isDirty = true; }

function hasValidNormalContent(title, body) {
    const safeTitle = (title || '').trim();
    const safeBody = (body || '').trim();
    if (safeTitle) return true;
    if (!safeBody) return false;
    return safeBody !== '尚未輸入內容';
}

function hasDraftContentForNormal(title, body) {
    const safeTitle = (title || '').trim();
    const safeBody = (body || '').trim();
    return !!safeTitle || !!safeBody || mediaItems.length > 0;
}

function hasDraftContentForMood() {
    const emotions = getSelectedMoodEmotions();
    const scales = getMoodScaleValues();
    const hasScale = [scales.spirit, scales.stress, scales.sleep].some(v => Number(v) > 0);
    return emotions.length > 0 || hasScale || mediaItems.length > 0;
}

function normalizeTagValue(raw) {
    const text = String(raw || '').trim();
    if (!text) return '';
    return text.replace(/\s*[x×✕]\s*$/i, '').trim();
}

function getSelectedTags() {
    return Array.from(document.querySelectorAll('#tagsArea .tag-pill.active'))
        .map(el => normalizeTagValue(el.getAttribute('data-tag') || el.textContent))
        .filter(tag => tag && !tag.startsWith('+'));
}

function switchTemplate(t) {
    currentTemplate = t;
    const isNormal = t === 'normal';
    const normalTemplate = document.getElementById('normalTemplate');
    const moodTemplate = document.getElementById('moodTemplate');
    normalTemplate.style.display = isNormal ? '' : 'none';
    moodTemplate.style.display = isNormal ? 'none' : '';
    normalTemplate.classList.toggle('hidden', !isNormal);
    moodTemplate.classList.toggle('hidden', isNormal);
    document.getElementById('tmplNormal').classList.toggle('active', isNormal);
    document.getElementById('tmplMood').classList.toggle('active', !isNormal);
    markDirty();
}

// 返回前若有未儲存內容，先顯示確認視窗。
function handleBack() {
    const dest = buildBackDest();
    if (isDirty) {
        leaveTarget = dest;
        document.getElementById('leaveModal').classList.add('show');
    } else {
        window.location.href = dest;
    }
}

// 確認離開並導回目標頁。
function confirmLeave() {
    window.location.href = leaveTarget || buildBackDest();
}

function saveDraft() {
    const title = document.getElementById('titleInput')?.value.trim();
    const body = getBodyText().trim();
    const canSaveDraft = currentTemplate === 'mood'
        ? hasDraftContentForMood()
        : hasDraftContentForNormal(title, body);
    if (!canSaveDraft) {
        document.getElementById('emptyModal').classList.add('show');
        return;
    }
    submitDiaryForm('draft');
}

function completeDiary() {
    const title = document.getElementById('titleInput')?.value.trim();
    const body = getBodyText().trim();
    if (currentTemplate === 'mood' && !validateMoodRequired()) return;
    if (currentTemplate === 'normal' && !hasValidNormalContent(title, body)) {
        document.getElementById('emptyModal').classList.add('show');
        return;
    }
    submitDiaryForm('active');
}

function setShare(type) {
    shareType = type;
    document.getElementById('sharePrivate').classList.toggle('active', type === 'private');
    document.getElementById('shareAnon').classList.toggle('active', type === 'anon');
    document.getElementById('shareAnon').classList.toggle('anon', type === 'anon');
    const visibilityHidden = document.getElementById('visibilityHidden');
    if (visibilityHidden) visibilityHidden.value = (type === 'anon') ? 'shared' : 'private';
    if (isEditMode) markDirty();
}

function toggleTag(el) {
    if (!el || el.classList.contains('add-tag-btn')) return;
    el.classList.toggle('active');
    markDirty();
}

function buildCustomTagPill(tagName, isActive = true) {
    const pill = document.createElement('span');
    pill.className = `tag-pill custom-tag-pill${isActive ? ' active' : ''}`;
    pill.setAttribute('data-custom', '1');
    pill.setAttribute('data-tag', tagName);
    pill.onclick = function () { toggleTag(pill); };

    const label = document.createElement('span');
    label.className = 'tag-text';
    label.textContent = tagName;

    const removeBtn = document.createElement('button');
    removeBtn.type = 'button';
    removeBtn.className = 'tag-remove-btn';
    removeBtn.title = '刪除自訂標籤';
    removeBtn.textContent = '×';
    removeBtn.onclick = function (event) { removeCustomTag(event, removeBtn); };

    pill.append(label, removeBtn);
    return pill;
}

function removeCustomTag(event, btn) {
    if (event) event.stopPropagation();
    const pill = btn?.closest('.custom-tag-pill');
    if (!pill) return;
    const tagName = normalizeTagValue(pill.getAttribute('data-tag') || pill.querySelector('.tag-text')?.textContent || pill.textContent);
    showDeleteConfirm(`確定要刪除自訂標籤「${tagName || '未命名'}」嗎？`, function () {
        if (tagName) deletedCustomTags.add(tagName);
        pill.remove();
        markDirty();
    });
}

function showAddTag() {
    const input = document.getElementById('addTagInput');
    const addBtn = document.getElementById('addTagBtn');
    addBtn.style.display = 'none';
    input.style.display = 'inline-block';
    input.focus();
}

function addTag() {
    const input = document.getElementById('addTagInput');
    const addBtn = document.getElementById('addTagBtn');
    const val = input.value.trim();
    if (val) {
        const tagsArea = document.getElementById('tagsArea');
        const normalized = val.toLowerCase();
        const allTagNodes = Array.from(document.querySelectorAll('#tagsArea .tag-pill[data-tag]'));
        const existsNode = allTagNodes.find(el =>
            (el.getAttribute('data-tag') || '').trim().toLowerCase() === normalized
        );

        if (existsNode) {
            existsNode.classList.add('active');
            markDirty();
        } else {
            const customCount = document.querySelectorAll('#tagsArea .tag-pill[data-custom="1"]').length;
            if (customCount >= 10) {
                showToast('自訂標籤最多 10 個。');
                input.value = '';
                input.style.display = 'none';
                addBtn.style.display = 'inline-flex';
                return;
            }

            deletedCustomTags.delete(val);
            const pill = buildCustomTagPill(val, true);
            tagsArea.insertBefore(pill, addBtn);
            markDirty();
        }
    }
    input.value = '';
    input.style.display = 'none';
    addBtn.style.display = 'inline-flex';
}

function renderCustomTags() {
    document.querySelectorAll('#tagsArea .custom-tag-pill').forEach(pill => {
        if (!pill.querySelector('.tag-remove-btn')) {
            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'tag-remove-btn';
            removeBtn.title = '刪除自訂標籤';
            removeBtn.textContent = '×';
            removeBtn.onclick = function (event) { removeCustomTag(event, removeBtn); };
            pill.appendChild(removeBtn);
        }
        if (pill.dataset.boundClick === '1') return;
        pill.dataset.boundClick = '1';
        pill.onclick = function () { toggleTag(pill); };
    });
}


function extractEmojiFromEmotionText(text) {
    const raw = (text || '').trim();
    return raw ? (Array.from(raw)[0] || '🙂') : '🙂';
}

function toggleEmotion(el) {
    const willActivate = !el.classList.contains('active');
    if (willActivate) {
        const selectedCount = document.querySelectorAll('#emotionGrid .emotion-btn.active').length;
        if (selectedCount >= 6) {
            showToast('情緒最多可選 6 個。');
            return;
        }
    }
    el.classList.toggle('active');
    markDirty();
}

function getMoodScaleRanges() {
    return document.querySelectorAll('#moodTemplate .scale-row input[type="range"]');
}

function getMoodScaleValues() {
    const ranges = getMoodScaleRanges();
    return {
        spirit: Number(ranges[0]?.value || document.getElementById('spiritVal')?.textContent || 0),
        stress: Number(ranges[1]?.value || document.getElementById('stressVal')?.textContent || 0),
        sleep: Number(ranges[2]?.value || document.getElementById('sleepVal')?.textContent || 0)
    };
}

function setMoodScaleValues(values) {
    const ranges = getMoodScaleRanges();
    const spirit = Math.max(0, Math.min(10, Number(values?.spirit) || 0));
    const stress = Math.max(0, Math.min(10, Number(values?.stress) || 0));
    const sleep = Math.max(0, Math.min(10, Number(values?.sleep) || 0));
    if (ranges[0]) ranges[0].value = String(spirit);
    if (ranges[1]) ranges[1].value = String(stress);
    if (ranges[2]) ranges[2].value = String(sleep);
    document.getElementById('spiritVal').textContent = String(spirit);
    document.getElementById('stressVal').textContent = String(stress);
    document.getElementById('sleepVal').textContent = String(sleep);
}

function getSelectedMoodEmotions() {
    return Array.from(document.querySelectorAll('#emotionGrid .emotion-btn.active'))
        .map(btn => btn.textContent.trim())
        .filter(Boolean);
}

function getSelectedMoodIds() {
    return Array.from(document.querySelectorAll('#emotionGrid .emotion-btn.active'))
        .map(btn => btn.getAttribute('data-mood-id') || '')
        .filter(Boolean);
}

function setSelectedMoodEmotions(emotions) {
    const emotionSet = new Set(Array.isArray(emotions) ? emotions : []);
    document.querySelectorAll('#emotionGrid .emotion-btn').forEach(btn => {
        const txt = btn.textContent.trim();
        btn.classList.toggle('active', emotionSet.has(txt));
    });
}

function collectMoodPayload() {
    const emotions = getSelectedMoodEmotions();
    const scales = getMoodScaleValues();
    const eventText = (document.getElementById('moodBody')?.value || '').trim();
    const thoughtText = (document.getElementById('moodThought')?.value || '').trim();
    const needText = (document.getElementById('moodNeed')?.value || '').trim();
    const summary = `情緒:${emotions.length ? emotions.join('、') : '未選擇'} 精神:${scales.spirit} 壓力:${scales.stress} 睡眠:${scales.sleep}`;
    const body = [
        `情緒:${emotions.length ? emotions.join('、') : '未選擇'}`,
        `精神:${scales.spirit}`,
        `壓力:${scales.stress}`,
        `睡眠:${scales.sleep}`,
        `事件:${eventText}`,
        `想法:${thoughtText}`,
        `需求:${needText}`
    ].join('\n');
    const emoji = emotions.length
        ? emotions.map(extractEmojiFromEmotionText).join('')
        : '🙂';
    return { summary, body, emoji };
}

function validateMoodRequired() {
    const emotions = getSelectedMoodEmotions();
    if (emotions.length === 0) {
        showToast('請至少選擇 1 個情緒。');
        return false;
    }
    const scales = getMoodScaleValues();
    const values = [scales.spirit, scales.stress, scales.sleep];
    const invalidScale = values.some(v => !Number.isFinite(v) || v <= 0 || v > 10);
    if (invalidScale) {
        showToast('量表數值必須介於 1 到 10。');
        return false;
    }
    return true;
}

function updateScale(el, type) {
    document.getElementById(type + 'Val').textContent = el.value;
    markDirty();
}

function togglePrompt() {
    const body = document.getElementById('promptBody');
    const arrow = document.getElementById('promptArrow');
    if (!body || !arrow) return;
    const willOpen = body.classList.contains('hidden');
    body.classList.toggle('hidden', !willOpen);
    arrow.textContent = willOpen ? '△' : '▽';
}

/* ============================================================
   繪圖畫布
   ============================================================ */
const canvas = document.getElementById('drawCanvas');
const canvas2 = document.getElementById('drawCanvas2');
let drawColor = '#333333';
let drawSize = 2;
let currentTool = 'pen';
const drawStates = {};

function getBodyEditor() {
    return currentTemplate === 'mood'
        ? document.getElementById('moodBody')
        : document.getElementById('bodyInput');
}

function getBodyText() {
    const editor = getBodyEditor();
    if (!editor) return '';
    return currentTemplate === 'mood' ? editor.value : editor.innerText;
}

function setBodyText(text) {
    const editor = getBodyEditor();
    if (!editor) return;
    if (currentTemplate === 'mood') editor.value = text || '';
    else editor.innerText = text || '';
}

function focusEditor() {
    const editor = getBodyEditor();
    if (!editor) return;
    editor.focus();
}

function formatText(cmd) {
    focusEditor();
    document.execCommand(cmd, false, null);
    markDirty();
}

function getPointFromEvent(e, c) {
    const r = c.getBoundingClientRect();
    const scaleX = c.width / r.width;
    const scaleY = c.height / r.height;
    return { x: (e.clientX - r.left) * scaleX, y: (e.clientY - r.top) * scaleY };
}

function getActiveCanvas() {
    return currentTemplate === 'mood' ? canvas2 : canvas;
}

function hideCanvasHint(c) {
    const hint = c.parentElement.querySelector('.canvas-hint');
    if (hint) hint.style.display = 'none';
}

function showCanvasHint(c) {
    const hint = c.parentElement.querySelector('.canvas-hint');
    if (hint) hint.style.display = '';
}

function bindCanvasEvents(c) {
    const state = drawStates[c.id];
    c.addEventListener('pointerdown', e => {
        const p = getPointFromEvent(e, c);
        state.isDrawing = true;
        state.startX = p.x;
        state.startY = p.y;
        state.snapshot = state.ctx.getImageData(0, 0, c.width, c.height);
        state.history.push(state.snapshot);
        if (state.history.length > 40) state.history.shift();

        if (currentTool === 'pen' || currentTool === 'eraser') {
            state.ctx.beginPath();
            state.ctx.moveTo(p.x, p.y);
        }
        hideCanvasHint(c);
        c.setPointerCapture(e.pointerId);
        markDirty();
    });

    c.addEventListener('pointermove', e => {
        if (!state.isDrawing) return;
        const p = getPointFromEvent(e, c);
        state.ctx.lineCap = 'round';
        state.ctx.lineJoin = 'round';
        state.ctx.lineWidth = drawSize;

        if (currentTool === 'pen') {
            state.ctx.globalCompositeOperation = 'source-over';
            state.ctx.strokeStyle = drawColor;
            state.ctx.lineTo(p.x, p.y);
            state.ctx.stroke();
            return;
        }

        if (currentTool === 'eraser') {
            state.ctx.globalCompositeOperation = 'destination-out';
            state.ctx.lineTo(p.x, p.y);
            state.ctx.stroke();
            state.ctx.globalCompositeOperation = 'source-over';
            return;
        }

        state.ctx.putImageData(state.snapshot, 0, 0);
        state.ctx.globalCompositeOperation = 'source-over';
        state.ctx.strokeStyle = drawColor;
        if (currentTool === 'line') {
            state.ctx.beginPath();
            state.ctx.moveTo(state.startX, state.startY);
            state.ctx.lineTo(p.x, p.y);
            state.ctx.stroke();
        } else if (currentTool === 'rect') {
            state.ctx.strokeRect(state.startX, state.startY, p.x - state.startX, p.y - state.startY);
        } else if (currentTool === 'circle') {
            const rx = (p.x - state.startX) / 2;
            const ry = (p.y - state.startY) / 2;
            const cx = state.startX + rx;
            const cy = state.startY + ry;
            state.ctx.beginPath();
            state.ctx.ellipse(cx, cy, Math.abs(rx), Math.abs(ry), 0, 0, Math.PI * 2);
            state.ctx.stroke();
        }
    });

    c.addEventListener('pointerup', e => {
        state.isDrawing = false;
        c.releasePointerCapture(e.pointerId);
    });
    c.addEventListener('pointercancel', () => { state.isDrawing = false; });
    c.addEventListener('pointerleave', () => { state.isDrawing = false; });
}

function initCanvas(c) {
    if (!c) return;
    const cx = c.getContext('2d');
    cx.lineCap = 'round';
    cx.lineJoin = 'round';
    cx.strokeStyle = drawColor;
    cx.lineWidth = drawSize;
    drawStates[c.id] = {
        ctx: cx,
        history: [],
        isDrawing: false,
        startX: 0,
        startY: 0,
        snapshot: null
    };
    bindCanvasEvents(c);
}

initCanvas(canvas);
initCanvas(canvas2);

function setDrawTool(tool, btn) {
    currentTool = tool;
    document.querySelectorAll('.diary-edit-page .drawing-toolbar .draw-tool-btn[data-tool]').forEach(el => {
        el.classList.toggle('active', el.getAttribute('data-tool') === tool);
    });
    if (btn) btn.blur();
}

function setStrokeColor(color) {
    drawColor = color || '#333333';
    document.querySelectorAll('.drawing-toolbar input[type="color"]').forEach(el => { el.value = drawColor; });
}

function setStrokeSize(size) {
    const n = Number(size);
    drawSize = Number.isFinite(n) ? Math.max(1, Math.min(18, n)) : 2;
    document.querySelectorAll('.drawing-toolbar input[type="range"]').forEach(el => { el.value = String(drawSize); });
}

function undoDraw() {
    const c = getActiveCanvas();
    const state = drawStates[c.id];
    if (!state || state.history.length === 0) return;
    const previous = state.history.pop();
    state.ctx.putImageData(previous, 0, 0);
}

function clearCanvas() {
    const c = getActiveCanvas();
    const state = drawStates[c.id];
    if (!state) return;
    state.history.push(state.ctx.getImageData(0, 0, c.width, c.height));
    state.ctx.clearRect(0, 0, c.width, c.height);
    showCanvasHint(c);
    markDirty();
}

function hasCanvasContent(canvasEl) {
    if (!canvasEl) return false;
    const ctx = canvasEl.getContext('2d');
    if (!ctx) return false;
    const pixels = ctx.getImageData(0, 0, canvasEl.width, canvasEl.height).data;
    for (let i = 3; i < pixels.length; i += 4) {
        if (pixels[i] !== 0) return true;
    }
    return false;
}

function triggerUpload() {
    if (mediaItems.length >= MAX_MEDIA_COUNT) {
        showToast('媒體上限為 10 張。');
        return;
    }
    document.getElementById('fileInput').click();
}
function handleUpload(e) {
    const files = Array.from(e.target.files || []);
    const remain = MAX_MEDIA_COUNT - mediaItems.length;
    if (remain <= 0) {
        showToast('媒體上限為 10 張。');
        e.target.value = '';
        return;
    }
    const selected = files.slice(0, remain);
    if (files.length > remain) showToast(`最多還可新增 ${remain} 張。`);
    if (selected.length === 0) {
        e.target.value = '';
        return;
    }
    const estimatedUploadBytes = selected.reduce((sum, file) => sum + estimateFileDataUrlBytes(file), 0);
    if (!canReserveStorage(estimatedUploadBytes)) {
        e.target.value = '';
        return;
    }
    Promise.all(selected.map(file => new Promise((resolve) => {
        const reader = new FileReader();
        reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '');
        reader.onerror = () => resolve('');
        reader.readAsDataURL(file);
    }))).then(results => {
        let blockedCount = 0;
        results.forEach(dataUrl => {
            if (dataUrl && mediaItems.length < MAX_MEDIA_COUNT) {
                if (!canReserveStorage(estimateDataUrlBytes(dataUrl), {
                    silentSoftWarning: true,
                    silentHardWarning: true
                })) {
                    blockedCount += 1;
                    return;
                }
                mediaItems.push({ kind: 'image', src: dataUrl });
            }
        });
        if (blockedCount > 0) showToast(`有 ${blockedCount} 張因儲存空間限制未加入。`);
        renderImages();
        markDirty();
    });
    e.target.value = '';
}
function updateImageAreaState() {
    const summary = getMediaSummary();
    const reachedLimit = summary.total >= MAX_MEDIA_COUNT;
    [document.getElementById('imageArea'), document.getElementById('imageAreaMood')].forEach(area => {
        if (!area) return;
        area.classList.toggle('is-disabled', reachedLimit);
    });
    document.querySelectorAll('[data-media-usage]').forEach(el => {
        el.textContent = `已使用 ${summary.total}/${MAX_MEDIA_COUNT} 張（圖片 ${summary.images} 張 / 繪圖 ${summary.drawings} 張）`;
    });
    refreshStorageWarning();
}
function renderImages() {
    const areas = [document.getElementById('imageArea'), document.getElementById('imageAreaMood')].filter(Boolean);
    areas.forEach(area => {
        area.querySelectorAll('.img-thumb-wrap').forEach(e => e.remove());
        mediaItems.forEach((item, i) => {
            const wrap = document.createElement('div');
            wrap.className = 'img-thumb-wrap';
            wrap.innerHTML = `<img class="img-thumb" src="${item.src}" title="${item.kind === 'drawing' ? 'drawing' : 'image'}"><div class="img-remove" onclick="removeImg(${i}, event)">✕</div>`;
            const input = area.querySelector('input');
            if (input) area.insertBefore(wrap, input);
            else area.appendChild(wrap);
        });
    });
    updateImageAreaState();
}
function removeImg(i, event) {
    if (event) event.stopPropagation();
    showDeleteConfirm('確定要刪除這個媒體項目嗎？', function () {
        mediaItems.splice(i, 1);
        renderImages();
        markDirty();
    });
}

// 共用刪除確認：所有編輯頁刪除動作都先經過此視窗。
function showDeleteConfirm(message, action) {
    pendingDeleteAction = typeof action === 'function' ? action : null;
    const text = document.getElementById('deleteModalText');
    if (text) text.textContent = `${message}\n移除後無法復原。`;
    document.getElementById('deleteModal')?.classList.add('show');
}

function confirmPendingDelete() {
    const action = pendingDeleteAction;
    pendingDeleteAction = null;
    hideModal('deleteModal');
    if (action) action();
}

function hideModal(id) {
    document.getElementById(id).classList.remove('show');
    if (id === 'deleteModal') pendingDeleteAction = null;
}
function submitDiaryForm(statusValue) {
    const form = document.getElementById('diaryForm');
    const selectedTagsCsv = document.getElementById('selectedTagsCsv');
    const selectedMoodIdsCsv = document.getElementById('selectedMoodIdsCsv');
    const deletedCustomTagsCsv = document.getElementById('deletedCustomTagsCsv');
    const templateTypeInput = document.getElementById('templateType');
    const diaryStatus = document.getElementById('diaryStatus');
    const bodyHidden = document.getElementById('bodyHidden');
    const diaryDateHidden = document.getElementById('diaryDateHidden');
    const mediaItemsJsonInput = document.getElementById('mediaItemsJson');

    if (!form || !templateTypeInput || !diaryStatus || !bodyHidden || !diaryDateHidden) return;

    templateTypeInput.value = currentTemplate;
    diaryStatus.value = statusValue;
    diaryDateHidden.value = toDateInputValue(selectedDate);
    ensureActiveCanvasCapturedBeforeSubmit();

    if (selectedTagsCsv) selectedTagsCsv.value = getSelectedTags().join(',');
    if (deletedCustomTagsCsv) deletedCustomTagsCsv.value = Array.from(deletedCustomTags).join(',');
    if (selectedMoodIdsCsv) selectedMoodIdsCsv.value = currentTemplate === 'mood' ? getSelectedMoodIds().join(',') : '';

    if (mediaItemsJsonInput) {
        mediaItemsJsonInput.value = JSON.stringify((mediaItems || []).map(item => ({
            kind: item && item.kind === 'drawing' ? 'drawing' : 'image',
            src: String(item?.src || '').trim()
        })).filter(item => item.src));
    }

    if (currentTemplate === 'mood') {
        const moodPayload = collectMoodPayload();
        bodyHidden.value = moodPayload.body || '';
    } else {
        bodyHidden.value = getBodyText().trim();
    }

    form.submit();
}

function saveDrawingV2() {
    if (mediaItems.length >= MAX_MEDIA_COUNT) {
        showToast('媒體上限為 10 張。');
        return;
    }
    const c = getActiveCanvas();
    if (!c || !hasCanvasContent(c)) {
        showToast('請先在繪圖區畫點內容。');
        return;
    }

    const exportCanvas = document.createElement('canvas');
    exportCanvas.width = c.width;
    exportCanvas.height = c.height;
    const exportCtx = exportCanvas.getContext('2d');
    if (!exportCtx) return;
    exportCtx.fillStyle = '#ffffff';
    exportCtx.fillRect(0, 0, exportCanvas.width, exportCanvas.height);
    exportCtx.drawImage(c, 0, 0);
    const dataUrl = exportCanvas.toDataURL('image/png');

    if (!canReserveStorage(estimateDataUrlBytes(dataUrl))) return;
    mediaItems.push({ kind: 'drawing', src: dataUrl });
    renderImages();
    showToast('繪圖已加入媒體區。');
    markDirty();
}

function ensureActiveCanvasCapturedBeforeSubmit() {
    const c = getActiveCanvas();
    if (!c || !hasCanvasContent(c)) return;
    if (mediaItems.length >= MAX_MEDIA_COUNT) return;

    const exportCanvas = document.createElement('canvas');
    exportCanvas.width = c.width;
    exportCanvas.height = c.height;
    const exportCtx = exportCanvas.getContext('2d');
    if (!exportCtx) return;
    exportCtx.fillStyle = '#ffffff';
    exportCtx.fillRect(0, 0, exportCanvas.width, exportCanvas.height);
    exportCtx.drawImage(c, 0, 0);
    const dataUrl = exportCanvas.toDataURL('image/png');
    if (!dataUrl) return;

    const isDuplicate = mediaItems.some(item => item && item.kind === 'drawing' && item.src === dataUrl);
    if (isDuplicate) return;

    if (!canReserveStorage(estimateDataUrlBytes(dataUrl), {
        silentSoftWarning: true,
        silentHardWarning: true
    })) {
        return;
    }

    mediaItems.push({ kind: 'drawing', src: dataUrl });
}

function showToast(msg) {
    const t = document.getElementById('toast');
    t.textContent = msg;
    t.classList.add('show');
    setTimeout(() => t.classList.remove('show'), 2500);
}

setStrokeColor(drawColor);
setStrokeSize(drawSize);
mediaItems = initialMediaItems.map(normalizeMediaItem).filter(Boolean).slice(0, MAX_MEDIA_COUNT);
renderImages();
updateImageAreaState();









