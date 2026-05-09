/**
 * store.js — 共用狀態集中管理
 *
 * 為什麼需要這個檔案？
 *   ES Module 的每個 .js 檔案都是獨立的作用域，
 *   如果各模組直接宣告自己的變數，彼此之間看不到對方的資料。
 *   把「需要跨模組共享的狀態」集中在這裡，其他模組統一 import，
 *   就能確保所有人讀到同一份資料，不會各自為政。
 *
 * 設計規則：
 *   - 每個狀態提供一個 getter（export let）和一個 setter（set 函式）
 *   - 不直接對外開放「可任意修改」的物件，要改值必須呼叫 setter
 *   - 這樣做的好處：日後要在 setter 裡加 log 或驗證，只改一個地方
 */

/**
 * 從後端 API 取回的完整資料（分類清單 + 貼文陣列）
 * 初始值為空，等 handleSearch() 第一次呼叫後才有內容
 */
export let rawData = { categories: [], posts: [] };
export function setRawData(data) { rawData = data; }

/**
 * 目前使用者選取的分類篩選條件
 * '全部' 代表不篩選，傳給 API 時不帶 category 參數
 */
export let currentCategory = '全部';
export function setCurrentCategory(v) { currentCategory = v; }

/**
 * 目前的排序方式
 *   'latest' → 依建立日期由新到舊
 *   'hot'    → 依總反應數由多到少
 */
export let currentSort = 'latest';
export function setCurrentSort(v) { currentSort = v; }

/**
 * 目前右側詳情面板顯示的貼文 ID
 * 用來判斷按讚後是否需要同步更新詳情面板
 * null 代表尚未選取任何貼文
 */
export let currentDetailId = null;
export function setCurrentDetailId(v) { currentDetailId = v; }

/**
 * 是否為第一次載入
 * true  → 資料回來後自動把第一篇顯示在詳情面板
 * false → 之後每次篩選/排序不強制切換詳情
 * 呼叫 clearFirstLoad() 將其設為 false（只執行一次）
 */
export let isFirstLoad = true;
export function clearFirstLoad() { isFirstLoad = false; }

// ── 分頁狀態（無限捲動） ─────────────────────────────────────

/** 目前載入到第幾頁 */
export let currentPage = 1;
export function setCurrentPage(v) { currentPage = v; }

/** 是否正在載入更多（防止重複觸發） */
export let isLoadingMore = false;
export function setIsLoadingMore(v) { isLoadingMore = v; }

/** 後端是否還有更多資料 */
export let hasMore = false;
export function setHasMore(v) { hasMore = v; }
