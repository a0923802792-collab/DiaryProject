using Microsoft.AspNetCore.Mvc;
using DiaryProject.Infrastructure;

namespace DiaryProject.Controllers;

[ApiController]
[Route("api/chart")]
public sealed class ChartController(IConfiguration configuration) : ControllerBase
{
    // 日記 DB 連線字串（透過 DatabaseFactory 備管連線字串建立權責）
    private readonly string _connectionString = DatabaseFactory.GetConnectionString(configuration);
    private readonly string _taskConnectionString = DatabaseFactory.GetEmotionTaskConnectionString(configuration);

    // 暫用固定 UserId（正式登入後改為從 Session / Claims 取得）
    private const int DemoUserId = 1;

    /// <summary>
    /// 取得所有圖表所需資料（一次呼叫全部）
    /// GET /api/chart?preset=week|month|year  （快速預設）
    /// GET /api/chart?from=2025-01-01&amp;to=2025-12-31  （自訂範圍）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? preset,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        var range = BuildRange(preset, from, to);

        var timeSeriesTask = ChartRepository.GetTimeSeriesCountAsync(_connectionString, range, ct, DemoUserId);
        var typeTask = ChartRepository.GetTypeDistributionAsync(_connectionString, range, ct, DemoUserId);
        var categoryTask = ChartRepository.GetCategoryDistributionAsync(_connectionString, range, ct, DemoUserId);
        var moodTrendTask = ChartRepository.GetMoodTrendAsync(_connectionString, range, ct, DemoUserId);
        var moodAvgTask = ChartRepository.GetMoodAverageAsync(_connectionString, range, ct, DemoUserId);
        var stressDistTask = ChartRepository.GetStressDistributionAsync(_connectionString, range, ct, DemoUserId);
        var sleepDistTask = ChartRepository.GetSleepDistributionAsync(_connectionString, range, ct, DemoUserId);
        var energyDistTask = ChartRepository.GetEnergyDistributionAsync(_connectionString, range, ct, DemoUserId);

        // ── 任務相關查詢（EmotionTaskDB）──
        var taskTimeSeriesTask = TaskRepository.GetTimeSeriesCheckinAsync(_taskConnectionString, range, ct, DemoUserId);
        var taskPerTaskTask = TaskRepository.GetPerTaskCheckinAsync(_taskConnectionString, range, ct, DemoUserId);
        var taskTypeTask       = TaskRepository.GetCheckinTypeDistAsync(_taskConnectionString, range, ct, DemoUserId);
        var taskSummaryTask    = TaskRepository.GetTaskSummaryAsync(_taskConnectionString, range, ct, DemoUserId);
        var taskWeekdayTask    = TaskRepository.GetCheckinWeekdayDistAsync(_taskConnectionString, range, ct, DemoUserId);
        var taskHourTask       = TaskRepository.GetCheckinHourDistAsync(_taskConnectionString, range, ct, DemoUserId);
        var taskRhythmTask     = TaskRepository.GetRhythmTypeDistAsync(_taskConnectionString, ct, DemoUserId);
        var taskWeeklyGoalTask = TaskRepository.GetWeekdayCompletionRateAsync(_taskConnectionString, range, ct, DemoUserId);

        await Task.WhenAll(timeSeriesTask, typeTask, categoryTask,
                           moodTrendTask, moodAvgTask, stressDistTask, sleepDistTask, energyDistTask,
                           taskTimeSeriesTask, taskPerTaskTask, taskTypeTask, taskSummaryTask,
                           taskWeekdayTask, taskHourTask, taskRhythmTask, taskWeeklyGoalTask);

        var timeSeries = range.FillGaps(await timeSeriesTask);
        var type = await typeTask;
        var category = await categoryTask;
        var moodTrend = await moodTrendTask;
        var moodAvg = await moodAvgTask;
        var stressDist = await stressDistTask;
        var sleepDist = await sleepDistTask;
        var energyDist = await energyDistTask;

