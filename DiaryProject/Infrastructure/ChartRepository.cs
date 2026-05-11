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
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
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
        var userFilter = userId > 0 ? " AND UserId = @UserId" : "";

        var sql = $"""
            SELECT {selectExpr} AS Label, COUNT(*) AS Cnt
            FROM dbo.Diary
            WHERE (TemplateType = 'normal' OR TemplateType = 'mood')
              AND Status = 'published'
            {range.ToSqlCondition("")}{userFilter}
            GROUP BY {groupExpr}
            ORDER BY {orderExpr};
            """;

        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 日記類型比例（normal / mood）──────────────────────────────────
    public static async Task<Dictionary<string, int>> GetTypeDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND UserId = @UserId" : "";
        var sql = $"""
            SELECT TemplateType, COUNT(*) AS Cnt
            FROM dbo.Diary
            WHERE (TemplateType = 'normal' OR TemplateType = 'mood')
              AND Status = 'published'
            {range.ToSqlCondition("")}{userFilter}
            GROUP BY TemplateType;
            """;

        var result = new Dictionary<string, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 分類標籤分布（Top 8）────────────────────────────────────────
    public static async Task<List<(string Tag, int Count)>> GetCategoryDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT TOP 8 t.TagName, COUNT(DISTINCT dt.DiaryId) AS Cnt
            FROM dbo.Tag t
            INNER JOIN dbo.DiaryTag dt ON dt.TagId = t.TagId
            INNER JOIN dbo.Diary d    ON d.DiaryId = dt.DiaryId
            WHERE d.Status = 'published'
              AND (t.TagType = 'system' OR t.TagType = 'Category')
            {range.ToSqlCondition()}{userFilter}
            GROUP BY t.TagName
            ORDER BY Cnt DESC;
            """;

        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 熱門標籤（Top 10，排除分類標籤）────────────────────────────
    public static async Task<List<(string Tag, int Count)>> GetTopTagsAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT TOP 10 t.TagName, COUNT(DISTINCT dt.DiaryId) AS Cnt
            FROM dbo.Tag t
            INNER JOIN dbo.DiaryTag dt ON dt.TagId = t.TagId
            INNER JOIN dbo.Diary d    ON d.DiaryId = dt.DiaryId
            WHERE d.Status = 'published'
              AND t.TagType <> 'system' AND t.TagType <> 'Category'
            {range.ToSqlCondition()}{userFilter}
            GROUP BY t.TagName
            ORDER BY Cnt DESC;
            """;

        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
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
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT TOP 200
                FORMAT(d.DiaryDate, 'MM/dd') AS DiaryDate,
                m.EnergyValue, m.StressValue, m.SleepValue
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Status = 'published'
            {range.ToSqlCondition()}{userFilter}
            ORDER BY d.DiaryDate ASC, d.DiaryTime ASC;
            """;

        var result = new List<MoodTrendRow>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
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
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT
                ROUND(AVG(CAST(m.EnergyValue AS float)), 1) AS AvgEnergy,
                ROUND(AVG(CAST(m.StressValue AS float)), 1) AS AvgStress,
                ROUND(AVG(CAST(m.SleepValue  AS float)), 1) AS AvgSleep,
                COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Status = 'published'
            {range.ToSqlCondition()}{userFilter};
            """;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
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
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT m.StressValue, COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Status = 'published'
            {range.ToSqlCondition()}{userFilter}
            GROUP BY m.StressValue
            ORDER BY m.StressValue;
            """;

        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetByte(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 睡眠品質分布（1-10 各值出現次數）────────────────────────────
    public static async Task<Dictionary<int, int>> GetSleepDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT m.SleepValue, COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Status = 'published'
            {range.ToSqlCondition()}{userFilter}
            GROUP BY m.SleepValue
            ORDER BY m.SleepValue;
            """;

        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetByte(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 活力分布（1-10 各值出現次數）────────────────────────────────
    public static async Task<Dictionary<int, int>> GetEnergyDistributionAsync(
        string connectionString, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var userFilter = userId > 0 ? " AND d.UserId = @UserId" : "";
        var sql = $"""
            SELECT m.EnergyValue, COUNT(*) AS Cnt
            FROM dbo.DiaryMood m
            JOIN dbo.Diary d ON d.DiaryId = m.DiaryId
            WHERE d.Status = 'published'
            {range.ToSqlCondition()}{userFilter}
            GROUP BY m.EnergyValue
            ORDER BY m.EnergyValue;
            """;

        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetByte(0)] = reader.GetInt32(1);
        return result;
    }

    // （已合併至 GetTimeSeriesCountAsync，不再需要獨立的 GetWeeklyCountAsync）
}

