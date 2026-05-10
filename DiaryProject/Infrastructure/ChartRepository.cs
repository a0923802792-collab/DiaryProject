using Microsoft.Data.SqlClient;

namespace DiaryProject.Infrastructure;

// ── 日期範圍篩選器 ────────────────────────────────────────────────────
/// <summary>
/// 圖表日期篩選器。
/// DateFrom / DateTo 皆為含端點的範圍，null 代表不限制。
/// </summary>
public sealed class ChartDateRange
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    public bool HasFilter => DateFrom.HasValue || DateTo.HasValue;

    /// <summary>
    /// 根據時間跨度自動決定顯示粒度：
    ///   ≤ 14 天 → day（每日）
    ///   15-90 天 → week（每週）
    ///   > 90 天或無範圍 → month（每月）
    /// </summary>
    public string Granularity => (DateFrom, DateTo) switch
    {
        (not null, not null) when DateTo.Value.DayNumber - DateFrom.Value.DayNumber <= 14 => "day",
        (not null, not null) when DateTo.Value.DayNumber - DateFrom.Value.DayNumber <= 90 => "week",
        _ => "month"
    };

    /// <summary>附加 AND [alias.]DiaryDate 篩選條件至 sql 字串。tableAlias 為空字串時直接用欄位名。</summary>
    public string ToSqlCondition(string tableAlias = "d")
    {
        var col = string.IsNullOrEmpty(tableAlias) ? "DiaryDate" : $"{tableAlias}.DiaryDate";
        return (DateFrom.HasValue, DateTo.HasValue) switch
        {
            (true, true) => $" AND {col} BETWEEN @DateFrom AND @DateTo",
            (true, false) => $" AND {col} >= @DateFrom",
            (false, true) => $" AND {col} <= @DateTo",
            _ => ""
        };
    }

    public void AddParams(SqlCommand cmd)
    {
        if (DateFrom.HasValue) cmd.Parameters.AddWithValue("@DateFrom", DateFrom.Value.ToDateTime(TimeOnly.MinValue));
        if (DateTo.HasValue) cmd.Parameters.AddWithValue("@DateTo", DateTo.Value.ToDateTime(TimeOnly.MaxValue));
    }

    /// <summary>
    /// 將時間序列結果補齊缺漏的日期（沒有資料的補 0）。
    /// 只在有完整 DateFrom+DateTo 時生效。
    /// </summary>
    public List<(string Label, int Count)> FillGaps(List<(string Label, int Count)> data)
    {
        if (!DateFrom.HasValue || !DateTo.HasValue) return data;
        var from = DateFrom.Value;
        var to = DateTo.Value;
        var dict = data.ToDictionary(x => x.Label, x => x.Count);
        var result = new List<(string, int)>();

        switch (Granularity)
        {
            case "day":
                for (var d = from; d <= to; d = d.AddDays(1))
                {
                    var label = d.ToString("MM/dd");
                    result.Add((label, dict.GetValueOrDefault(label, 0)));
                }
                break;
            case "week":
                // 從 DateFrom 所在週的週日開始
                var startDow = (int)from.DayOfWeek;
                var weekStart = from.AddDays(-startDow);
                for (var w = weekStart; w <= to; w = w.AddDays(7))
                {
                    var label = w.ToString("MM/dd");
                    result.Add((label, dict.GetValueOrDefault(label, 0)));
                }
                break;
            case "month":
                var mStart = new DateOnly(from.Year, from.Month, 1);
                var mEnd = new DateOnly(to.Year, to.Month, 1);
                for (var m = mStart; m <= mEnd; m = m.AddMonths(1))
                {
                    var label = m.ToString("yyyy-MM");
                    result.Add((label, dict.GetValueOrDefault(label, 0)));
                }
                break;
            default:
                return data;
        }
        return result;
    }

    /// <summary>
    /// 將日期型別的資料（如情緒趨勢）補齊缺漏的日期，缺漏處填 null。
    /// 回傳所有日期標籤列表。
    /// </summary>
    public List<string> GenerateDateLabels()
    {
        if (!DateFrom.HasValue || !DateTo.HasValue) return [];
        var labels = new List<string>();
        for (var d = DateFrom.Value; d <= DateTo.Value; d = d.AddDays(1))
            labels.Add(d.ToString("MM/dd"));
        return labels;
    }
}

/// <summary>圖表 API 所需的各種統計查詢</summary>
public static class ChartRepository
{
    // ── 日記時間序列（自動依粒度切換 日/週/月）──────────────────────
    public static async Task<List<(string Label, int Count)>> GetTimeSeriesCountAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var (selectExpr, groupExpr, orderExpr) = range.Granularity switch
        {
            "day" => ("FORMAT(DiaryDate, 'MM/dd')",
                       "FORMAT(DiaryDate, 'MM/dd')",
                       "MIN(CAST(DiaryDate AS DATE))"),
            "week" => ("FORMAT(DATEADD(DAY, 1 - DATEPART(WEEKDAY, DiaryDate), DiaryDate), 'MM/dd')",
                       "FORMAT(DATEADD(DAY, 1 - DATEPART(WEEKDAY, DiaryDate), DiaryDate), 'MM/dd')",
                       "MIN(DATEADD(DAY, 1 - DATEPART(WEEKDAY, DiaryDate), DiaryDate))"),
            _ => ("FORMAT(DiaryDate, 'yyyy-MM')",
                       "FORMAT(DiaryDate, 'yyyy-MM')",
                       "FORMAT(DiaryDate, 'yyyy-MM')")
        };

