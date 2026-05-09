/**
 * identity.js — 「我是誰」的唯一來源
 *
 * ┌─────────────────────────────────────────────────────────────┐
 * │  擴充性設計說明                                              │
 * │                                                             │
 * │  現在（無登入）：產生 / 讀取一組匿名 UUID 存在 localStorage  │
 * │  未來（有登入）：只需在這裡改成讀取伺服器回傳的 userId       │
 * │                  其他所有檔案完全不需要修改                  │
 * └─────────────────────────────────────────────────────────────┘
 *
 * 使用方式：
 *   import { getVisitorId } from '../identity.js';
 *   const id = getVisitorId(); // → "anon-xxxxxxxx-xxxx-..."  或  "user-123"
 */

const VISITOR_KEY = 'sw_visitor_id';

/**
 * 取得目前使用者的識別 ID。
 *
 * 優先順序：
 *   1. 若伺服器已注入登入使用者資訊（window.__currentUser），回傳 "user-{id}"
 *   2. 否則從 localStorage 讀取（或產生）匿名 UUID，回傳 "anon-{uuid}"
 *
 * ★ 未來加入登入系統時，只需在後端 Layout 注入：
 *      <script>window.__currentUser = { id: "@User.FindFirstValue(...)" };</script>
 *   這裡就會自動切換到真實使用者 ID，localStorage 那條路不會再走。
 *
 * @returns {string}
 */
export function getVisitorId() {
    // ── 未來擴充點：有登入資訊就用真實 userId ──────────────────
    const loggedIn = window.__currentUser?.id;
    if (loggedIn) return `user-${loggedIn}`;
    // ────────────────────────────────────────────────────────────

    // 無登入：讀取或產生匿名 UUID
    let id = localStorage.getItem(VISITOR_KEY);
    if (!id) {
        id = 'anon-' + crypto.randomUUID();
        localStorage.setItem(VISITOR_KEY, id);
    }
    return id;
}
