/**
 * render/postList.js — 渲染左側貼文列表
 *
 * 把後端回傳的 posts 陣列轉成 HTML 卡片，並寫入 #post-list 容器。
 * 每次 handleSearch() 取得新資料後都會重新呼叫此函式。
 */

function avatarText(nickname) {
    const name = String(nickname || '匿名').trim();
    return Array.from(name)[0] || '匿';
}

function escAttr(s) {
    return String(s ?? '')
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

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
                <div class="user-avatar">${avatarText(post.nickname)}</div>
                <span class="post-date">📅 ${post.date}</span>
                <span class="post-id">#${post.id}</span>
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
                <!-- 有圖才顯示第一張縮圖；多張時在右下角顯示總張數 -->
                ${post.images && post.images.length > 0
            ? `<div class="img-preview">
                    <img src="${post.images[0]}" alt="preview">
                    ${post.images.length > 1 ? `<span class="img-count-badge">${post.images.length}</span>` : ''}
                  </div>`
            : ''}
            </div>

<div class="post-actions">
    <!-- 各反應按鈕：已按過的加 disabled 防止重複，數字來自後端 -->
    <button class="reaction-btn" onclick="addReaction(event,${post.id},'like')"    ${post.myReaction === 'like'    ? 'disabled data-reacted' : ''}><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M720-120H280v-520l280-280 50 50q7 7 11.5 19t4.5 23v14l-44 174h258q32 0 56 24t24 56v80q0 7-2 15t-4 15L794-168q-9 20-30 34t-44 14Zm-360-80h360l120-280v-80H480l54-220-174 174v406Zm0-406v406-406Zm-80-34v80H160v360h120v80H80v-520h200Z"/></svg> ${post.reactions.like}</button>
    <button class="reaction-btn" onclick="addReaction(event,${post.id},'love')"    ${post.myReaction === 'love'    ? 'disabled data-reacted' : ''}><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M320-480v80q0 66 47 113t113 47q66 0 113-47t47-113v-80H320Zm160 180q-42 0-71-29t-29-71v-20h200v20q0 42-29 71t-71 29ZM272.5-652.5Q243-625 231-577l58 14q6-26 20-41.5t31-15.5q17 0 31 15.5t20 41.5l58-14q-12-48-41.5-75.5T340-680q-38 0-67.5 27.5Zm280 0Q523-625 511-577l58 14q6-26 20-41.5t31-15.5q17 0 31 15.5t20 41.5l58-14q-12-48-41.5-75.5T620-680q-38 0-67.5 27.5ZM324-111.5Q251-143 197-197t-85.5-127Q80-397 80-480t31.5-156Q143-709 197-763t127-85.5Q397-880 480-880t156 31.5Q709-817 763-763t85.5 127Q880-563 880-480t-31.5 156Q817-251 763-197t-127 85.5Q563-80 480-80t-156-31.5ZM480-480Zm227 227q93-93 93-227t-93-227q-93-93-227-93t-227 93q-93 93-93 227t93 227q93 93 227 93t227-93Z"/></svg> ${post.reactions.love}</button>
    <button class="reaction-btn" onclick="addReaction(event,${post.id},'hug')"     ${post.myReaction === 'hug'     ? 'disabled data-reacted' : ''}><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M592-379q49-39 63-101h-83q-12 27-37 43.5T480-420q-30 0-55-16.5T388-480h-83q14 62 63 101t112 39q63 0 112-39ZM405.5-554.5Q420-569 420-590t-14.5-35.5Q391-640 370-640t-35.5 14.5Q320-611 320-590t14.5 35.5Q349-540 370-540t35.5-14.5Zm220 0Q640-569 640-590t-14.5-35.5Q611-640 590-640t-35.5 14.5Q540-611 540-590t14.5 35.5Q569-540 590-540t35.5-14.5ZM480-120l-58-50q-101-88-167-152T150-437q-39-51-54.5-94T80-620q0-94 63-157t157-63q52 0 99 22t81 62q34-40 81-62t99-22q94 0 157 63t63 157q0 46-15.5 89T810-437q-39 51-105 115T538-170l-58 50Zm0-108q96-83 158-141t98-102.5q36-44.5 50-79t14-69.5q0-60-40-100t-100-40q-47 0-87 26.5T518-666h-76q-15-41-55-67.5T300-760q-60 0-100 40t-40 100q0 35 14 69.5t50 79Q260-427 322-369t158 141Zm0-266Z"/></svg> ${post.reactions.hug}</button>
    <button class="reaction-btn" onclick="addReaction(event,${post.id},'empathy')" ${post.myReaction === 'empathy' ? 'disabled data-reacted' : ''}><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="M250-320h60v-10q0-71 49.5-120.5T480-500q71 0 120.5 49.5T650-330v10h60v-10q0-96-67-163t-163-67q-96 0-163 67t-67 163v10Zm34-270q41-6 86.5-32t72.5-59l-46-38q-20 24-55.5 44T276-650l8 60Zm392 0 8-60q-30-5-65.5-25T563-719l-46 38q27 33 72.5 59t86.5 32ZM324-111.5Q251-143 197-197t-85.5-127Q80-397 80-480t31.5-156Q143-709 197-763t127-85.5Q397-880 480-880t156 31.5Q709-817 763-763t85.5 127Q880-563 880-480t-31.5 156Q817-251 763-197t-127 85.5Q563-80 480-80t-156-31.5ZM480-480Zm227 227q93-93 93-227t-93-227q-93-93-227-93t-227 93q-93 93-93 227t93 227q93 93 227 93t227-93Z"/></svg> ${post.reactions.empathy}</button>
    <button class="reaction-btn" onclick="addReaction(event,${post.id},'cheer')"   ${post.myReaction === 'cheer'   ? 'disabled data-reacted' : ''}><svg xmlns="http://www.w3.org/2000/svg" height="20px" viewBox="0 -960 960 960" width="20px" fill="currentColor"><path d="m422-232 207-248H469l29-227-185 267h139l-30 208ZM320-80l40-280H160l360-520h80l-40 320h240L400-80h-80Zm151-390Z"/></svg> ${post.reactions.cheer}</button>
</div>

        </article>
    `).join('');
    //<div class="post-actions">
    //    <!-- 各反應按鈕：已按過的加 disabled 防止重複，數字來自後端 -->
    //    <button class="reaction-btn" onclick="addReaction(event,${post.id},'like')"    ${hasReacted(post.id, 'like')    ? 'disabled data-reacted' : ''}><img src="./ICON/thumb_up_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"         class="reaction-icon" alt="like">    ${post.reactions.like}</button>
    //    <button class="reaction-btn" onclick="addReaction(event,${post.id},'peace')"   ${hasReacted(post.id, 'peace')   ? 'disabled data-reacted' : ''}><img src="/icons/sentiment_excited_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg" class="reaction-icon" alt="peace">   ${post.reactions.peace}</button>
    //    <button class="reaction-btn" onclick="addReaction(event,${post.id},'hug')"     ${hasReacted(post.id, 'hug')     ? 'disabled data-reacted' : ''}><img src="/icons/heart_smile_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"       class="reaction-icon" alt="hug">     ${post.reactions.hug}</button>
    //    <button class="reaction-btn" onclick="addReaction(event,${post.id},'empathy')" ${hasReacted(post.id, 'empathy') ? 'disabled data-reacted' : ''}><img src="/icons/sentiment_sad_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"    class="reaction-icon" alt="empathy"> ${post.reactions.empathy}</button>
    //    <button class="reaction-btn" onclick="addReaction(event,${post.id},'cheer')"   ${hasReacted(post.id, 'cheer')   ? 'disabled data-reacted' : ''}><img src="/icons/bolt_24dp_1F1F1F_FILL0_wght400_GRAD0_opsz24.svg"             class="reaction-icon" alt="cheer">   ${post.reactions.cheer}</button>
    //</div>
    if (append) {
        // 無限捲動：追加到列表底部，不影響已渲染的卡片
        postList.insertAdjacentHTML('beforeend', html);
    } else {
        // 第一頁或篩選變更：整批取代
        postList.innerHTML = html;
    }
}