        var taskTimeSeries  = range.FillGaps(await taskTimeSeriesTask);
        var taskPerTask    = await taskPerTaskTask;
        var taskType       = await taskTypeTask;
        var taskSummary    = await taskSummaryTask;
        var weekdayDist    = await taskWeekdayTask;
        var hourDist       = await taskHourTask;
        var rhythmDist     = await taskRhythmTask;
        var weeklyGoalRows = await taskWeeklyGoalTask;

        // 打卡星期分布：DATEPART(WEEKDAY) 1=週日…7=週六，對應標籤順序
        var weekdayLabels = new[] { "日", "一", "二", "三", "四", "五", "六" };
        var weekdayData   = Enumerable.Range(1, 7).Select(i => weekdayDist.GetValueOrDefault(i, 0)).ToArray();

        // 打卡時段分布：0–23 時
        var hourLabels = Enumerable.Range(0, 24).Select(i => $"{i:D2}:00").ToArray();
        var hourData   = Enumerable.Range(0, 24).Select(i => hourDist.GetValueOrDefault(i, 0)).ToArray();

        // 週目標達成率：固定以週一～週日呈現完成率
        var weeklyGoalDict = weeklyGoalRows.ToDictionary(r => r.Weekday, r => r.CompletionRate);
        var weeklyGoalLabels = new[] { "週一", "週二", "週三", "週四", "週五", "週六", "週日" };
        var weeklyGoalRates = Enumerable.Range(1, 7).Select(i => weeklyGoalDict.GetValueOrDefault(i, 0)).ToArray();

        var stressData = Enumerable.Range(1, 10).Select(i => stressDist.GetValueOrDefault(i, 0)).ToArray();
        var sleepData = Enumerable.Range(1, 10).Select(i => sleepDist.GetValueOrDefault(i, 0)).ToArray();
        var energyData = Enumerable.Range(1, 10).Select(i => energyDist.GetValueOrDefault(i, 0)).ToArray();
        var scaleLabels = Enumerable.Range(1, 10).Select(i => i.ToString()).ToArray();

