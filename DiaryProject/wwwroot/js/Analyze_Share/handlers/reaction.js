/**
 * handlers/reaction.js — 按讚反應邏輯
 *
 * 核心設計：「樂觀更新 + 雙層防重複」
 *
 * 樂觀更新（Optimistic Update）：
 *   使用者按下按鈕後，不等後端回應，立刻把畫面上的數字 +1。
 *   同時在背景呼叫 API 把資料存進資料庫。
 *
 * 雙層防重複：
 *   第一層（前端）：localStorage 記錄已按過的反應，讓按鈕立即變 disabled。
 *   第二層（後端）：DB 的 PostReactionLog 用 UNIQUE 約束阻擋重複插入，
 *                  就算使用者清除 localStorage 或換瀏覽器，DB 也不會多計一次。
 *
 * 擴充性：
 *   身份識別統一由 identity.js 提供，localStorage key 與 visitorId 綁定。
 *   未來加入登入系統，只需修改 identity.js，此檔案不需要改。
 */
import { rawData, currentDetailId } from '../store.js';
import { postReaction } from '../api.js';
import { renderDetail } from '../render/detail.js';
import { renderPostList } from '../render/postList.js';
import { getVisitorId } from '../identity.js';

// localStorage key 前綴，加上 visitorId 讓不同使用者的記錄互相隔離
// 儲存格式：{ "123": { "like": true }, "456": { "hug": true } }
function getReactionKey() {
    return `sw_reacted_${getVisitorId()}`;
}

/**
 * 檢查目前使用者是否已對某篇貼文的某種反應按過讚（前端快取查詢）
 *
 * @param {number} postId - 貼文 ID
 * @param {string} type   - 反應類型
 * @returns {boolean}
 */
export function hasReacted(postId, type) {
    const data = JSON.parse(localStorage.getItem(getReactionKey()) || '{}');
    return !!(data[postId]?.[type]);
}

/**
 * 把「已按讚」記錄寫入 localStorage（前端快取，讓按鈕立即變 disabled）
 *
 * @param {number} postId - 貼文 ID
 * @param {string} type   - 反應類型
 */
export function markReacted(postId, type) {
    const key = getReactionKey();
    const data = JSON.parse(localStorage.getItem(key) || '{}');
    if (!data[postId]) data[postId] = {};
    data[postId][type] = true;
    localStorage.setItem(key, JSON.stringify(data));
}

/**
 * 使用者按下反應按鈕時的完整處理流程
 *
 * 步驟：
 *   1. 阻止點擊事件冒泡（避免觸發卡片的 onclick → showDetail）
 *   2. 檢查是否已按過，若是則直接返回（防重複）
 *   3. 在記憶體內把該反應數 +1（樂觀更新）
 *   4. 把「已按」寫入 localStorage
 *   5. 重新渲染貼文列表（讓卡片上的數字更新）
 *   6. 若詳情面板正在顯示這篇，也同步更新詳情
 *   7. 在背景呼叫 API 把反應存進資料庫（fire-and-forget）
 *
 * @param {MouseEvent} event  - 點擊事件（用來呼叫 stopPropagation）
 * @param {number}     postId - 目標貼文 ID
 * @param {string}     type   - 反應類型：like | peace | hug | empathy | cheer
 */
export function addReaction(event, postId, type) {
    // 阻止冒泡，避免同時觸發卡片的 showDetail
    event.stopPropagation();

    // 已按過就不做任何事（防止前端重複計算）
    if (hasReacted(postId, type)) return;

    // 從記憶體（store 的 rawData）找到這篇貼文
    const post = rawData.posts.find(p => p.id === postId);
    if (post) {
        // 樂觀更新：直接修改記憶體內的數字，不等後端
        post.reactions[type]++;

        // 記錄已按，之後重整頁面按鈕仍是 disabled
        markReacted(postId, type);

        // 直接用記憶體資料重渲染列表，不重打 API（避免蓋掉樂觀更新的數字）
        renderPostList(rawData.posts);

        // 若右側詳情面板正在顯示這篇，也要同步更新反應數
        if (currentDetailId === postId) renderDetail(post);

        // 背景送 API（fire-and-forget），把反應寫入資料庫
        // visitorId 由 identity.js 提供，後端用來做 DB 層防重複
        postReaction(postId, type, getVisitorId());
    }
}
