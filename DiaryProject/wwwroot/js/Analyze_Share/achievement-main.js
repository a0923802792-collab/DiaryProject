/**
 * achievement-main.js — 成就牆前端邏輯
 *
 * 資料流：
 *   DOMContentLoaded
 *     → loadAchievements()
 *       → GET /api/achievement
 *         → renderStats()   ← 更新上方三個統計數字
 *         → renderBadges()  ← 渲染 25 個成就徽章格
 *
 * ⚠️ 目前使用一般函式（非 ES Module），所有函式都是全域變數。
 *    未來建議改為 ES Module，與分享牆的 JS 架構保持一致：
 *      <script type="module" src="~/js/achievement-main.js"></script>
 *
 * 成就格樣式規則：
 *   isUnlocked = true  → class="badge-card unlocked"，顯示彩色 icon
 *   isUnlocked = false → class="badge-card locked"，icon 改成 🔒，顯示進度條
 *
 * 進度條顯示條件：
 *   maxProgress > 1 → 才顯示進度條（布林型成就不顯示進度條）
 */

/**
 * 頁面主入口：向後端取得成就資料並渲染
 *
 * 失敗處理：catch 後把 #achievement-wall 的內容換成錯誤提示，
 * 不讓畫面停在「載入中…」狀態。
 */
async function loadAchievements() {
    try {
        const res = await fetch('/api/achievement');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();

        renderStats(data);
        renderBadges(data.items);
    } catch (err) {
        const wall = document.getElementById('achievement-wall');
        if (wall) wall.innerHTML = '<div class="badge-loading" style="color:#c00">載入失敗，請重新整理</div>';
        console.error('achievement load failed', err);
    }
}

/**
 * 更新上方三個統計數字
 *
 * @param {{ unlockedCount: number, totalCount: number }} data - API 回傳的 AchievementResponse
 *
 * 更新的 DOM 元素：
 *   #unlocked-count ← 已解鎖數量
 *   #total-count    ← 總成就數（目前固定 25）
 *   #percent        ← 解鎖百分比，例如 "60%"
 */
function renderStats(data) {
    const pct = data.totalCount > 0
        ? Math.round(data.unlockedCount / data.totalCount * 100)
        : 0;

    setText('unlocked-count', data.unlockedCount);
    setText('total-count', data.totalCount);
    setText('percent', `${pct}%`);
}

/**
 * 渲染 25 個成就徽章格到 #achievement-wall
 *
 * @param {Array<{
 *   id: number,
 *   icon: string,
 *   name: string,
 *   description: string,
 *   isUnlocked: boolean,
 *   progress: number,
 *   maxProgress: number
 * }>} items - 後端回傳的 AchievementItem 陣列
 *
 * 渲染邏輯：
 *   1. isUnlocked = true  → 彩色徽章（badge-card unlocked），顯示 icon
 *   2. isUnlocked = false → 灰色鎖定（badge-card locked），icon 改 🔒
 *   3. 未解鎖 + maxProgress > 1 → 額外顯示進度條與 "N / Max" 文字
 *   4. title 屬性：滑鼠停留時顯示成就描述說明
 */
function renderBadges(items) {
    const wall = document.getElementById('achievement-wall');
    if (!wall) return;

    wall.innerHTML = items.map(item => {
        // 解鎖 → 彩色樣式；未解鎖 → 灰色鎖定樣式
        const cls = item.isUnlocked ? 'badge-card unlocked' : 'badge-card locked';
        // 解鎖 → 原本的 emoji；未解鎖 → 統一顯示 🔒
        const icon = item.isUnlocked ? item.icon : '🔒';
        // 進度百分比：最多 100%，避免超出設計範圍
        const pct = item.maxProgress > 1
            ? Math.min(100, Math.round(item.progress / item.maxProgress * 100))
            : 0;

        // 進度條 HTML：只在「未解鎖 + 有進度上限（非布林型）」時顯示
        const progressHtml = (!item.isUnlocked && item.maxProgress > 1) ? `
            <div class="badge-progress-wrap">
                <div class="badge-progress-bar" style="width:${pct}%"></div>
            </div>
            <div class="badge-progress-text">${item.progress} / ${item.maxProgress}</div>
        ` : '';

        return `
            <div class="${cls}" title="${item.description}">
                <div class="badge-icon">${icon}</div>
                <div class="badge-name">${item.name}</div>
                <div class="badge-desc">${item.description}</div>
                ${progressHtml}
            </div>
        `;
    }).join('');
}

/**
 * 安全地設定指定 id 元素的文字內容
 *
 * 為什麼用這個包裝？
 *   避免每次都要寫 document.getElementById + null 檢查，讓程式更簡潔。
 *
 * @param {string} id    - 目標元素的 id
 * @param {string|number} value - 要顯示的文字
 */
function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value;
}

// ES Module 以 type="module" 載入時自動 defer（等同於把整個檔案包在 DOMContentLoaded 裡）
// 所以不需要 addEventListener，直接呼叫即可
loadAchievements();
