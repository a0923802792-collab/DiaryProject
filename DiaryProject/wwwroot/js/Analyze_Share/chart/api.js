/**
 * chart/api.js — 圖表 API 請求
 *
 * 職責：只負責與後端 /api/chart 溝通，不知道資料要怎麼顯示。
 * 呼叫方（chart-main.js）拿到資料後再決定要怎麼處理。
 */

/**
 * 向後端 /api/chart 取得所有圖表所需資料（一次呼叫全部）
 *
 * 參數說明（三個互斥，優先順序：preset > from/to）：
 * @param {string|null} preset - 快速預設值：'week'|'month'|'3month'|'6month'|'year'
 *                               傳 null 代表「全部時間範圍」
 * @param {string|null} from   - 自訂起始日期，格式 'YYYY-MM-DD'
 * @param {string|null} to     - 自訂結束日期，格式 'YYYY-MM-DD'
 * @returns {Promise<object>}  - 後端 ChartController 回傳的完整 JSON 物件
 * @throws {Error}             - HTTP 狀態非 2xx 時丟出錯誤，由呼叫方 catch 處理
 */
export async function fetchChartData(preset, from, to) {
    const params = new URLSearchParams();
    if (preset) params.set('preset', preset);
    if (from) params.set('from', from);
    if (to) params.set('to', to);

    const url = '/api/chart' + (params.toString() ? '?' + params.toString() : '');
    const res = await fetch(url);
    if (!res.ok) throw new Error(`圖表 API 回傳 ${res.status}`);
    return res.json();
}
