/**
 * chart/render.js — 所有 Chart.js 圖表繪製邏輯
 *
 * 職責：
 *   - 把後端回傳的 JSON 資料，用 Chart.js 畫出各種圖表
 *   - 維護圖表實例快取（_charts），避免重建時記憶體洩漏
 *   - 提供 RangeSlider（底部區間拖拉器），讓使用者縮放 x 軸範圍
 *
 * 依賴：
 *   - Chart.js 全域變數 `Chart`（由 Chart.cshtml 的 CDN <script> 提供）
 *   - 注意：此模組是 ES Module，Chart.js 仍以 UMD/全域方式載入，
 *     兩者可以共存，只要 CDN script 在 type="module" 之前載入即可。
 *
 * 模組層級狀態（不對外暴露）：
 *   _charts  - Chart.js 實例快取（key=canvasId）
 *   _sliders - RangeSlider 實例快取（key=sliderId）
 *   _gran    - 目前粒度（'day'|'week'|'month'），由 renderAll 更新
 */

// ── 調色盤 ────────────────────────────────────────────────────
const PALETTE = [
    '#7c8cf8', '#f9a27a', '#5dd8b1', '#f7d96a',
    '#f27b9c', '#a78bfa', '#4dc9e6', '#f98b6e',
];

const GRADIENT_BLUE = { bg: 'rgba(124,140,248,0.15)', border: '#7c8cf8' };
const GRADIENT_GREEN = { bg: 'rgba(93,216,177,0.15)', border: '#5dd8b1' };

// ── 粒度對應標題 ──────────────────────────────────────────────
const GRAN_CFG = {
    day: { diary: '📅 每日日記篇數', task: '📅 每日打卡次數' },
    week: { diary: '📅 每週日記篇數', task: '📅 每週打卡次數' },
    month: { diary: '📅 每月日記篇數', task: '📅 每月打卡次數' },
};

// ── 模組層級狀態 ──────────────────────────────────────────────
/** @type {Object.<string, import('chart.js').Chart>} */
export const _charts = {};   // 對外開放給 RangeSlider._applyToChart 存取

const _sliders = {};         // RangeSlider 實例，每次 renderAll 時重建
let _gran = 'month';         // 目前粒度，由 renderAll 設定

// ══════════════════════════════════════════════════════════════
// ── 公開 API ──────────────────────────────────────────────────
// ══════════════════════════════════════════════════════════════

/**
 * 統一繪製所有圖表（日記頁 + 任務頁）
 *
 * 呼叫時機：每次 loadChart 取到新資料後
 * 特殊處理：任務頁面（#tab-task）可能是 display:none，
 *   需要暫時顯示才能正確計算 canvas 尺寸，繪製完再藏回去。
 *
 * @param {object} data - 後端 ChartController 回傳的完整物件
 */
export function renderAll(data) {
    _gran = data.granularity || 'month';

    // ── 日記區塊 ──
    try {
        fillSummary(data.summary);
        drawTimeSeries(data.timeSeries);
        drawTrend(data.timeSeries);
        drawTypeDonut(data.typeDistribution);
        drawCategory(data.category);
        drawMoodTrend(data.moodTrend);
        drawStressDist(data.stressDistribution);
        drawSleepDist(data.sleepDistribution);
    } catch (e) {
        console.error('[diary renderAll]', e);
    }

    // ── 任務區塊（隱藏面板需暫時顯示才能正確計算 canvas 尺寸）──
    fillTaskSummary(data.summary);
    const taskPanel = document.getElementById('tab-task');
    const wasHidden = taskPanel && taskPanel.style.display === 'none';
    if (wasHidden) {
        taskPanel.style.visibility = 'hidden';
        taskPanel.style.display = '';
    }
    try {
        drawTaskTimeSeries(data.taskTimeSeries);
        drawTaskPerTask(data.taskPerTask);
        drawTaskType(data.taskCheckinType);
    } catch (e) { console.error('[task renderAll]', e); }
    if (wasHidden) {
        taskPanel.style.display = 'none';
        taskPanel.style.visibility = '';
    }
}

