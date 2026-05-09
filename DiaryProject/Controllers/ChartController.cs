using Microsoft.AspNetCore.Mvc;
using MyMvcProject.Infrastructure;

namespace MyMvcProject.Controllers;

[ApiController]
[Route("api/chart")]
public sealed class ChartController(IConfiguration configuration) : ControllerBase
{
    // 日記 DB 連線字串（透過 DatabaseFactory 備管連線字串建立權責）
    private readonly string _connectionString = DatabaseFactory.GetConnectionString(configuration);
    private readonly string _taskConnectionString = DatabaseFactory.GetEmotionTaskConnectionString(configuration);

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

        var timeSeriesTask = ChartRepository.GetTimeSeriesCountAsync(_connectionString, range, ct);
        var typeTask = ChartRepository.GetTypeDistributionAsync(_connectionString, range, ct);
        var categoryTask = ChartRepository.GetCategoryDistributionAsync(_connectionString, range, ct);
        var moodTrendTask = ChartRepository.GetMoodTrendAsync(_connectionString, range, ct);
        var moodAvgTask = ChartRepository.GetMoodAverageAsync(_connectionString, range, ct);
        var stressDistTask = ChartRepository.GetStressDistributionAsync(_connectionString, range, ct);
        var sleepDistTask = ChartRepository.GetSleepDistributionAsync(_connectionString, range, ct);

        // ── 任務相關查詢（MoodDiary）──
        var taskTimeSeriesTask = TaskRepository.GetTimeSeriesCheckinAsync(_taskConnectionString, range, ct);
        var taskPerTaskTask = TaskRepository.GetPerTaskCheckinAsync(_taskConnectionString, range, ct);
        var taskTypeTask = TaskRepository.GetCheckinTypeDistAsync(_taskConnectionString, range, ct);
        var taskSummaryTask = TaskRepository.GetTaskSummaryAsync(_taskConnectionString, range, ct);

        await Task.WhenAll(timeSeriesTask, typeTask, categoryTask,
                           moodTrendTask, moodAvgTask, stressDistTask, sleepDistTask,
                           taskTimeSeriesTask, taskPerTaskTask, taskTypeTask, taskSummaryTask);

        var timeSeries = range.FillGaps(await timeSeriesTask);
        var type = await typeTask;
        var category = await categoryTask;
        var moodTrend = await moodTrendTask;
        var moodAvg = await moodAvgTask;
        var stressDist = await stressDistTask;
        var sleepDist = await sleepDistTask;

        var taskTimeSeries = range.FillGaps(await taskTimeSeriesTask);
        var taskPerTask = await taskPerTaskTask;
        var taskType = await taskTypeTask;
        var taskSummary = await taskSummaryTask;

        var stressData = Enumerable.Range(1, 10).Select(i => stressDist.GetValueOrDefault(i, 0)).ToArray();
        var sleepData = Enumerable.Range(1, 10).Select(i => sleepDist.GetValueOrDefault(i, 0)).ToArray();
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
            // ── 任務區塊（合併原本的 taskMonthly + taskWeekly）──
            taskTimeSeries = new
            {
                labels = taskTimeSeries.Select(x => x.Label).ToArray(),
                data = taskTimeSeries.Select(x => x.Count).ToArray()
            },
            taskPerTask = new
            {
                labels = taskPerTask.Select(x => x.Title).ToArray(),
                data = taskPerTask.Select(x => x.Count).ToArray()
            },
            taskCheckinType = new
            {
                labels = taskType.Keys.Select(k => k == "Complete" ? "正常打卡" : "補打卡").ToArray(),
                data = taskType.Values.ToArray()
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
}