        var sql = $"""
            SELECT {selectExpr} AS Label, COUNT(*) AS Cnt
            FROM dbo.Diary
            WHERE (TemplateType = 'normal' OR TemplateType = 'mood')
              AND Visibility = 'shared' AND Status = 'published'
            {range.ToSqlCondition("")}
            GROUP BY {groupExpr}
            ORDER BY {orderExpr};
            """;

        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 日記類型比例（normal / mood）──────────────────────────────────
    public static async Task<Dictionary<string, int>> GetTypeDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT TemplateType, COUNT(*) AS Cnt
            FROM dbo.Diary
            WHERE (TemplateType = 'normal' OR TemplateType = 'mood')
              AND Visibility = 'shared' AND Status = 'published'
            {range.ToSqlCondition("")}
            GROUP BY TemplateType;
            """;

        var result = new Dictionary<string, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 分類標籤分布（Top 8）────────────────────────────────────────
    public static async Task<List<(string Tag, int Count)>> GetCategoryDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT TOP 8 t.TagName, COUNT(DISTINCT dt.DiaryId) AS Cnt
            FROM dbo.Tag t
            INNER JOIN dbo.DiaryTag dt ON dt.TagId = t.TagId
            INNER JOIN dbo.Diary d    ON d.DiaryId = dt.DiaryId
            WHERE d.Visibility = 'shared' AND d.Status = 'published'
              AND (t.TagType = 'system' OR t.TagType = 'Category')
            {range.ToSqlCondition()}
            GROUP BY t.TagName
            ORDER BY Cnt DESC;
            """;

        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 熱門標籤（Top 10，排除分類標籤）────────────────────────────
    public static async Task<List<(string Tag, int Count)>> GetTopTagsAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT TOP 10 t.TagName, COUNT(DISTINCT dt.DiaryId) AS Cnt
            FROM dbo.Tag t
            INNER JOIN dbo.DiaryTag dt ON dt.TagId = t.TagId
            INNER JOIN dbo.Diary d    ON d.DiaryId = dt.DiaryId
            WHERE d.Visibility = 'shared' AND d.Status = 'published'
              AND t.TagType <> 'system' AND t.TagType <> 'Category'
            {range.ToSqlCondition()}
            GROUP BY t.TagName
            ORDER BY Cnt DESC;
            """;

        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 情緒三指數趨勢（近 30 筆 mood 日記）────────────────────────
    public sealed class MoodTrendRow
    {
        public string Date { get; init; } = string.Empty;
        public int Energy { get; init; }
        public int Stress { get; init; }
        public int Sleep { get; init; }
    }

    public static async Task<List<MoodTrendRow>> GetMoodTrendAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT TOP 200
                FORMAT(d.DiaryDate, 'MM/dd') AS DiaryDate,
                m.EnergyValue, m.StressValue, m.SleepValue
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Visibility = 'shared' AND d.Status = 'published'
            {range.ToSqlCondition()}
            ORDER BY d.DiaryDate ASC, d.DiaryTime ASC;
            """;

