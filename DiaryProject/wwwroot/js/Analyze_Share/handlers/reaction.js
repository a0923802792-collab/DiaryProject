/**
 * handlers/reaction.js — 按讚反應邏輯（互斥單選）
 *
 * 核心設計：每篇文只能選一種反應
 *   - 點新反應 → 舊反應數字 -1、新反應數字 +1
 *   - 點相同反應 → 不做任何事
 *   - rawData 中的 post.myReaction 是權威來源（由 server 回傳並即時更新）
 *   - localStorage 作為次要快取（供跨頁面保持狀態）
 */
import { rawData, currentDetailId } from '../store.js';
import { postReaction } from '../api.js';
import { renderDetail } from '../render/detail.js';
import { renderPostList } from '../render/postList.js';
import { getVisitorId } from '../identity.js';

// localStorage key：每位訪客一份，互不干擾
function getReactionKey() {
    return `sw_reacted_${getVisitorId()}`;
}

/**
 * 取得目前訪客對指定貼文的反應類型
 * 優先從 rawData（server 回傳）讀取，沒有則退回 localStorage 快取
 */
function getMyReaction(postId) {
    // 優先從 rawData（server 回傳的狀態）讀取
    const post = rawData.posts?.find(p => p.id === postId);
    if (post !== undefined) return post.myReaction ?? null;
    // 降為 localStorage 快取
    const data = JSON.parse(localStorage.getItem(getReactionKey()) || '{}');
    return data[postId] ?? null;
}

/**
 * 檢查目前訪客是否已對某篇貼文按了指定類型的反應
 */
export function hasReacted(postId, type) {
    return getMyReaction(postId) === type;
}

/**
 * 寫入反應記錄到 localStorage（快取用）
 */
function markReacted(postId, type) {
    const key  = getReactionKey();
    const data = JSON.parse(localStorage.getItem(key) || '{}');
    data[postId] = type;
    localStorage.setItem(key, JSON.stringify(data));
}

/**
 * 使用者按下反應按鈕
 *
 * 互斥邏輯：
 *   1. 已按相同反應 → 不做任何事（return）
 *   2. 已有其他反應 → 樂觀 -1
 *   3. 新增新反應 → 樂觀 +1
 *   反應類型同步寫入 post.myReaction 與 localStorage 快取，
 *   並在背景向後端 POST（後端以 UNIQUE(DiaryId, VisitorId) 保證 DB 一致性）
 */
export function addReaction(event, postId, type) {
    event.stopPropagation();

    const oldType = getMyReaction(postId);

    // 同一反應再按一次：不做任何事
    if (oldType === type) return;

    const post = rawData.posts.find(p => p.id === postId);
    if (!post) return;

    // 若有舊反應：樂觀 -1
    if (oldType !== null) {
        post.reactions[oldType] = Math.max(0, (post.reactions[oldType] || 0) - 1);
    }

    // 新反應：樂觀 +1，更新記憶體與快取
    post.reactions[type]++;
    post.myReaction = type;          // 更新 rawData 中的權威狀態
    markReacted(postId, type);       // 同步 localStorage 快取

    // 重渲染
    renderPostList(rawData.posts);
    if (currentDetailId === postId) renderDetail(post);

    // 背景 API（後端以 UNIQUE(DiaryId, VisitorId) 做 DB 層防重複與切換）
    postReaction(postId, type, getVisitorId());
}
