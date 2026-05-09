/**
 * render/postList.js — 渲染左側貼文列表
 *
 * 把後端回傳的 posts 陣列轉成 HTML 卡片，並寫入 #post-list 容器。
 * 每次 handleSearch() 取得新資料後都會重新呼叫此函式。
 */
import { hasReacted } from '../handlers/reaction.js';

/**
 * 重新渲染貼文列表
 *
 * 渲染邏輯：
 *   - 無貼文且非追加 → 顯示「沒有找到相關日記」提示
 *   - 有貼文 → 用 map() 把每篇 post 物件轉成 <article> HTML 字串
 *   - append=false（第 1 頁）→ 整批取代 innerHTML
 *   - append=true（第 2 頁以後）→ 用 insertAdjacentHTML 追加到底部
 *
 * 關於反應按鈕的 disabled 處理：
 *   hasReacted() 會去查 localStorage，若按過就在按鈕上加 disabled 屬性和降低透明度，
 *   讓使用者知道「這個已經按過了」。
 *
 * 關於 onclick 寫在 HTML 屬性上的原因：
 *   innerHTML 產生的 DOM 無法直接綁 addEventListener，
 *   所以改用 onclick 屬性呼叫掛在 window 上的函式（在 main.js 設定）。
 *
 * @param {object[]} posts  - 後端回傳的貼文陣列，預設為空陣列
 * @param {boolean}  append - true 時追加到列表底部（無限捲動用），false 時重置列表
 */
export function renderPostList(posts = [], append = false) {
    const postList = document.getElementById('post-list');
    if (!postList) return;

    // 空結果且非追加：顯示提示訊息就結束
    if (!append && posts.length === 0) {
        postList.innerHTML = '<div class="no-result">沒有找到相關日記 😅</div>';
        return;
    }

    // 把每篇貼文轉成 HTML 卡片字串
    const html = posts.map(post => `
        <article class="post-card" onclick="showDetail(${post.id})">
            <div class="post-meta">
                <div class="user-avatar"></div>
                <span class="post-date">📅 ${post.date}</span>
            </div>
            <div class="post-content">
                <div class="text-area">
                    <h3>${post.title}</h3>
                    <!-- 列表只顯示前 50 字預覽，完整內容在詳情面板 -->
                    <p>${post.content.substring(0, 50)}...</p>
                    <div class="post-tags">
                        <span class="post-tag">#${post.category}</span>
                        ${post.tags.map(t => `<span class="post-tag">${t}</span>`).join('')}
                    </div>
                </div>
                <!-- 有圖才顯示第一張縮圖，無圖不佔空間 -->
                ${post.images && post.images.length > 0
            ? `<div class="img-preview"><img src="${post.images[0]}" alt="preview"></div>`
            : ''}
            </div>
            <div class="post-actions">
                <!-- 回應按鈕：stopPropagation 防止觸發卡片的 showDetail -->
                <button onclick="event.stopPropagation()">回應</button>
                <!-- 各反應按鈕：已按過的加 disabled 防止重複，數字來自後端 -->
                <button onclick="addReaction(event,${post.id},'like')"    ${hasReacted(post.id, 'like') ? 'disabled style="opacity:.45"' : ''}>👍 ${post.reactions.like}</button>
                <button onclick="addReaction(event,${post.id},'peace')"   ${hasReacted(post.id, 'peace') ? 'disabled style="opacity:.45"' : ''}>😌 ${post.reactions.peace}</button>
                <button onclick="addReaction(event,${post.id},'hug')"     ${hasReacted(post.id, 'hug') ? 'disabled style="opacity:.45"' : ''}>🤗 ${post.reactions.hug}</button>
                <button onclick="addReaction(event,${post.id},'empathy')" ${hasReacted(post.id, 'empathy') ? 'disabled style="opacity:.45"' : ''}>🥺 ${post.reactions.empathy}</button>
                <button onclick="addReaction(event,${post.id},'cheer')"   ${hasReacted(post.id, 'cheer') ? 'disabled style="opacity:.45"' : ''}>💪 ${post.reactions.cheer}</button>
            </div>
        </article>
    `).join('');

    if (append) {
        // 無限捲動：追加到列表底部，不影響已渲染的卡片
        postList.insertAdjacentHTML('beforeend', html);
    } else {
        // 第一頁或篩選變更：整批取代
        postList.innerHTML = html;
    }
}

