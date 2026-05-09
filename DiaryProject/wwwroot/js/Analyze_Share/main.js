/**
 * main.js — 整個 JS 應用的入口點
 *
 * 這個檔案只做兩件事：
 *   1. 把需要被 HTML onclick 屬性呼叫的函式「掛到 window 上」
 *   2. 等 DOM 準備好後，綁定事件監聽器，然後執行第一次查詢
 *
 * 為什麼需要掛到 window？
 *   ES Module 預設是「嚴格作用域」，模組內的函式外部無法直接存取。
 *   但 cshtml 裡的 onclick="filterByCategory('感情')" 是直接在 HTML 屬性上呼叫，
 *   瀏覽器執行時會去找 window.filterByCategory，找不到就報錯。
 *   所以這裡手動把需要的函式掛到 window，讓 HTML 屬性能呼叫到。
 *
 * 為什麼用 DOMContentLoaded？
 *   確保 HTML 元素（#search-input、#sort-latest 等）都已存在後再綁定事件，
 *   避免 getElementById 找不到元素而回傳 null。
 */
import { handleSearch, filterByCategory, setSortOrder, showDetail } from './handlers/search.js';
import { addReaction } from './handlers/reaction.js';

/**
 * 把函式掛到 window，讓 cshtml 裡的 onclick 屬性可以直接呼叫
 * 例如：onclick="showDetail(123)" → 等同於 window.showDetail(123)
 */
window.filterByCategory = filterByCategory;
window.setSortOrder = setSortOrder;
window.showDetail = showDetail;
window.addReaction = addReaction;

document.addEventListener('DOMContentLoaded', () => {
    // 搜尋框：使用者每次輸入都觸發查詢（即時搜尋，不需按 Enter）
    // 明確傳入 page=1，確保搜尋時重置分頁
    document.getElementById('search-input')
        ?.addEventListener('input', () => handleSearch(1));

    // 排序按鈕：點擊後切換排序並重新查詢
    document.getElementById('sort-latest')
        ?.addEventListener('click', () => setSortOrder('latest'));
    document.getElementById('sort-hot')
        ?.addEventListener('click', () => setSortOrder('hot'));

    // 頁面初始化：執行第一次查詢，載入預設（最新排序、無篩選）的貼文列表
    handleSearch(1);
});
