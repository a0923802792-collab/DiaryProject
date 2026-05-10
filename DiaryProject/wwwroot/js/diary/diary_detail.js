/* ============================================================
   日記詳情頁
   - 由後端 Razor 輸出日記內容。
   - 本檔只負責返回連結、圖片預覽、刪除確認視窗與提示訊息。
   ============================================================ */

const serverDiaryId = Number(document.getElementById('diaryId')?.value || 0);
const BACK_URL = '/Diary/DiaryList';

const backBtn = document.getElementById('backBtn');
if (backBtn) backBtn.href = BACK_URL;

let previewItems = [];
let previewIndex = 0;

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

initPreviewFromRenderedThumbs();