// ══════════════════════════════════════════════════════════════
// ── 統計摘要填入 ──────────────────────────────────────────────
// ══════════════════════════════════════════════════════════════

/** 填入日記頁上方統計卡片數字 */
function fillSummary(s) {
    setText('s-total', s.totalDiaries);
    setText('s-normal', s.normalCount);
    setText('s-mood', s.moodCount);
    setText('s-energy', s.avgEnergy ?? '-');
    setText('s-stress', s.avgStress ?? '-');
    setText('s-sleep', s.avgSleep ?? '-');
}

/** 填入任務頁上方統計卡片數字 */
function fillTaskSummary(s) {
    setText('st-active', s.taskActive ?? '-');
    setText('st-total', s.taskTotalCheckin ?? '-');
    setText('st-complete', s.taskComplete ?? '-');
    setText('st-makeup', s.taskMakeup ?? '-');
}

/** 安全地設定 DOM 元素的文字（找不到元素時不報錯） */
function setText(id, val) {
    const el = document.getElementById(id);
    if (el) el.textContent = val ?? '-';
}

// ══════════════════════════════════════════════════════════════
// ── Chart.js 實例管理 ─────────────────────────────────────────
// ══════════════════════════════════════════════════════════════

/** 移除 canvas 旁邊的「載入中」轉圈，並回傳 canvas 元素 */
function clearLoading(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return null;
    const wrap = canvas.closest('.chart-wrap');
    wrap?.querySelector('.chart-loading')?.remove();
    return canvas;
}

/**
 * 銷毀舊的 Chart.js 實例並回傳 canvas 元素
 * 每次重繪前都要先銷毀，否則 Chart.js 會報「Canvas is already in use」
 */
function getCanvas(canvasId) {
    if (_charts[canvasId]) {
        _charts[canvasId].destroy();
        delete _charts[canvasId];
    }
    return clearLoading(canvasId);
}

/** 建立新 Chart.js 實例，儲存至快取後回傳 */
function makeChart(canvasId, config) {
    const canvas = getCanvas(canvasId);
    if (!canvas) return null;
    const chart = new Chart(canvas, config);
    _charts[canvasId] = chart;
    return chart;
}

// ── 通用 Options 工廠 ─────────────────────────────────────────
/**
 * 產生各圖表共用的 Chart.js options 基礎設定
 * @param {string}  unit       - tooltip 顯示單位，例如 '篇'|'次'|'天'
 * @param {boolean} showLegend - 是否顯示圖例（甜甜圈圖需要；長條圖通常不需要）
 */
function baseOptions(unit = '', showLegend = false) {
    return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: { display: showLegend },
            tooltip: {
                callbacks: { label: ctx => ` ${ctx.raw} ${unit}` }
            }
        },
        scales: {
            x: { grid: { color: 'rgba(0,0,0,.05)' }, ticks: { color: '#8a94a6' } },
            y: { grid: { color: 'rgba(0,0,0,.05)' }, ticks: { color: '#8a94a6' } }
        }
    };
}

// ══════════════════════════════════════════════════════════════
// ── 日記圖表繪製函式 ───────────────────────────────────────────
// ══════════════════════════════════════════════════════════════

/**
 * 日記時間序列（長條圖，自動依粒度切換「每日/每週/每月」標題）
 * 繪製完成後附加 RangeSlider，讓使用者可以縮放 x 軸
 */
