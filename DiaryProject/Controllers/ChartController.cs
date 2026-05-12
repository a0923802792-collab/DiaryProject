using Microsoft.AspNetCore.Mvc;
using DiaryProject.Infrastructure;

namespace DiaryProject.Controllers;

[ApiController]
[Route("api/chart")]
public sealed class ChartController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connectionString = DatabaseFactory.GetConnectionString(configuration);
    private readonly string _taskConnectionString = DatabaseFactory.GetEmotionTaskConnectionString(configuration);

    private int? GetCurrentUserId()
    {
        return HttpContext.Session.GetInt32("UserId");
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? preset,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "尚未登入" });
        }

        var range = BuildRange(preset, from, to);

        var timeSeriesTask = ChartRepository.GetTimeSeriesCountAsync(_connectionString, range, ct, userId.Value);
        var typeTask = ChartRepository.GetTypeDistributionAsync(_connectionString, range, ct, userId.Value);
        var categoryTask = ChartRepository.GetCategoryDistributionAsync(_connectionString, range, ct, userId.Value);
        var moodTrendTask = ChartRepository.GetMoodTrendAsync(_connectionString, range, ct, userId.Value);
        var moodAvgTask = ChartRepository.GetMoodAverageAsync(_connectionString, range, ct, userId.Value);
        var stressDistTask = ChartRepository.GetStressDistributionAsync(_connectionString, range, ct, userId.Value);
        var sleepDistTask = ChartRepository.GetSleepDistributionAsync(_connectionString, range, ct, userId.Value);
        var energyDistTask = ChartRepository.GetEnergyDistributionAsync(_connectionString, range, ct, userId.Value);

        var taskTimeSeriesTask = TaskRepository.GetTimeSeriesCheckinAsync(_taskConnectionString, range, ct, userId.Value);
        var taskPerTaskTask = TaskRepository.GetPerTaskCheckinAsync(_taskConnectionString, range, ct, userId.Value);
        var taskTypeTask = TaskRepository.GetCheckinTypeDistAsync(_taskConnectionString, range, ct, userId.Value);
        var taskSummaryTask = TaskRepository.GetTaskSummaryAsync(_taskConnectionString, range, ct, userId.Value);
        var taskWeekdayTask = TaskRepository.GetCheckinWeekdayDistAsync(_taskConnectionString, range, ct, userId.Value);
        var taskHourTask = TaskRepository.GetCheckinHourDistAsync(_taskConnectionString, range, ct, userId.Value);
        var taskRhythmTask = TaskRepository.GetRhythmTypeDistAsync(_taskConnectionString, ct, userId.Value);
        var taskWeeklyGoalTask = TaskRepository.GetWeekdayCompletionRateAsync(_taskConnectionString, range, ct, userId.Value);

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

        var taskTimeSeries = range.FillGaps(await taskTimeSeriesTask);
        var taskPerTask = await taskPerTaskTask;
        var taskType = await taskTypeTask;
        var taskSummary = await taskSummaryTask;
        var weekdayDist = await taskWeekdayTask;
        var hourDist = await taskHourTask;
        var rhythmDist = await taskRhythmTask;
        var weeklyGoalRows = await taskWeeklyGoalTask;

        var weekdayLabels = new[] { "日", "一", "二", "三", "四", "五", "六" };
        var weekdayData = Enumerable.Range(1, 7).Select(i => weekdayDist.GetValueOrDefault(i, 0)).ToArray();

        var hourLabels = Enumerable.Range(0, 24).Select(i => $"{i:D2}:00").ToArray();
        var hourData = Enumerable.Range(0, 24).Select(i => hourDist.GetValueOrDefault(i, 0)).ToArray();

        var weeklyGoalDict = weeklyGoalRows.ToDictionary(r => r.Weekday, r => r.CompletionRate);
        var weeklyGoalLabels = new[] { "週一", "週二", "週三", "週四", "週五", "週六", "週日" };
        var weeklyGoalRates = Enumerable.Range(1, 7).Select(i => weeklyGoalDict.GetValueOrDefault(i, 0)).ToArray();

        var stressData = Enumerable.Range(1, 10).Select(i => stressDist.GetValueOrDefault(i, 0)).ToArray();
        var sleepData = Enumerable.Range(1, 10).Select(i => sleepDist.GetValueOrDefault(i, 0)).ToArray();
        var energyData = Enumerable.Range(1, 10).Select(i => energyDist.GetValueOrDefault(i, 0)).ToArray();
        var scaleLabels = Enumerable.Range(1, 10).Select(i => i.ToString()).ToArray();

        return Ok(new
        {
            appliedRange = new
            {
                from = range.DateFrom?.ToString("yyyy-MM-dd"),
                to = range.DateTo?.ToString("yyyy-MM-dd"),
            },
            granularity = range.Granularity,
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
            taskTimeSeries = new
            {
                labels = taskTimeSeries.Select(x => x.Label).ToArray(),
                data = taskTimeSeries.Select(x => x.Count).ToArray()
            },
            taskPerTask = new
            {
                labels = taskPerTask.Select(x => x.Title).ToArray(),
                data = taskPerTask.Select(x => x.TotalCheckins).ToArray(),
                completionRates = taskPerTask.Select(x => x.CompletionRate).ToArray(),
                rankings = BuildTaskRankings(taskPerTask)
            },
            taskCheckinType = new
            {
                labels = new[] { "正常打卡", "補打卡" },
                data = new[]
                {
                    taskType.GetValueOrDefault("Complete", 0),
                    taskType.Where(kv => kv.Key != "Complete").Sum(kv => kv.Value)
                }
            },
            taskWeekdayDist = new { labels = weekdayLabels, data = weekdayData },
            taskHourDist = new { labels = hourLabels, data = hourData },
            taskRhythmDist = new
            {
                labels = rhythmDist.Keys
                    .Select(k => k == "Daily" ? "每日任務" : "非每日任務").ToArray(),
                data = rhythmDist.Values.ToArray()
            },
            taskWeeklyGoal = new
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
                taskActive = taskSummary.ActiveTasks,
                taskTotalCheckin = taskSummary.TotalCheckins,
                taskComplete = taskSummary.CompleteCount,
                taskMakeup = taskSummary.MakeupCount,
            }
        });
    }

    private static object BuildMoodTrendResponse(
        ChartDateRange range, List<ChartRepository.MoodTrendRow> raw)
    {
        var allLabels = range.GenerateDateLabels();
        if (allLabels.Count == 0)
        {
            return new
            {
                labels = raw.Select(x => x.Date).ToArray(),
                energy = raw.Select(x => (int?)x.Energy).ToArray(),
                stress = raw.Select(x => (int?)x.Stress).ToArray(),
                sleep = raw.Select(x => (int?)x.Sleep).ToArray(),
            };
        }

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

    [HttpGet("task-list")]
    public async Task<IActionResult> GetTaskList(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "尚未登入" });
        }

        var rows = await TaskRepository.GetAllTaskListAsync(_taskConnectionString, ct, userId.Value);
        return Ok(new
        {
            tasks = rows.Select(r => new
            {
                taskId = r.TaskId,
                title = r.Title,
                rhythmType = r.RhythmType,
                status = r.Status,
                createdAt = r.CreatedAt.ToString("yyyy-MM-dd"),
                totalCheckins = r.TotalCheckins,
                completeCheckins = r.CompleteCheckins,
                makeupCheckins = r.MakeupCheckins,
                undoCheckins = r.UndoCheckins,
                weeklyTarget = r.WeeklyTarget,
                startDate = r.StartDate?.ToString("yyyy-MM-dd"),
                completionRate = r.CompletionRate,
            }).ToArray()
        });
    }

    [HttpGet("task-detail/{taskId:int}")]
    public async Task<IActionResult> GetTaskDetail(int taskId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "尚未登入" });
        }

        var (info, logs) = await TaskRepository.GetTaskDetailAsync(_taskConnectionString, taskId, ct);

        if (info is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            taskId = info.TaskId,
            title = info.Title,
            rhythmType = info.RhythmType,
            status = info.Status,
            createdAt = info.CreatedAt.ToString("yyyy-MM-dd"),
            totalCheckins = info.TotalCheckins,
            completeCheckins = info.CompleteCheckins,
            makeupCheckins = info.MakeupCheckins,
            undoCheckins = info.UndoCheckins,
            weeklyTarget = info.WeeklyTarget,
            startDate = info.StartDate?.ToString("yyyy-MM-dd"),
            completionRate = info.CompletionRate,
            logs = logs.Select(l => new
            {
                date = l.Date.ToString("yyyy-MM-dd"),
                type = l.Type,
                checkinAt = l.CheckinAt.ToString("yyyy-MM-dd HH:mm"),
            }).ToArray()
        });
    }

    private static ChartDateRange BuildRange(string? preset, string? from, string? to)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

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

        DateOnly? dateFrom = DateOnly.TryParse(from, out var f) ? f : null;
        DateOnly? dateTo = DateOnly.TryParse(to, out var t) ? t : null;
        return new ChartDateRange { DateFrom = dateFrom, DateTo = dateTo };
    }

    private static object BuildTaskRankings(List<TaskRepository.PerTaskRow> rows)
    {
        var withCheckins = rows.Where(r => r.TotalCheckins > 0).ToList();

        static object ToRankItem(TaskRepository.PerTaskRow r) => new
        {
            title = r.Title,
            count = r.TotalCheckins,
            complete = r.CompleteCheckins,
            rate = r.CompletionRate
        };

        return new
        {
            topCheckin = rows.OrderByDescending(r => r.TotalCheckins).Take(3).Select(ToRankItem).ToArray(),
            bottomCheckin = rows.OrderBy(r => r.TotalCheckins).Take(3).Select(ToRankItem).ToArray(),
            topRate = withCheckins.OrderByDescending(r => r.CompletionRate).Take(3).Select(ToRankItem).ToArray(),
            bottomRate = withCheckins.OrderBy(r => r.CompletionRate).Take(3).Select(ToRankItem).ToArray(),
        };
    }
}