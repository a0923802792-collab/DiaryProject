/**
 * handlers/search.js — 搜尋、分類篩選、排序邏輯
 *
 * 這個檔案負責「使用者想看什麼」的所有決策：
 *   - 關鍵字輸入 → 重新查詢
 *   - 點分類標籤  → 切換分類 → 重新查詢
 *   - 點排序按鈕  → 切換排序 → 重新查詢
 *   - 點貼文卡片  → 顯示詳情（不重新查詢，直接從 rawData 找）
 *   - 往下滾動到底 → 自動載入下一頁（無限捲動）
 *
 * 所有篩選/排序都由「後端 SQL」處理，前端只負責組 Query String 送過去。
 */
import {
    rawData, setRawData,
    currentCategory, setCurrentCategory,
    currentSort, setCurrentSort,
    currentPage, setCurrentPage,
    isLoadingMore, setIsLoadingMore,
    hasMore, setHasMore,
} from '../store.js';
import { fetchFilteredPosts } from '../api.js';
import { renderCategories } from '../render/categories.js';
import { renderPostList } from '../render/postList.js';
import { renderDetail } from '../render/detail.js';
import { getVisitorId } from '../identity.js';

// IntersectionObserver 相關（模組層級，只初始化一次）
let _sentinel = null;
let _observer = null;

/**
 * 核心查詢函式 — 組裝目前的篩選條件後向後端要資料，並重新渲染整個頁面
 *
 * @param {number} page - 要載入的頁碼，預設 1（重新查詢）；> 1 為載入更多
 *
 * 呼叫時機：
 *   - 頁面初始化（main.js DOMContentLoaded）
 *   - 使用者打字搜尋（input 事件）
 *   - 切換分類 / 排序（filterByCategory / setSortOrder）
 *   - 按讚後（需要刷新列表的反應數）
 *   - 無限捲動觸發（IntersectionObserver 偵測到 sentinel 進入視窗）
 *
 * 流程：
 *   1. 讀取搜尋框文字、目前分類、目前排序 → 組成 URLSearchParams
 *   2. 呼叫 api.js 的 fetchFilteredPosts() 向後端送 GET 請求
 *   3. page=1 → 重置列表；page>1 → 追加到現有列表
 *   4. 重新查詢時收起詳情面板，點擊貼文後才顯示右側詳情
 */
export async function handleSearch(page = 1) {
    // 防止重複觸發（正在載入更多時不接受新的載入請求）
    if (isLoadingMore) return;

    // 讀取搜尋框，?.trim() 避免前後空白影響搜尋，?? '' 確保不為 null
    const q = document.getElementById('search-input')?.value?.trim() ?? '';

    // 組裝 Query String，sort 是必填；category / q / visitorId 有值才加
    const params = new URLSearchParams({ sort: currentSort });
    if (q) params.set('q', q);
    if (currentCategory && currentCategory !== '全部') params.set('category', currentCategory);
    params.set('visitorId', getVisitorId());

    // 分頁參數
    params.set('page', page);
    params.set('pageSize', 10);

    // 載入更多時顯示載入中提示
    if (page > 1) {
        setIsLoadingMore(true);
        showLoadMoreSpinner(true);
    }

    try {
        const data = await fetchFilteredPosts(params);

        // 更新分頁狀態
        setHasMore(data.hasMore);
        setCurrentPage(page);

        if (page === 1) {
            // 第一頁：重置列表
            setRawData(data);
            renderCategories();
            renderPostList(data.posts, false);
            // 自動顯示第一篇文章詳情
            if (data.posts.length > 0) {
                renderDetail(data.posts[0]);
            } else {
                hideDetail();
            }
        } else {
            // 後續頁：追加到現有列表
            rawData.posts = [...rawData.posts, ...data.posts];
            renderPostList(data.posts, true);
        }

        // 確保 sentinel 已建立（用於無限捲動偵測）
        ensureScrollSentinel();
    } catch (err) {
        console.error('handleSearch error:', err);
    } finally {
        setIsLoadingMore(false);
        showLoadMoreSpinner(false);
    }
}

/**
 * 切換分類篩選
 *
 * 邏輯：
 *   - 點已選取的分類 → 取消篩選（回到「全部」）
 *   - 點「全部」      → 取消篩選
 *   - 點其他分類      → 切換到該分類
 *
 * @param {string} category - 分類名稱，或 '全部'
 */
export function filterByCategory(category) {
    // 點同一個分類或點「全部」都代表取消篩選
    const next = (category === currentCategory || category === '全部')
        ? '全部'
        : category;
    setCurrentCategory(next);
    handleSearch(1); // 切換分類時重置到第 1 頁
}

/**
 * 切換排序方式，並更新按鈕的 active 樣式
 *
 * @param {'latest'|'hot'} order
 */
export function setSortOrder(order) {
    setCurrentSort(order);
    // toggle('active', condition) 等於：condition 為 true 時加 class，否則移除
    document.getElementById('sort-latest')?.classList.toggle('active', order === 'latest');
    document.getElementById('sort-hot')?.classList.toggle('active', order === 'hot');
    handleSearch(1); // 切換排序時重置到第 1 頁
}

/**
 * 點擊貼文卡片後顯示詳情
 *
 * 不重新打 API，直接從 rawData（已在記憶體的資料）找到對應貼文後渲染。
 * 這樣可以立即回應點擊，不需等待網路。
 *
 * @param {number} id - 貼文 DiaryId
 */
export function showDetail(id) {
    const post = rawData.posts.find(p => p.id === id);
    if (post) renderDetail(post);
}

function hideDetail() {
    document.getElementById('sharewall-layout')?.classList.remove('has-detail');
    const detailPanel = document.getElementById('detail-panel');
    if (!detailPanel) return;
    detailPanel.classList.remove('is-visible');
    detailPanel.innerHTML = '';
}

// ── 無限捲動輔助函式 ────────────────────────────────────────

/**
 * 在貼文列表底部放一個 1px 高的 sentinel 元素，
 * 當它進入使用者的可視範圍時，自動觸發載入下一頁。
 */
function ensureScrollSentinel() {
    if (_sentinel) return; // 只建立一次

    _sentinel = document.createElement('div');
    _sentinel.id = 'scroll-sentinel';
    _sentinel.style.height = '1px';

    // 把 sentinel 放在 post-list 容器的父元素末尾
    const postList = document.getElementById('post-list');
    postList?.parentElement?.appendChild(_sentinel);

    _observer = new IntersectionObserver(entries => {
        if (entries[0].isIntersecting && hasMore && !isLoadingMore) {
            handleSearch(currentPage + 1);
        }
    }, { threshold: 0.1 });

    _observer.observe(_sentinel);
}

/**
 * 顯示/隱藏「載入中…」提示
 */
function showLoadMoreSpinner(show) {
    let el = document.getElementById('load-more-spinner');
    if (!el) {
        el = document.createElement('div');
        el.id = 'load-more-spinner';
        el.style.cssText = 'text-align:center;padding:16px;color:#aaa;font-size:13px;display:none;';
        el.textContent = '載入中…';
        document.getElementById('post-list')?.after(el);
    }
    el.style.display = show ? 'block' : 'none';
}