function drawTimeSeries({ labels, data }) {
    const cfg = GRAN_CFG[_gran] || GRAN_CFG.month;
    const el = document.getElementById('title-timeseries');
    if (el) el.textContent = cfg.diary;

    const maxVal = Math.max(...data, 1);
    const opts = baseOptions('篇', false);
    opts.scales.y.suggestedMax = Math.ceil(maxVal * 1.3);
    opts.scales.y.min = 0;

    makeChart('chart-timeseries', {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: '篇數',
                data,
                backgroundColor: GRADIENT_BLUE.bg,
                borderColor: GRADIENT_BLUE.border,
                borderWidth: 2,
                borderRadius: 6,
            }]
        },
        options: opts
    });
    attachSlider('rs-timeseries', 'chart-timeseries', labels, data, '#7c8cf8');
}

/**
 * 日記類型比例（甜甜圈圖）
 * 區分「一般日記」與「情緒日記」
 */
function drawTypeDonut({ labels, data }) {
    makeChart('chart-type', {
        type: 'doughnut',
        data: {
            labels,
            datasets: [{
                data,
                backgroundColor: ['#7c8cf8', '#f9a27a'],
                borderWidth: 0,
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '60%',
            plugins: {
                legend: { position: 'bottom', labels: { font: { size: 13 }, padding: 16 } },
                tooltip: { callbacks: { label: ctx => ` ${ctx.label}：${ctx.raw} 篇` } }
            }
        }
    });
}

/**
 * 分類標籤分布（水平長條圖，Top 8）
 * x 軸最大值設為最大值的 2 倍，讓短的條也清晰可見
 */
function drawCategory({ labels, data }) {
    const maxVal = Math.max(...data, 1);
    const xMax = Math.ceil(maxVal * 2);
    const opts = baseOptions('篇', true);
    opts.indexAxis = 'y';
    opts.scales.x = { ...opts.scales.x, max: xMax };

    makeChart('chart-category', {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: '篇數',
                data,
                backgroundColor: PALETTE,
                borderRadius: 6,
                borderWidth: 0,
            }]
        },
        options: opts
    });
}

/**
 * 發文趨勢（折線圖，與 timeSeries 使用相同資料，呈現不同視覺）
 */
function drawTrend({ labels, data }) {
    const trendTitles = { day: '📈 每日發文趨勢', week: '📈 每週發文趨勢', month: '📈 發文趨勢' };
    const el = document.getElementById('title-trend');
    if (el) el.textContent = trendTitles[_gran] || trendTitles.month;

    const maxVal = Math.max(...data, 1);
    const opts = baseOptions('篇', false);
    opts.scales.y.suggestedMax = Math.ceil(maxVal * 1.3);
    opts.scales.y.min = 0;

    makeChart('chart-trend', {
        type: 'line',
        data: {
            labels,
            datasets: [{
                label: '篇數',
                data,
                borderColor: GRADIENT_GREEN.border,
                backgroundColor: GRADIENT_GREEN.bg,
                borderWidth: 2.5,
                pointRadius: 4,
                pointBackgroundColor: GRADIENT_GREEN.border,
                fill: true,
                tension: 0.35,
            }]
        },
        options: opts
    });
    attachSlider('rs-trend', 'chart-trend', labels, data, '#5dd8b1');
}

/**
 * 情緒三指數趨勢（折線圖）
 * 三條線：活力（綠）、壓力（橘）、睡眠（藍）
 * spanGaps: true → 沒有資料的日期不斷線，直接跳過
 */
function drawMoodTrend({ labels, energy, stress, sleep }) {
    makeChart('chart-mood-trend', {
        type: 'line',
        data: {
            labels,
            datasets: [
                { label: '⚡ 活力', data: energy, borderColor: '#5dd8b1', backgroundColor: 'transparent', borderWidth: 2.5, pointRadius: 4, tension: 0.35, spanGaps: true },
                { label: '😤 壓力', data: stress, borderColor: '#f9a27a', backgroundColor: 'transparent', borderWidth: 2.5, pointRadius: 4, tension: 0.35, spanGaps: true },
                { label: '😴 睡眠', data: sleep, borderColor: '#7c8cf8', backgroundColor: 'transparent', borderWidth: 2.5, pointRadius: 4, tension: 0.35, spanGaps: true },
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: true, position: 'top' },
                tooltip: { callbacks: { label: ctx => ` ${ctx.dataset.label}：${ctx.raw} / 10` } }
            },
            scales: {
                x: { grid: { color: 'rgba(0,0,0,.05)' }, ticks: { color: '#8a94a6' } },
                y: { min: 0, max: 10, grid: { color: 'rgba(0,0,0,.05)' }, ticks: { color: '#8a94a6', stepSize: 2 } }
            }
        }
    });
    attachSlider('rs-mood-trend', 'chart-mood-trend', labels, energy, '#5dd8b1');
}

