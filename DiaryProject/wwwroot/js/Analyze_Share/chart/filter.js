/**
 * chart/filter.js — 時間篩選器 UI 輔助函式
 *
 * 負責所有與「時間篩選按鈕列」相關的 DOM 操作。
 * 不知道「篩選後要載入哪些資料」，那是 chart-main.js 的責任。
 */

/**
 * 設定指定快速篩選按鈕為 active（同時取消其他所有按鈕的 active 狀態）
 * @param {HTMLElement} btn - 被點擊的 .filter-btn 元素
 */
export function setActiveBtn(btn) {
    document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
}

/**
 * 依 preset 字串，把對應的快速按鈕設為 active
 *
 * 按鈕上有 data-preset 屬性（如 data-preset="week"）。
 * 沒有 data-preset 或 data-preset="all" 的按鈕，對應 preset='all'。
 *
 * @param {string} preset - 例如 'all'|'week'|'month'|'3month'|'6month'|'year'
 */
export function setActivePreset(preset) {
    document.querySelectorAll('.filter-btn').forEach(b => {
        b.classList.toggle('active', (b.dataset.preset || 'all') === preset);
    });
}

/**
 * 移除所有快速篩選按鈕的 active 狀態（輸入自訂日期時使用）
 */
export function clearActiveBtn() {
    document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
}

/**
 * 清空自訂日期輸入框（#filter-from 與 #filter-to）
 */
export function clearCustomInputs() {
    const f = document.getElementById('filter-from');
    const t = document.getElementById('filter-to');
    if (f) f.value = '';
    if (t) t.value = '';
}

/**
 * 更新「目前顯示範圍」提示文字（#filter-info）
 *
 * 有 from 或 to 時顯示範圍說明；否則隱藏整個提示。
 *
 * @param {{ from: string|null, to: string|null }|null} range - 後端回傳的 appliedRange 物件
 */
export function updateFilterInfo(range) {
    const el = document.getElementById('filter-info');
    if (!el) return;
    if (range?.from || range?.to) {
        const f = range.from ?? '起始';
        const t = range.to ?? '今日';
        el.textContent = `📅 目前顯示範圍：${f} ～ ${t}`;
        el.style.display = 'block';
    } else {
        el.style.display = 'none';
    }
}