        return Ok(new
        {
            // ── 套用的範圍與粒度（給前端顯示用）──
            appliedRange = new
            {
                from = range.DateFrom?.ToString("yyyy-MM-dd"),
                to = range.DateTo?.ToString("yyyy-MM-dd"),
            },
            granularity = range.Granularity, // "day" | "week" | "month"
            // ── 日記時間序列（合併原本的 monthly + weekly）──
            timeSeries = new
            {
                labels = timeSeries.Select(x => x.Label).ToArray(),
                data = timeSeries.Select(x => x.Count).ToArray()
            },
            typeDistribution = new
            {
                labels = type.Keys.Select(k => k == "normal" ? "一般日記" : "情緒日記").ToArray(),
                data = type.Values.ToArray()
            },
            category = new
            {
                labels = category.Select(x => x.Tag).ToArray(),
                data = category.Select(x => x.Count).ToArray()
            },
            moodTrend = BuildMoodTrendResponse(range, moodTrend),
            moodAverage = new
            {
                avgEnergy = moodAvg.AvgEnergy,
                avgStress = moodAvg.AvgStress,
                avgSleep = moodAvg.AvgSleep,
                count = moodAvg.Count,
            },
            stressDistribution = new { labels = scaleLabels, data = stressData },
            sleepDistribution = new { labels = scaleLabels, data = sleepData },
            energyDistribution = new { labels = scaleLabels, data = energyData },
            // ── 任務區塊（合併原本的 taskMonthly + taskWeekly）──
            taskTimeSeries = new
            {
                labels = taskTimeSeries.Select(x => x.Label).ToArray(),
                data = taskTimeSeries.Select(x => x.Count).ToArray()
            },
            taskPerTask = new
            {
                labels          = taskPerTask.Select(x => x.Title).ToArray(),
                data            = taskPerTask.Select(x => x.TotalCheckins).ToArray(),
                completionRates = taskPerTask.Select(x => x.CompletionRate).ToArray(),
                // ── 排行前 / 後三名 ──────────────────────────────────
                rankings = BuildTaskRankings(taskPerTask)
            },
            taskCheckinType = new
            {
                labels = new[] { "正常打卡", "補打卡" },
                data   = new[]
                {
                    taskType.GetValueOrDefault("Complete", 0),
                    taskType.Where(kv => kv.Key != "Complete").Sum(kv => kv.Value)
                }
            },
            taskWeekdayDist = new { labels = weekdayLabels, data = weekdayData },
            taskHourDist    = new { labels = hourLabels,    data = hourData },
            taskRhythmDist  = new
            {
                labels = rhythmDist.Keys
                    .Select(k => k == "Daily" ? "每日任務" : "非每日任務").ToArray(),
                data = rhythmDist.Values.ToArray()
            },
            taskWeeklyGoal  = new
            {
                labels = weeklyGoalLabels,
                data = weeklyGoalRates
            },
            summary = new
            {
                totalDiaries = timeSeries.Sum(x => x.Count),
                normalCount = type.GetValueOrDefault("normal", 0),
                moodCount = type.GetValueOrDefault("mood", 0),
                totalCategories = category.Count,
                avgEnergy = moodAvg.AvgEnergy,
                avgStress = moodAvg.AvgStress,
                avgSleep = moodAvg.AvgSleep,
                // 任務摘要
                taskActive = taskSummary.ActiveTasks,
                taskTotalCheckin = taskSummary.TotalCheckins,
                taskComplete = taskSummary.CompleteCount,
                taskMakeup = taskSummary.MakeupCount,
            }
        });
    }

    // ── 情緒趨勢對齊完整日期範圍 ────────────────────────────────────
    private static object BuildMoodTrendResponse(
        ChartDateRange range, List<ChartRepository.MoodTrendRow> raw)
    {
        var allLabels = range.GenerateDateLabels();
        if (allLabels.Count == 0)
        {
            // 沒有日期範圍時，直接回傳原始資料
            return new
            {
                labels = raw.Select(x => x.Date).ToArray(),
                energy = raw.Select(x => (int?)x.Energy).ToArray(),
                stress = raw.Select(x => (int?)x.Stress).ToArray(),
                sleep = raw.Select(x => (int?)x.Sleep).ToArray(),
            };
        }

        // 同一天可能有多筆（多篇 mood 日記），用第一筆（已按時間排序）
        var dict = new Dictionary<string, ChartRepository.MoodTrendRow>();
        foreach (var r in raw)
            dict.TryAdd(r.Date, r);

        var energy = new List<int?>();
        var stress = new List<int?>();
        var sleep = new List<int?>();
        foreach (var label in allLabels)
        {
            if (dict.TryGetValue(label, out var row))
            {
                energy.Add(row.Energy);
                stress.Add(row.Stress);
                sleep.Add(row.Sleep);
            }
            else
            {
                energy.Add(null);
                stress.Add(null);
                sleep.Add(null);
            }
        }

        return new
        {
            labels = allLabels.ToArray(),
            energy = energy.ToArray(),
            stress = stress.ToArray(),
            sleep = sleep.ToArray(),
        };
    }

    // ── 任務歷史列表 ──────────────────────────────────────────────────
    /// <summary>
    /// 取得全部任務（含匯總統計，不依日期篩選）
    /// GET /api/chart/task-list
    /// </summary>
    [HttpGet("task-list")]
    public async Task<IActionResult> GetTaskList(CancellationToken ct)
    {
        var rows = await TaskRepository.GetAllTaskListAsync(_taskConnectionString, ct, DemoUserId);
        return Ok(new
        {
            tasks = rows.Select(r => new
            {
                taskId           = r.TaskId,
                title            = r.Title,
                rhythmType       = r.RhythmType,
                status           = r.Status,
                createdAt        = r.CreatedAt.ToString("yyyy-MM-dd"),
                totalCheckins    = r.TotalCheckins,
                completeCheckins = r.CompleteCheckins,
                makeupCheckins   = r.MakeupCheckins,
                undoCheckins     = r.UndoCheckins,
                weeklyTarget     = r.WeeklyTarget,
                startDate        = r.StartDate?.ToString("yyyy-MM-dd"),
                completionRate   = r.CompletionRate,
            }).ToArray()
        });
    }

    /// <summary>
    /// 取得單一任務詳細打卡紀錄
    /// GET /api/chart/task-detail/{taskId}
    /// </summary>
    [HttpGet("task-detail/{taskId:int}")]
    public async Task<IActionResult> GetTaskDetail(int taskId, CancellationToken ct)
    {
        var (info, logs) = await TaskRepository.GetTaskDetailAsync(_taskConnectionString, taskId, ct);
        if (info is null) return NotFound();
        return Ok(new
        {
            taskId           = info.TaskId,
            title            = info.Title,
            rhythmType       = info.RhythmType,
            status           = info.Status,
            createdAt        = info.CreatedAt.ToString("yyyy-MM-dd"),
            totalCheckins    = info.TotalCheckins,
            completeCheckins = info.CompleteCheckins,
            makeupCheckins   = info.MakeupCheckins,
            undoCheckins     = info.UndoCheckins,
            weeklyTarget     = info.WeeklyTarget,
            startDate        = info.StartDate?.ToString("yyyy-MM-dd"),
            completionRate   = info.CompletionRate,
            logs             = logs.Select(l => new
            {
                date      = l.Date.ToString("yyyy-MM-dd"),
                type      = l.Type,
                checkinAt = l.CheckinAt.ToString("yyyy-MM-dd HH:mm"),
            }).ToArray()
        });
    }

    // ── 解析日期範圍 ──────────────────────────────────────────────────
    private static ChartDateRange BuildRange(string? preset, string? from, string? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // preset 優先
        if (!string.IsNullOrWhiteSpace(preset))
            return preset.ToLower() switch
            {
                "week" => new ChartDateRange { DateFrom = today.AddDays(-6), DateTo = today },
                "month" => new ChartDateRange { DateFrom = today.AddMonths(-1), DateTo = today },
                "3month" => new ChartDateRange { DateFrom = today.AddMonths(-3), DateTo = today },
                "6month" => new ChartDateRange { DateFrom = today.AddMonths(-6), DateTo = today },
                "year" => new ChartDateRange { DateFrom = today.AddYears(-1), DateTo = today },
                _ => new ChartDateRange()
            };

        // 自訂範圍
        DateOnly? dateFrom = DateOnly.TryParse(from, out var f) ? f : null;
        DateOnly? dateTo = DateOnly.TryParse(to, out var t) ? t : null;
        return new ChartDateRange { DateFrom = dateFrom, DateTo = dateTo };
    }

    // ── 計算各任務排行前 / 後三名（次數 + 完成率）──────────────────────
    private static object BuildTaskRankings(List<TaskRepository.PerTaskRow> rows)
    {
        // 只排計算有打卡記錄的任務（total > 0）進完成率排行
        var withCheckins = rows.Where(r => r.TotalCheckins > 0).ToList();

        static object ToRankItem(TaskRepository.PerTaskRow r) => new
        {
            title      = r.Title,
            count      = r.TotalCheckins,
            complete   = r.CompleteCheckins,
            rate       = r.CompletionRate
        };

        return new
        {
            // 打卡次數：最多前三 / 最少前三（不限是否有打卡）
            topCheckin    = rows.OrderByDescending(r => r.TotalCheckins).Take(3).Select(ToRankItem).ToArray(),
            bottomCheckin = rows.OrderBy(r => r.TotalCheckins).Take(3).Select(ToRankItem).ToArray(),
            // 完成率：僅含有打卡記錄的任務
            topRate    = withCheckins.OrderByDescending(r => r.CompletionRate).Take(3).Select(ToRankItem).ToArray(),
            bottomRate = withCheckins.OrderBy(r => r.CompletionRate).Take(3).Select(ToRankItem).ToArray(),
        };
    }
}