/**
 * 壓力分布（長條圖，1-10 分各有幾天）
 * 顏色從暖橘漸層，分數越高顏色越深
 */
function drawStressDist({ labels, data }) {
    makeChart('chart-stress-dist', {
        type: 'bar',
        data: {
            labels,
            datasets: [{ label: '天數', data, backgroundColor: data.map((_, i) => `hsl(${20 + i * 14}, 80%, 65%)`), borderRadius: 6, borderWidth: 0 }]
        },
        options: {
            ...baseOptions('天', false),
            plugins: { ...baseOptions().plugins, tooltip: { callbacks: { label: ctx => ` 壓力 ${ctx.label} 分：${ctx.raw} 天` } } }
        }
    });
}

/**
 * 睡眠品質分布（長條圖，1-10 分各有幾天）
 * 顏色從淡藍到深藍漸層
 */
function drawSleepDist({ labels, data }) {
    makeChart('chart-sleep-dist', {
        type: 'bar',
        data: {
            labels,
            datasets: [{ label: '天數', data, backgroundColor: data.map((_, i) => `hsl(${200 + i * 10}, 70%, 60%)`), borderRadius: 6, borderWidth: 0 }]
        },
        options: {
            ...baseOptions('天', false),
            plugins: { ...baseOptions().plugins, tooltip: { callbacks: { label: ctx => ` 睡眠 ${ctx.label} 分：${ctx.raw} 天` } } }
        }
    });
}

// ══════════════════════════════════════════════════════════════
// ── 任務打卡圖表繪製函式 ──────────────────────────────────────
// ══════════════════════════════════════════════════════════════

/** 任務打卡時間序列（長條圖，自動切換日/週/月標題） */
function drawTaskTimeSeries({ labels, data }) {
    const cfg = GRAN_CFG[_gran] || GRAN_CFG.month;
    const el = document.getElementById('title-task-timeseries');
    if (el) el.textContent = cfg.task;

    makeChart('chart-task-timeseries', {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: '打卡次數',
                data,
                backgroundColor: 'rgba(93,216,177,0.2)',
                borderColor: '#5dd8b1',
                borderWidth: 2,
                borderRadius: 6,
            }]
        },
        options: baseOptions('次', false)
    });
    attachSlider('rs-task-timeseries', 'chart-task-timeseries', labels, data, '#5dd8b1');
}

/** 各任務打卡次數（水平長條圖），顯示每個習慣的累計打卡次數 */
function drawTaskPerTask({ labels, data }) {
    makeChart('chart-task-per-task', {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: '打卡次數',
                data,
                backgroundColor: PALETTE,
                borderRadius: 6,
                borderWidth: 0,
            }]
        },
        options: { ...baseOptions('次', true), indexAxis: 'y' }
    });
}