// ════════════════════════════════════════════════════════════════════
// 任務打卡圖表查詢（EmotionTaskDB）
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

    private static string UserCond(int userId, string tableAlias = "t") =>
        userId > 0 ? $" AND {tableAlias}.user_id = @UserId" : "";

    private static void AddUserParam(SqlCommand cmd, int userId)
    {
        if (userId > 0) cmd.Parameters.AddWithValue("@UserId", userId);
    }

    // ── 任務打卡時間序列（自動依粒度切換 日/週/月）──────────────────
    public static async Task<List<(string Label, int Count)>> GetTimeSeriesCheckinAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
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
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE 1=1 {DateCond(range)}{UserCond(userId)}
            GROUP BY {groupExpr}
            ORDER BY {orderExpr};
            """;
        var result = new List<(string, int)>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        return result;
    }

    // ── 各任務打卡次數（含完成次數，用於計算完成率）─────────────────
    public sealed class PerTaskRow
    {
        public string Title           { get; init; } = string.Empty;
        public int    TotalCheckins   { get; init; }
        public int    CompleteCheckins { get; init; }
        /// <summary>Complete / Total × 100，Total=0 時為 0</summary>
        public double CompletionRate  =>
            TotalCheckins == 0 ? 0.0 : Math.Round((double)CompleteCheckins / TotalCheckins * 100, 1);
    }

    public static async Task<List<PerTaskRow>> GetPerTaskCheckinAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var joinCond = DateCond(range);
        var userFilter = userId > 0 ? " AND t.user_id = @UserId" : "";
        var sql = $"""
            SELECT t.title,
                   COUNT(cl.checkin_id) AS total,
                   SUM(CASE WHEN cl.checkin_type = 'Complete' THEN 1 ELSE 0 END) AS complete_cnt
            FROM dbo.task t
            LEFT JOIN dbo.task_checkin_log cl ON cl.task_id = t.task_id {joinCond}
            WHERE 1=1{userFilter}
            GROUP BY t.task_id, t.title
            ORDER BY total DESC;
            """;
        var result = new List<PerTaskRow>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new PerTaskRow
            {
                Title            = reader.GetString(0),
                TotalCheckins    = reader.GetInt32(1),
                CompleteCheckins = reader.GetInt32(2),
            });
        return result;
    }

    // ── 打卡類型分布（Complete / Makeup）──────────────────────────────
    public static async Task<Dictionary<string, int>> GetCheckinTypeDistAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT cl.checkin_type, COUNT(*) AS Cnt
            FROM dbo.task_checkin_log cl
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE 1=1 {DateCond(range)}{UserCond(userId)}
            GROUP BY cl.checkin_type;
            """;
        var result = new Dictionary<string, int>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    // （已合併至 GetTimeSeriesCheckinAsync，不再需要獨立的 GetWeeklyCheckinAsync）

    // ── 打卡星期分布（週日=1 … 週六=7）──────────────────────────────
    public static async Task<Dictionary<int, int>> GetCheckinWeekdayDistAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT DATEPART(WEEKDAY, cl.checkin_date) AS wd, COUNT(*) AS Cnt
            FROM dbo.task_checkin_log cl
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE 1=1 {DateCond(range)}{UserCond(userId)}
            GROUP BY DATEPART(WEEKDAY, cl.checkin_date)
            ORDER BY wd;
            """;
        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetInt32(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 打卡時段分布（0–23 時）───────────────────────────────────────
    public static async Task<Dictionary<int, int>> GetCheckinHourDistAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT DATEPART(HOUR, cl.checkin_at) AS hr, COUNT(*) AS Cnt
            FROM dbo.task_checkin_log cl
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE 1=1 {DateCond(range)}{UserCond(userId)}
            GROUP BY DATEPART(HOUR, cl.checkin_at)
            ORDER BY hr;
            """;
        var result = new Dictionary<int, int>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetInt32(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 任務類型比例（Daily / NonDaily，僅計算 Active 任務）─────────
    public static async Task<Dictionary<string, int>> GetRhythmTypeDistAsync(
        string connStr, CancellationToken ct = default, int userId = 0)
    {
        var userFilter = userId > 0 ? " AND user_id = @UserId" : "";
        var sql = $"""
            SELECT rhythm_type, COUNT(*) AS Cnt
            FROM dbo.task
            WHERE status = 'Active'{userFilter}
            GROUP BY rhythm_type;
            """;
        var result = new Dictionary<string, int>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }

    // ── 週目標達成率（週一～週日完成率）─────────────────────────────
    public sealed class WeekdayCompletionRateRow
    {
        public int Weekday { get; init; }
        public int TotalCheckins { get; init; }
        public int CompleteCheckins { get; init; }
        public double CompletionRate =>
            TotalCheckins == 0 ? 0.0 : Math.Round((double)CompleteCheckins / TotalCheckins * 100, 1);
    }

    public static async Task<List<WeekdayCompletionRateRow>> GetWeekdayCompletionRateAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var sql = $"""
            SELECT ((DATEPART(WEEKDAY, cl.checkin_date) + @@DATEFIRST + 5) % 7) + 1 AS weekday_no,
                   COUNT(*) AS total,
                   SUM(CASE WHEN cl.checkin_type = 'Complete' THEN 1 ELSE 0 END) AS complete_cnt
            FROM dbo.task_checkin_log cl
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE cl.checkin_type != 'Undo' {DateCond(range)}{UserCond(userId)}
            GROUP BY ((DATEPART(WEEKDAY, cl.checkin_date) + @@DATEFIRST + 5) % 7) + 1
            ORDER BY weekday_no;
            """;
        var result = new List<WeekdayCompletionRateRow>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new WeekdayCompletionRateRow
            {
                Weekday = reader.GetInt32(0),
                TotalCheckins = reader.GetInt32(1),
                CompleteCheckins = reader.GetInt32(2),
            });
        return result;
    }

    // ── 任務摘要統計 ──────────────────────────────────────────────────
    public sealed class TaskSummaryRow
    {
        public int ActiveTasks { get; init; }
        public int TotalCheckins { get; init; }
        public int CompleteCount { get; init; }
        public int MakeupCount { get; init; }
    }

    public static async Task<TaskSummaryRow> GetTaskSummaryAsync(
        string connStr, ChartDateRange? range = null, CancellationToken ct = default, int userId = 0)
    {
        range ??= new ChartDateRange();
        var activeUserFilter = userId > 0 ? " AND user_id = @UserId" : "";
        var sql = $"""
            SELECT
                (SELECT COUNT(*) FROM dbo.task WHERE status = 'Active'{activeUserFilter})              AS ActiveTasks,
                COUNT(*)                                                                              AS TotalCheckins,
                COALESCE(SUM(CASE WHEN cl.checkin_type = 'Complete' THEN 1 ELSE 0 END), 0)           AS CompleteCount,
                COALESCE(SUM(CASE WHEN cl.checkin_type = 'Makeup'   THEN 1 ELSE 0 END), 0)           AS MakeupCount
            FROM dbo.task_checkin_log cl
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE 1=1 {DateCond(range)}{UserCond(userId)};
            """;
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        range.AddParams(cmd);
        AddUserParam(cmd, userId);
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

    // ══════════════════════════════════════════════════════════════
    // ── 任務歷史 ──────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════

    /// <summary>全部任務列表（含匯總統計，不限日期）</summary>
    public sealed class TaskListRow
    {
        public int       TaskId           { get; init; }
        public string    Title            { get; init; } = string.Empty;
        public string    RhythmType       { get; init; } = string.Empty;
        public string    Status           { get; init; } = string.Empty;
        public DateTime  CreatedAt        { get; init; }
        public int       TotalCheckins    { get; init; }
        public int       CompleteCheckins { get; init; }
        public int       MakeupCheckins   { get; init; }
        public int       UndoCheckins     { get; init; }
        public int?      WeeklyTarget     { get; init; }
        public DateOnly? StartDate        { get; init; }
        public double CompletionRate =>
            TotalCheckins == 0 ? 0.0 : Math.Round((double)CompleteCheckins / TotalCheckins * 100, 1);
    }

    public static async Task<List<TaskListRow>> GetAllTaskListAsync(
        string connStr, CancellationToken ct = default, int userId = 0)
    {
        var userFilter = userId > 0 ? " WHERE t.user_id = @UserId" : "";
        var sql = $"""
            SELECT t.task_id, t.title, t.rhythm_type, t.status, t.created_at,
                   COUNT(cl.checkin_id)                                                          AS total,
                   SUM(CASE WHEN cl.checkin_type = 'Complete' THEN 1 ELSE 0 END)                 AS complete_cnt,
                   SUM(CASE WHEN cl.checkin_type = 'Makeup'   THEN 1 ELSE 0 END)                 AS makeup_cnt,
                   SUM(CASE WHEN cl.checkin_type = 'Undo'     THEN 1 ELSE 0 END)                 AS undo_cnt,
                   MAX(r.weekly_target_count)                                                     AS weekly_target,
                   CAST(MIN(r.start_date) AS DATE)                                               AS start_date
            FROM dbo.task t
            LEFT JOIN dbo.task_checkin_log cl ON cl.task_id = t.task_id
            LEFT JOIN dbo.task_schedule_rule r  ON r.task_id = t.task_id
            {userFilter}
            GROUP BY t.task_id, t.title, t.rhythm_type, t.status, t.created_at
            ORDER BY t.status ASC, total DESC;
            """;
        var result = new List<TaskListRow>();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        AddUserParam(cmd, userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new TaskListRow
            {
                TaskId           = reader.GetInt32(0),
                Title            = reader.GetString(1),
                RhythmType       = reader.GetString(2),
                Status           = reader.GetString(3),
                CreatedAt        = reader.GetDateTime(4),
                TotalCheckins    = reader.GetInt32(5),
                CompleteCheckins = reader.GetInt32(6),
                MakeupCheckins   = reader.GetInt32(7),
                UndoCheckins     = reader.GetInt32(8),
                WeeklyTarget     = reader.IsDBNull(9)  ? null : reader.GetInt32(9),
                StartDate        = reader.IsDBNull(10) ? null : DateOnly.FromDateTime(reader.GetDateTime(10)),
            });
        return result;
    }

    /// <summary>單一任務詳細打卡紀錄（不限筆數）</summary>
    public sealed class CheckinLogEntry
    {
        public DateOnly Date      { get; init; }
        public string   Type      { get; init; } = string.Empty;
        public DateTime CheckinAt { get; init; }
    }

    public static async Task<(TaskListRow? Info, List<CheckinLogEntry> Logs)> GetTaskDetailAsync(
        string connStr, int taskId, CancellationToken ct = default)
    {
        const string sqlInfo = """
            SELECT t.task_id, t.title, t.rhythm_type, t.status, t.created_at,
                   COUNT(cl.checkin_id)                                                           AS total,
                   SUM(CASE WHEN cl.checkin_type = 'Complete' THEN 1 ELSE 0 END)                  AS complete_cnt,
                   SUM(CASE WHEN cl.checkin_type = 'Makeup'   THEN 1 ELSE 0 END)                  AS makeup_cnt,
                   SUM(CASE WHEN cl.checkin_type = 'Undo'     THEN 1 ELSE 0 END)                  AS undo_cnt,
                   MAX(r.weekly_target_count)                                                      AS weekly_target,
                   CAST(MIN(r.start_date) AS DATE)                                                AS start_date,
                   MIN(cl.checkin_date)                                                            AS first_checkin,
                   MAX(cl.checkin_date)                                                            AS last_checkin
            FROM dbo.task t
            LEFT JOIN dbo.task_checkin_log cl ON cl.task_id = t.task_id
            LEFT JOIN dbo.task_schedule_rule r  ON r.task_id = t.task_id
            WHERE t.task_id = @TaskId
            GROUP BY t.task_id, t.title, t.rhythm_type, t.status, t.created_at;
            """;
        const string sqlLogs = """
            SELECT checkin_date, checkin_type, checkin_at
            FROM dbo.task_checkin_log
            WHERE task_id = @TaskId
            ORDER BY checkin_date DESC, checkin_at DESC;
            """;

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);

        TaskListRow? info = null;
        await using (var cmd = new SqlCommand(sqlInfo, conn))
        {
            cmd.Parameters.AddWithValue("@TaskId", taskId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
                info = new TaskListRow
                {
                    TaskId           = r.GetInt32(0),
                    Title            = r.GetString(1),
                    RhythmType       = r.GetString(2),
                    Status           = r.GetString(3),
                    CreatedAt        = r.GetDateTime(4),
                    TotalCheckins    = r.GetInt32(5),
                    CompleteCheckins = r.GetInt32(6),
                    MakeupCheckins   = r.GetInt32(7),
                    UndoCheckins     = r.GetInt32(8),
                    WeeklyTarget     = r.IsDBNull(9)  ? null : r.GetInt32(9),
                    StartDate        = r.IsDBNull(10) ? null : DateOnly.FromDateTime(r.GetDateTime(10)),
                };
        }
        if (info is null) return (null, []);

        var logs = new List<CheckinLogEntry>();
        await using (var cmd2 = new SqlCommand(sqlLogs, conn))
        {
            cmd2.Parameters.AddWithValue("@TaskId", taskId);
            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
                logs.Add(new CheckinLogEntry
                {
                    Date      = DateOnly.FromDateTime(r2.GetDateTime(0)),
                    Type      = r2.GetString(1),
                    CheckinAt = r2.GetDateTime(2),
                });
        }
        return (info, logs);
    }
}

