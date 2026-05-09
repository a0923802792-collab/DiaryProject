/**
 * render/categories.js — 渲染分類按鈕列
 *
 * 負責把 store 裡的 categories 陣列畫成 HTML 按鈕，
 * 並根據目前的 currentCategory 決定哪個按鈕要加 'active' 樣式。
 */
import { rawData, currentCategory } from '../store.js';
import { filterByCategory } from '../handlers/search.js';

/**
 * 重新產生分類按鈕列的 HTML 並寫入 DOM
 *
 * 「全部」按鈕固定排在最前面，後面接著後端回傳的分類清單。
 * 每次 handleSearch() 結束後都會呼叫此函式，確保 active 狀態正確。
 *
 * 為什麼用 innerHTML 而不是逐一 createElement？
 *   分類數量少（5～10 個），用 template literal 拼 HTML 字串最簡潔，
 *   效能差異可以忽略。若分類很多或需要細粒度更新，才考慮改用 DOM API。
 */
export function renderCategories() {
    const container = document.getElementById('category-container');
    // 找不到容器（例如不在 sharewall4 頁面）就直接離開，不報錯
    if (!container) return;

    // 「全部」按鈕：currentCategory 為 '全部' 時顯示 active
    const allOption = `<span class="tag ${currentCategory === '全部' ? 'active' : ''}"
        onclick="filterByCategory('全部')">全部</span>`;

    // 動態產生每個分類的按鈕，與 currentCategory 相符時加 active
    const cats = rawData.categories.map(cat =>
        `<span class="tag ${cat === currentCategory ? 'active' : ''}"
            onclick="filterByCategory('${cat}')">${cat}</span>`
    ).join('');

    // 一次性寫入，避免多次操作 DOM 造成重繪
    container.innerHTML = allOption + cats;
}
