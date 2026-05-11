/**
 * handlers/reaction.js — 按讚反應邏輯（互斥單選）
 *
 * 核心設計：每篇文只能選一種反應
 *   - 點新反應 → 舊反應數字 -1、新反應數字 +1
 *   - 點相同反應 → 不做任何事
 *   - localStorage 格式：{ postId: "like" }（每篇只存一個字串）
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
 * 取得目前訪客對指定貼文的反應類型（沒按過則回傳 null）
 */
function getMyReaction(postId) {
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
 * 寫入反應記錄到 localStorage
 */
function markReacted(postId, type) {
    const key  = getReactionKey();
    const data = JSON.parse(localStorage.getItem(key) || '{}');
    data[postId] = type;
    localStorage.setItem(key, JSON.stringify(data));
}

/**
 * 清除 localStorage 中某篇貼文的反應記錄
 */
function unmarkReacted(postId) {
    const key  = getReactionKey();
    const data = JSON.parse(localStorage.getItem(key) || '{}');
    delete data[postId];
    localStorage.setItem(key, JSON.stringify(data));
}

/**
 * 使用者按下反應按鈕
 *
 * 互斥邏輯：
 *   1. 已按相同反應 → 不做任何事（return）
 *   2. 已有其他反應 → 樂觀 -1 + 清除舊記錄（背景不需要額外 API，後端 UPSERT 自己處理）
 *   3. 新增新反應 → 樂觀 +1 + 寫入記錄 + 背景 API
 */
export function addReaction(event, postId, type) {
    event.stopPropagation();

    const oldType = getMyReaction(postId);

    // 同一反應再按一次：不做任何事
    if (oldType === type) return;

    const post = rawData.posts.find(p => p.id === postId);
    if (!post) return;

    // 若有舊反應：樂觀 -1 並清除前端記錄
    if (oldType !== null) {
        post.reactions[oldType] = Math.max(0, (post.reactions[oldType] || 0) - 1);
        unmarkReacted(postId);
    }

    // 新反應：樂觀 +1
    post.reactions[type]++;
    markReacted(postId, type);

    // 重渲染
    renderPostList(rawData.posts);
    if (currentDetailId === postId) renderDetail(post);

    // 背景 API（UPSERT：後端會自動處理舊反應 -1、新反應 +1）
    postReaction(postId, type, getVisitorId());
}
