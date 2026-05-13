/* ============================================================
   日記詳情頁 diary_detail.js
   ------------------------------------------------------------
   功能：
   1. 圖片預覽
   2. 分享 / 刪除確認視窗
   3. 編輯按鈕連結
   4. 依照「日記列表頁目前篩選後的順序」修正上一篇 / 下一篇
   ------------------------------------------------------------
   注意：
   不要再把返回按鈕強制改成 /Diary/DiaryList，
   否則會把列表頁篩選條件洗掉。
   ============================================================ */

const serverDiaryId = Number(document.getElementById('diaryId')?.value || 0);

let previewItems = [];
let previewIndex = 0;

/* ============================================================
   詳情頁上一篇 / 下一篇：依照列表頁目前順序
   ------------------------------------------------------------
   diary_index.js 會在點卡片時，把「目前篩選後的日記 ID 順序」
   存到 sessionStorage。
   詳情頁讀取這份順序後，就能做到：
   下一篇 = 列表頁目前看到的下一張卡片
   ============================================================ */

function getCurrentQueryString() {
    // 保留目前網址上的篩選參數，例如 templateType、visibility、keyword、tags、period...
    return window.location.search || '';
}

function buildDetailUrlById(id) {
    // 詳情頁切換時，保留原本的 query string，避免篩選條件消失。
    return `/Diary/DiaryDetail?id=${encodeURIComponent(id)}${getCurrentQueryString().replace(/^\?id=[^&]*&?/, '?')}`;
}

function getDetailUrlPreserveQuery(id) {
    const params = new URLSearchParams(window.location.search);

    // 先移除舊的 id，再放入新的 id。
    params.delete('id');
    params.set('id', String(id));

    return `/Diary/DiaryDetail?${params.toString()}`;
}

function applyPrevNextFromListOrder() {
    if (!serverDiaryId) return;

    const prevBtn = document.getElementById('prevBtn');
    const nextBtn = document.getElementById('nextBtn');

    // 如果畫面沒有上一篇 / 下一篇按鈕，就不用處理。
    if (!prevBtn && !nextBtn) return;

    let ids = [];
    try {
        ids = JSON.parse(sessionStorage.getItem('diaryListNavIds') || '[]');
    } catch {
        ids = [];
    }

    // 如果沒有從列表頁帶來的順序，就維持後端 ViewBag 產生的連結。
    if (!Array.isArray(ids) || ids.length === 0) return;

    ids = ids
        .map(x => Number(x))
        .filter(x => Number.isFinite(x) && x > 0);

    const currentIndex = ids.indexOf(serverDiaryId);

    // 如果目前這篇不在篩選後清單中，也維持後端原本連結。
    if (currentIndex < 0) return;

    const prevId = currentIndex > 0 ? ids[currentIndex - 1] : null;
    const nextId = currentIndex < ids.length - 1 ? ids[currentIndex + 1] : null;

    if (prevBtn) {
        if (prevId) {
            // 如果原本是 <a>，直接更新 href。
            if (prevBtn.tagName.toLowerCase() === 'a') {
                prevBtn.href = getDetailUrlPreserveQuery(prevId);
                prevBtn.classList.remove('disabled');
            }
        } else {
            disableNavButton(prevBtn);
        }
    }

    if (nextBtn) {
        if (nextId) {
            if (nextBtn.tagName.toLowerCase() === 'a') {
                nextBtn.href = getDetailUrlPreserveQuery(nextId);
                nextBtn.classList.remove('disabled');
            }
        } else {
            disableNavButton(nextBtn);
        }
    }
}

function disableNavButton(el) {
    // 如果 Razor 已經輸出 button disabled，這裡不用改。
    if (el.tagName.toLowerCase() === 'button') {
        el.disabled = true;
        return;
    }

    // 如果 Razor 輸出的是 a，但前端判斷沒有上一筆 / 下一筆，
    // 就移除 href，並加上 disabled 樣式，避免使用者點擊。
    el.removeAttribute('href');
    el.classList.add('disabled');
    el.setAttribute('aria-disabled', 'true');
}

