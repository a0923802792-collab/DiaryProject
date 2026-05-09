/**
 * chart-main.js — 數據圖表頁面入口點（ES Module）
 *
 * 職責：
 *   1. 監聽篩選器 UI 事件（分頁切換、快速按鈕、自訂日期）
 *   2. 組裝篩選參數 → 呼叫 chart/api.js 取得資料
 *   3. 把資料傳給 chart/render.js 繪圖
 *   4. 維護「最近一次資料」（_lastData），供分頁切換時重繪使用
 *
 * 不包含：
 *   - 任何 Chart.js 繪圖邏輯（在 chart/render.js）
 *   - Filter UI 操作（在 chart/filter.js）
 *   - API fetch 細節（在 chart/api.js）
 *
 * 注意：Chart.js 以 UMD 格式透過 CDN 載入（掛在 window.Chart），
 *   此模組不需要 import Chart，只要確保 CDN <script> 在此模組之前載入即可。
 *   Chart.cshtml 中已設定正確的載入順序。
 */
import { fetchChartData } from './chart/api.js';
import {
    setActiveBtn, setActivePreset,
    clearActiveBtn, clearCustomInputs,
    updateFilterInfo
} from './chart/filter.js';
import { renderAll } from './chart/render.js';

// ── 模組層級狀態 ──────────────────────────────────────────────
/** 最近一次從後端取回的完整資料，供分頁切換補繪使用 */
let _lastData = null;

// ── 核心函式：載入圖表資料 ────────────────────────────────────

/**
 * 組裝篩選參數、向後端取資料、更新篩選提示、繪圖
 *
 * @param {string|null} preset - 快速預設：'week'|'month'|'3month'|'6month'|'year'|null
 * @param {string|null} from   - 自訂起始日期 'YYYY-MM-DD'
 * @param {string|null} to     - 自訂結束日期 'YYYY-MM-DD'
 */
async function loadChart(preset, from, to) {
    try {
        const data = await fetchChartData(preset, from, to);
        _lastData = data;
        updateFilterInfo(data.appliedRange);
        renderAll(data);
    } catch (e) {
        console.error('圖表資料載入失敗', e);
        document.querySelectorAll('.chart-loading').forEach(el => {
            el.innerHTML = '<span style="color:#e55">⚠️ 載入失敗，請開啟 F12 Console 查看錯誤</span>';
        });
    }
}

// ── 初始化（ES Module 自動 defer，DOM 一定已載入）────────────

// ── 分頁切換 ──────────────────────────────────────────────────
// 切換分頁時：隱藏其他面板 → 顯示目標面板 → 補繪圖表
// 補繪原因：canvas 在 display:none 時尺寸為 0，
//   Chart.js 若在那時繪製，會產生尺寸錯誤，需在顯示後重繪
document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const target = btn.dataset.tab;
        document.querySelectorAll('.tab-panel').forEach(p => {
            p.style.display = p.id === `tab-${target}` ? '' : 'none';
        });
        // requestAnimationFrame 確保 DOM 已完成顯示再補繪
        if (_lastData) {
            requestAnimationFrame(() => renderAll(_lastData));
        }
    });
});

// ── 快速篩選按鈕 ──────────────────────────────────────────────
document.querySelectorAll('.filter-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        setActiveBtn(btn);
        clearCustomInputs();
        loadChart(btn.dataset.preset || null, null, null);
    });
});

// ── 自訂日期套用 ──────────────────────────────────────────────
document.getElementById('filter-apply')?.addEventListener('click', () => {
    const from = document.getElementById('filter-from')?.value;
    const to = document.getElementById('filter-to')?.value;
    if (!from && !to) return; // 兩者都沒填就不送
    clearActiveBtn();
    loadChart(null, from || null, to || null);
});

// ── 清除篩選 ──────────────────────────────────────────────────
document.getElementById('filter-clear')?.addEventListener('click', () => {
    clearCustomInputs();
    setActivePreset('all');
    loadChart(null, null, null);
});

// ── 預設載入（全部時間範圍）──────────────────────────────────
setActivePreset('all');
loadChart(null, null, null);

