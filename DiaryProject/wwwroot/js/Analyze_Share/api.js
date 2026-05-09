/**
 * api.js — 所有後端 HTTP 呼叫集中於此
 *
 * 為什麼要獨立成一個檔案？
 *   如果每個地方都自己寫 fetch(...)，之後 API 網址或格式一改，
 *   就要到處找、到處改。集中在這裡，改一個地方就全部生效。
 *
 * 使用相對路徑 /api/... 的原因：
 *   這個 JS 是由同一個 ASP.NET MVC 伺服器提供的，
 *   相對路徑不綁死 port，開發/測試/上線環境都不需要改程式碼。
 */

/**
 * 向後端查詢分享牆資料（含篩選條件）
 *
 * 範例呼叫：
 *   const data = await fetchFilteredPosts(new URLSearchParams({ sort: 'hot', category: '感情' }));
 *   // → GET /api/sharewall4?sort=hot&category=感情
 *
 * @param {URLSearchParams} params - 查詢參數（sort / category / q）
 * @returns {Promise<{categories: string[], posts: object[]}>}
 * @throws {Error} HTTP 狀態非 2xx 時丟出錯誤，由呼叫方 catch 處理
 */
export async function fetchFilteredPosts(params) {
    const r = await fetch(`/api/sharewall4?${params}`);
    if (!r.ok) throw new Error(`API 回傳 ${r.status}`);
    return r.json();
}

/**
 * 送出按讚反應到後端（fire-and-forget 模式）
 *
 * 什麼是 fire-and-forget？
 *   按下按鈕後，前端「樂觀更新」（立刻在畫面上加 1），
 *   同時悄悄把資料送到後端，不等後端回應、不擋住畫面。
 *   就算網路斷線送失敗，畫面已經更新，體驗不受影響。
 *   失敗只會在 console 印出警告，不影響使用者操作。
 *
 * @param {number} postId     - 目標貼文 ID（對應 Diary.DiaryId）
 * @param {string} type       - 反應類型：like | peace | hug | empathy | cheer
 * @param {string} visitorId  - 來自 identity.js 的身份 ID（匿名 UUID 或真實 userId）
 *                              後端用此欄位做 DB 層防重複（PostReactionLog UNIQUE 約束）
 */
export function postReaction(postId, type, visitorId) {
    fetch('/api/sharewall4/react', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ diaryId: postId, reactionType: type, visitorId }),
    }).catch(err => console.warn('reaction sync failed', err));
}