/* ============================================================
   圖片預覽
   ============================================================ */

function initPreviewItems() {
    previewItems = Array.from(document.querySelectorAll('#imagesGrid .img-thumb')).map(el => {
        const img = el.querySelector('img');
        const kind = el.dataset.kind || (img ? 'image' : 'text');
        const value = (el.dataset.previewValue || img?.getAttribute('src') || el.textContent || '').trim();
        return { kind, value };
    }).filter(item => item.value);
}

function updatePreviewNavState() {
    const prevBtn = document.getElementById('previewPrevBtn');
    const nextBtn = document.getElementById('previewNextBtn');
    if (!prevBtn || !nextBtn) return;

    const hasItems = previewItems.length > 0;
    prevBtn.disabled = !hasItems || previewIndex <= 0;
    nextBtn.disabled = !hasItems || previewIndex >= previewItems.length - 1;
}

function renderPreview() {
    if (previewItems.length === 0) return;

    previewIndex = Math.max(0, Math.min(previewIndex, previewItems.length - 1));
    const current = previewItems[previewIndex];
    const display = document.getElementById('previewDisplay');
    const counter = document.getElementById('previewCounter');

    if (!display || !counter) return;

    if (current.kind === 'image') {
        display.innerHTML = `<img src="${current.value}" alt="日記圖片預覽">`;
    } else {
        display.textContent = current.value;
    }

    counter.textContent = `${previewIndex + 1} / ${previewItems.length}`;
    document.querySelectorAll('#imagesGrid .img-thumb').forEach((el, i) => {
        el.classList.toggle('thumb-active', i === previewIndex);
    });
    updatePreviewNavState();
}

function openPreview(index) {
    previewIndex = index;
    renderPreview();
}

function shiftPreview(dir) {
    if (previewItems.length === 0) return;
    previewIndex += dir;
    renderPreview();
}

function initPreviewFromRenderedThumbs() {
    if (!document.querySelector('#imagesGrid .img-thumb')) {
        updatePreviewNavState();
        return;
    }

    initPreviewItems();
    if (previewItems.length > 0) {
        previewIndex = 0;
        renderPreview();
    } else {
        updatePreviewNavState();
    }
}

/* ============================================================
   共用視窗與刪除確認
   ============================================================ */

function hideModal(id) {
    document.getElementById(id)?.classList.remove('show');
}

function showShareConfirmModal() {
    const shareBtn = document.getElementById('shareBtn');
    const text = document.getElementById('shareConfirmText');
    const visibility = shareBtn?.dataset.visibility || '';
    const willShare = visibility !== '已分享';

    if (text) {
        text.textContent = willShare
            ? '確定要將這篇日記設為分享嗎？'
            : '這篇日記目前為分享狀態，確定要改回私人嗎？';
    }

    document.getElementById('shareConfirmModal')?.classList.add('show');
}

function confirmDiaryShare() {
    document.getElementById('shareDiaryForm')?.submit();
}

function showDiaryDeleteModal() {
    document.getElementById('deleteModal')?.classList.add('show');
}

function confirmDiaryDelete() {
    document.getElementById('deleteDiaryForm')?.submit();
}

function showToast(msg) {
    const toast = document.getElementById('toast');
    if (!toast) return;

    toast.textContent = msg;
    toast.classList.add('show');
    setTimeout(() => toast.classList.remove('show'), 2500);
}

/* ============================================================
   頁面初始化
   ============================================================ */

const editDiaryBtn = document.getElementById('editDiaryBtn');
if (editDiaryBtn && serverDiaryId > 0) {
    editDiaryBtn.href = `/Diary/DiaryEdit?id=${serverDiaryId}`;
}

const emptyEditBtn = document.getElementById('emptyEditBtn');
if (emptyEditBtn && serverDiaryId > 0) {
    emptyEditBtn.href = `/Diary/DiaryEdit?id=${serverDiaryId}`;
}

applyPrevNextFromListOrder();
initPreviewFromRenderedThumbs();