        var result = new List<MoodTrendRow>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new MoodTrendRow
            {
                Date = reader.GetString(0),
                Energy = reader.GetByte(1),
                Stress = reader.GetByte(2),
                Sleep = reader.GetByte(3),
            });
        return result;
    }

    // ── 情緒三指數平均值 ──────────────────────────────────────────────
    public sealed class MoodAvgRow
    {
        public double AvgEnergy { get; init; }
        public double AvgStress { get; init; }
        public double AvgSleep { get; init; }
        public int Count { get; init; }
    }

    public static async Task<MoodAvgRow> GetMoodAverageAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT
                ROUND(AVG(CAST(m.EnergyValue AS float)), 1) AS AvgEnergy,
                ROUND(AVG(CAST(m.StressValue AS float)), 1) AS AvgStress,
                ROUND(AVG(CAST(m.SleepValue  AS float)), 1) AS AvgSleep,
                COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Visibility = 'shared' AND d.Status = 'published'
            {range.ToSqlCondition()};
            """;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct) && !reader.IsDBNull(0))
            return new MoodAvgRow
            {
                AvgEnergy = reader.GetDouble(0),
                AvgStress = reader.GetDouble(1),
                AvgSleep = reader.GetDouble(2),
                Count = reader.GetInt32(3),
            };
        return new MoodAvgRow();
    }

    // ── 壓力分布（1-10 各值出現次數）────────────────────────────────
    public static async Task<Dictionary<int, int>> GetStressDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT m.StressValue, COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Visibility = 'shared' AND d.Status = 'published'
            {range.ToSqlCondition()}
            GROUP BY m.StressValue
            ORDER BY m.StressValue;
            """;

        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetByte(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 睡眠品質分布（1-10 各值出現次數）────────────────────────────
    public static async Task<Dictionary<int, int>> GetSleepDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT m.SleepValue, COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Visibility = 'shared' AND d.Status = 'published'
            {range.ToSqlCondition()}
            GROUP BY m.SleepValue
            ORDER BY m.SleepValue;
            """;

        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetByte(0)] = reader.GetInt32(1);
        return result;
    }

    // （已合併至 GetTimeSeriesCountAsync，不再需要獨立的 GetWeeklyCountAsync）
}

// ════════════════════════════════════════════════════════════════════
// 任務打卡圖表查詢（MoodDiary）
// ════════════════════════════════════════════════════════════════════
public static class TaskRepository
{
    // 日期篩選條件（checkin_date 欄位，無別名）
    private static string DateCond(ChartDateRange range) =>
        (range.DateFrom.HasValue, range.DateTo.HasValue) switch
        {
            (true, true) => " AND cl.checkin_date BETWEEN @DateFrom AND @DateTo",
            (true, false) => " AND cl.checkin_date >= @DateFrom",
            (false, true) => " AND cl.checkin_date <= @DateTo",
            _ => ""
        };

    // ── 任務打卡時間序列（自動依粒度切換 日/週/月）──────────────────
    public static async Task<List<(string Label, int Count)>> GetTimeSeriesCheckinAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var (selectExpr, groupExpr, orderExpr) = range.Granularity switch
        {
            "day" => ("FORMAT(cl.checkin_date, 'MM/dd')",
                       "FORMAT(cl.checkin_date, 'MM/dd')",
                       "MIN(CAST(cl.checkin_date AS DATE))"),
            "week" => ("FORMAT(DATEADD(DAY, 2 - DATEPART(WEEKDAY, cl.checkin_date), cl.checkin_date), 'MM/dd')",
                       "FORMAT(DATEADD(DAY, 2 - DATEPART(WEEKDAY, cl.checkin_date), cl.checkin_date), 'MM/dd')",
                       "MIN(DATEADD(DAY, 2 - DATEPART(WEEKDAY, cl.checkin_date), cl.checkin_date))"),
            _ => ("FORMAT(cl.checkin_date, 'yyyy-MM')",
                       "FORMAT(cl.checkin_date, 'yyyy-MM')",
                       "FORMAT(cl.checkin_date, 'yyyy-MM')")
        };

        var sql = $"""
            SELECT {selectExpr} AS Label, COUNT(*) AS Cnt
            FROM dbo.task_checkin_log cl
            WHERE 1=1 {DateCond(range)}
            GROUP BY {groupExpr}
            ORDER BY {orderExpr};
            """;
        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 各任務打卡次數 ────────────────────────────────────────────────
    public static async Task<List<(string Title, int Count)>> GetPerTaskCheckinAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var dateCond = DateCond(range);
        // LEFT JOIN 裡的日期條件要搬到 ON 子句
        var joinCond = dateCond.Replace(" AND cl.checkin_date", " AND cl.checkin_date");
        var sql = $"""
            SELECT t.title, COUNT(cl.checkin_id) AS Cnt
            FROM dbo.task t
            LEFT JOIN dbo.task_checkin_log cl ON cl.task_id = t.task_id {joinCond}
            GROUP BY t.task_id, t.title
            ORDER BY Cnt DESC;
            """;
        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 打卡類型分布（Complete / Makeup）──────────────────────────────
    public static async Task<Dictionary<string, int>> GetCheckinTypeDistAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT cl.checkin_type, COUNT(*) AS Cnt
            FROM dbo.task_checkin_log cl
            WHERE 1=1 {DateCond(range)}
            GROUP BY cl.checkin_type;
            """;
        var result = new Dictionary<string, int>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    // （已合併至 GetTimeSeriesCheckinAsync，不再需要獨立的 GetWeeklyCheckinAsync）

    // ── 任務摘要統計 ──────────────────────────────────────────────────
    public sealed class TaskSummaryRow
    {
        public int ActiveTasks { get; init; }
        public int TotalCheckins { get; init; }
        public int CompleteCount { get; init; }
        public int MakeupCount { get; init; }
    }

    public static async Task<TaskSummaryRow> GetTaskSummaryAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT
                (SELECT COUNT(*) FROM dbo.task WHERE status = 'Active')                              AS ActiveTasks,
                COUNT(*)                                                                              AS TotalCheckins,
                COALESCE(SUM(CASE WHEN cl.checkin_type = 'Complete' THEN 1 ELSE 0 END), 0)           AS CompleteCount,
                COALESCE(SUM(CASE WHEN cl.checkin_type = 'Makeup'   THEN 1 ELSE 0 END), 0)           AS MakeupCount
            FROM dbo.task_checkin_log cl
            WHERE 1=1 {DateCond(range)};
            """;
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return new TaskSummaryRow
            {
                ActiveTasks = reader.GetInt32(0),
                TotalCheckins = reader.GetInt32(1),
                CompleteCount = reader.GetInt32(2),
                MakeupCount = reader.GetInt32(3),
            };
        return new TaskSummaryRow();
    }
}

