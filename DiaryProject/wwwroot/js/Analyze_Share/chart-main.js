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
import { renderAll, renderTaskList, renderTaskDetail } from './chart/render.js';

// ── 模組層級狀態 ──────────────────────────────────────────────
/** 最近一次從後端取回的完整資料，供分頁切換補繪使用 */
let _lastData     = null;
/** 任務歷史列表快取（只 fetch 一次） */
let _taskListData = null;

// ── 核心函式：載入圖表資料 ────────────────────────────────────
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

// ── 任務歷史：載入列表 ────────────────────────────────────────
async function loadTaskHistory() {
    if (_taskListData) {
        renderTaskList(_taskListData.tasks, loadTaskDetail);
        return;
    }
    const listEl = document.getElementById('th-task-list');
    if (listEl) listEl.innerHTML = '<div class="th-loading">⏳ 載入中…</div>';
    try {
        const resp = await fetch('/api/chart/task-list');
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        _taskListData = await resp.json();
        renderTaskList(_taskListData.tasks, loadTaskDetail);
    } catch (e) {
        console.error('任務列表載入失敗', e);
        const listEl2 = document.getElementById('th-task-list');
        if (listEl2) listEl2.innerHTML = '<div class="th-error">⚠️ 任務列表載入失敗，請重新整理</div>';
    }
}

// ── 任務歷史：載入單一任務詳細 ────────────────────────────────
async function loadTaskDetail(taskId, containerEl) {
    containerEl.innerHTML = '<div class="th-loading">⏳ 載入中…</div>';
    try {
        const resp = await fetch(`/api/chart/task-detail/${taskId}`);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const detail = await resp.json();
        renderTaskDetail(detail, containerEl);
    } catch (e) {
        console.error('任務詳細載入失敗', e);
        containerEl.innerHTML = '<div class="th-error">⚠️ 詳細資料載入失敗</div>';
    }
}

// ── 初始化（ES Module 自動 defer，DOM 一定已載入）────────────

// ── 分頁切換 ──────────────────────────────────────────────────
const _filterBar  = document.querySelector('.filter-bar');
const _filterInfo = document.getElementById('filter-info');

document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const target = btn.dataset.tab;
        document.querySelectorAll('.tab-panel').forEach(p => {
            p.style.display = p.id === `tab-${target}` ? '' : 'none';
        });

        if (target === 'history') {
            // 隱藏日期篩選器（不適用於任務歷史）
            if (_filterBar)  _filterBar.style.display  = 'none';
            if (_filterInfo) _filterInfo.style.display = 'none';
            loadTaskHistory();
        } else {
            // 恢復日期篩選器
            if (_filterBar)  _filterBar.style.display  = '';
            // requestAnimationFrame 確保 DOM 已顯示再補繪
            if (_lastData) requestAnimationFrame(() => renderAll(_lastData));
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

// ── 任務歷史篩選器 ────────────────────────────────────────────
function reRenderHistory() {
    if (_taskListData) renderTaskList(_taskListData.tasks, loadTaskDetail);
}

document.getElementById('th-search')?.addEventListener('input', reRenderHistory);

document.querySelectorAll('.th-filter').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.th-filter').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        reRenderHistory();
    });
});

document.querySelectorAll('.th-type').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.th-type').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        reRenderHistory();
    });
});

// ── 預設載入（全部時間範圍）──────────────────────────────────
setActivePreset('all');
loadChart(null, null, null);