/** 打卡類型分布（甜甜圈圖）：正常打卡 vs 補打卡 */
function drawTaskType({ labels, data }) {
    makeChart('chart-task-type', {
        type: 'doughnut',
        data: {
            labels,
            datasets: [{
                data,
                backgroundColor: ['#5dd8b1', '#f9a27a'],
                borderWidth: 0,
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '60%',
            plugins: {
                legend: { position: 'bottom', labels: { font: { size: 13 }, padding: 16 } },
                tooltip: { callbacks: { label: ctx => ` ${ctx.label}：${ctx.raw} 次` } }
            }
        }
    });
}

// ══════════════════════════════════════════════════════════════
// ── RangeSlider — 底部區間拖拉器 ──────────────────────────────
//
// 仿股票軟體底部的灰色區間拖拉器，可左右拖動定義圖表可見範圍。
// 元件包含：
//   - 迷你預覽 canvas（顯示整體數據輪廓）
//   - 左右灰色遮罩（表示未選取的範圍）
//   - 左右把手（拖動改變起始/結束索引）
//   - 中間視窗（拖動整體移動選取區間）
// ══════════════════════════════════════════════════════════════

class RangeSlider {
    /**
     * @param {string}   containerId  - 放置 slider 的 DOM ID
     * @param {string}   chartId      - 對應的 Chart.js canvas ID
     * @param {string[]} labels       - 完整 x 軸標籤陣列
     * @param {number[]} previewData  - 用於迷你預覽的數值陣列
     * @param {string}   color        - 預覽圖的顏色（hex 或 rgb）
     */
    constructor(containerId, chartId, labels, previewData, color = '#7c8cf8') {
        this.container = document.getElementById(containerId);
        this.chartId = chartId;
        this.labels = labels;
        this.total = labels.length;

        // 資料太少（< 3 筆）不需要 slider
        if (!this.container || this.total < 3) return;

        // 起始 / 結束索引（0-based），預設顯示全部
        this.startIdx = 0;
        this.endIdx = this.total - 1;

        this._buildDOM();
        this._drawPreview(previewData, color);
        this._bindEvents();
        this._updatePositions();

        this.container.style.display = 'block';
    }

    /** 建立 slider 內部 DOM 結構 */
    _buildDOM() {
        this.container.innerHTML = `
            <canvas class="rs-preview"></canvas>
            <div class="rs-mask rs-mask-left"></div>
            <div class="rs-mask rs-mask-right"></div>
            <div class="rs-window"></div>
            <div class="rs-handle rs-handle-left"></div>
            <div class="rs-handle rs-handle-right"></div>
        `;
        this.maskL = this.container.querySelector('.rs-mask-left');
        this.maskR = this.container.querySelector('.rs-mask-right');
        this.window = this.container.querySelector('.rs-window');
        this.handleL = this.container.querySelector('.rs-handle-left');
        this.handleR = this.container.querySelector('.rs-handle-right');
        this.canvas = this.container.querySelector('.rs-preview');
    }

    /**
     * 在迷你 canvas 上畫類似股票成交量的長條預覽圖
     * 使用 devicePixelRatio 確保 Retina 螢幕清晰
     */
    _drawPreview(data, color) {
        const c = this.canvas;
        const rect = this.container.getBoundingClientRect();
        const dpr = window.devicePixelRatio || 1;
        c.width = rect.width * dpr;
        c.height = rect.height * dpr;
        const ctx = c.getContext('2d');
        ctx.scale(dpr, dpr);

        const w = rect.width;
        const h = rect.height;
        const max = Math.max(...data, 1);
        const barW = w / data.length;

        ctx.fillStyle = color;
        ctx.globalAlpha = 0.5;
        data.forEach((v, i) => {
            const barH = (v / max) * (h - 4);
            ctx.fillRect(i * barW + 1, h - barH - 2, Math.max(barW - 2, 1), barH);
        });
    }

    /** 綁定滑鼠/觸控拖拉事件（左把手、右把手、中間視窗） */
    _bindEvents() {
        let dragging = null; // 'left' | 'right' | 'window'
        let startX = 0, origStart = 0, origEnd = 0;

        const onStart = (type, e) => {
            e.preventDefault();
            dragging = type;
            startX = (e.touches ? e.touches[0] : e).clientX;
            origStart = this.startIdx;
            origEnd = this.endIdx;
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onEnd);
            document.addEventListener('touchmove', onMove, { passive: false });
            document.addEventListener('touchend', onEnd);
        };

        const onMove = (e) => {
            if (!dragging) return;
            e.preventDefault();
            const dx = (e.touches ? e.touches[0] : e).clientX - startX;
            const containerW = this.container.getBoundingClientRect().width;
            const dIdx = Math.round((dx / containerW) * this.total);

            if (dragging === 'left') {
                this.startIdx = Math.max(0, Math.min(origStart + dIdx, this.endIdx - 1));
            } else if (dragging === 'right') {
                this.endIdx = Math.max(this.startIdx + 1, Math.min(origEnd + dIdx, this.total - 1));
            } else { // 'window'
                const span = origEnd - origStart;
                let newStart = origStart + dIdx;
                let newEnd = origEnd + dIdx;
                if (newStart < 0) { newStart = 0; newEnd = span; }
                if (newEnd > this.total - 1) { newEnd = this.total - 1; newStart = newEnd - span; }
                this.startIdx = newStart;
                this.endIdx = newEnd;
            }

            this._updatePositions();
            this._applyToChart();
        };

        const onEnd = () => {
            dragging = null;
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onEnd);
            document.removeEventListener('touchmove', onMove);
            document.removeEventListener('touchend', onEnd);
        };

        this.handleL.addEventListener('mousedown', e => onStart('left', e));
        this.handleL.addEventListener('touchstart', e => onStart('left', e), { passive: false });
        this.handleR.addEventListener('mousedown', e => onStart('right', e));
        this.handleR.addEventListener('touchstart', e => onStart('right', e), { passive: false });
        this.window.addEventListener('mousedown', e => onStart('window', e));
        this.window.addEventListener('touchstart', e => onStart('window', e), { passive: false });
    }

    /** 根據 startIdx / endIdx 更新各 DOM 元素的位置與寬度 */
    _updatePositions() {
        const leftPct = (this.startIdx / this.total) * 100;
        const rightPct = ((this.total - 1 - this.endIdx) / this.total) * 100;
        const handleW = 12; // px，需與 CSS .rs-handle { width } 一致

        this.maskL.style.width = `calc(${leftPct}% + ${handleW}px)`;
        this.maskR.style.width = `calc(${rightPct}% + ${handleW}px)`;
        this.handleL.style.left = `calc(${leftPct}% - ${handleW / 2}px)`;
        this.handleR.style.right = `calc(${rightPct}% - ${handleW / 2}px)`;
        this.window.style.left = `calc(${leftPct}% + ${handleW / 2}px)`;
        this.window.style.right = `calc(${rightPct}% + ${handleW / 2}px)`;
    }

    /**
     * 將選取範圍套用到對應的 Chart.js 圖表
     * 用 label 字串設定 x.min / x.max，Chart.js 會自動對齊到最近的資料點
     * chart.update('none') = 不播動畫，立即更新，滑動時才不會卡頓
     */
    _applyToChart() {
        const chart = _charts[this.chartId];
        if (!chart) return;
        const xScale = chart.options.scales?.x;
        if (!xScale) return;
        xScale.min = this.labels[this.startIdx];
        xScale.max = this.labels[this.endIdx];
        chart.update('none');
    }
}

/**
 * 建立（或重建）一個 RangeSlider 並儲存至 _sliders 快取
 *
 * 每次 renderAll 後重建，確保 slider 的 labels/data 與最新圖表一致。
 *
 * @param {string}   sliderId - slider 容器的 DOM ID
 * @param {string}   chartId  - 對應圖表的 canvas ID
 * @param {string[]} labels   - x 軸標籤陣列
 * @param {number[]} data     - 預覽圖數值陣列
 * @param {string}   color    - 預覽顏色
 */
function attachSlider(sliderId, chartId, labels, data, color) {
    // 已有舊 slider → 清空容器讓建構子重建
    if (_sliders[sliderId]) {
        const el = document.getElementById(sliderId);
        if (el) el.innerHTML = '';
    }
    _sliders[sliderId] = new RangeSlider(sliderId, chartId, labels, data, color);
}
