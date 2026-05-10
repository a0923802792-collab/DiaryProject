using Microsoft.Data.SqlClient;

namespace DiaryProject.Infrastructure;

// =====================================================================
// DiaryStats — 從 master DB 查出的日記相關原始統計
// 負責：只存數字，不含任何業務判斷
// =====================================================================
public sealed class DiaryStats
{
    public int DiaryCount { get; init; }  // 總日記數
    public int MaxDiaryStreak { get; init; }  // 最長連續天數
    public int SharedDiaryCount { get; init; }  // 公開分享篇數
    public int MaxReactionsOnSinglePost { get; init; }  // 單篇最高反應數
    public int TotalReactionsReceived { get; init; }  // 全部反應總數
}

// =====================================================================
// TaskStats — 從 MoodDiary 查出的習慣相關原始統計
// 負責：只存數字與布林，不含任何業務判斷
// =====================================================================
public sealed class TaskStats
{
    public int TaskCount { get; init; }
    public int ActiveTaskCount { get; init; }
    public int CheckinCount { get; init; }
    public int MaxCheckinStreakSameTask { get; init; }  // 同一習慣最長連續天數
    public int MaxCheckinCountSameTask { get; init; }  // 同一習慣最多累計次數
    public int MaxEarlyMorningStreak { get; init; }  // 連續幾天在 10:00 前打卡
    public bool HasBothRhythmTypes { get; init; }  // 同時有 Daily + NonDaily
    public bool MetWeeklyTargetOnce { get; init; }  // 曾達成週目標
    public bool AllActiveTasksMetTargetInWeek { get; init; }  // 某週所有習慣全達標
    public bool DailyTaskFullWeek { get; init; }  // Daily 習慣單週 7 天全勤
    public int AccountAgeDays { get; init; }  // 帳號年齡（天）
}

// =====================================================================
// AchievementRepository — 負責所有成就相關的 DB 查詢
// 只做 I/O，不做業務判斷（業務判斷交給 AchievementData）
// =====================================================================
public static class AchievementRepository
{
    /// <summary>
    /// 查詢 master DB（日記、分享、反應）的統計數字。
    /// </summary>
    public static async Task<DiaryStats> QueryDiaryStatsAsync(
        string connectionString,
        CancellationToken ct = default)
    {
        // 一次送多個 SELECT，用 NextResult() 逐一讀取，減少 round-trip
        const string sql = """
            -- 1. 總日記數
            SELECT COUNT(*)
            FROM dbo.Diary
            WHERE TemplateType = 'normal' AND Status = 'published';

            -- 2. 最長連續記錄天數（gaps-and-islands）
            WITH DiaryDates AS (
                SELECT DISTINCT CAST(DiaryDate AS DATE) AS day
                FROM dbo.Diary
                WHERE TemplateType = 'normal' AND Status = 'published'
            ),
            Numbered AS (
                SELECT day,
                       DATEADD(DAY, -CAST(ROW_NUMBER() OVER (ORDER BY day) AS INT), day) AS grp
                FROM DiaryDates
            )
            SELECT ISNULL(MAX(cnt), 0)
            FROM (SELECT COUNT(*) AS cnt FROM Numbered GROUP BY grp) x;

            -- 3. 公開分享篇數
            SELECT COUNT(*)
            FROM dbo.Diary
            WHERE Visibility = 'shared' AND Status = 'published';

            -- 4. 單篇最高反應數
            SELECT ISNULL(MAX(total), 0)
            FROM (
                SELECT DiaryId, SUM([Count]) AS total
                FROM dbo.PostReactionCount
                GROUP BY DiaryId
            ) x;

            -- 5. 全部反應總數
            SELECT ISNULL(SUM([Count]), 0) FROM dbo.PostReactionCount;
            """;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);

        await r.ReadAsync(ct); int diaryCount = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int maxStreak = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int sharedCount = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int maxReactions = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int totalReact = r.GetInt32(0);

        return new DiaryStats
        {
            DiaryCount = diaryCount,
            MaxDiaryStreak = maxStreak,
            SharedDiaryCount = sharedCount,
            MaxReactionsOnSinglePost = maxReactions,
            TotalReactionsReceived = totalReact,
        };
    }

    /// <summary>
    /// 查詢 MoodDiary（習慣打卡）的統計數字。
    /// </summary>
    public static async Task<TaskStats> QueryTaskStatsAsync(
        string connectionString,
        int userId,
        CancellationToken ct = default)
    {
        const string sql = """
            -- 1. 習慣總數
            SELECT COUNT(*) FROM dbo.task WHERE user_id = @UserId;

            -- 2. 進行中習慣數
            SELECT COUNT(*) FROM dbo.task WHERE user_id = @UserId AND status = 'Active';

            -- 3. 打卡總次數（排除 Undo）
            SELECT COUNT(*)
            FROM dbo.task_checkin_log cl
            JOIN dbo.task t ON t.task_id = cl.task_id
            WHERE t.user_id = @UserId AND cl.checkin_type != 'Undo';

            -- 4. 同一習慣最長連續天數
            WITH Numbered AS (
                SELECT cl.task_id, cl.checkin_date,
                       DATEADD(DAY,
                           -CAST(ROW_NUMBER() OVER (PARTITION BY cl.task_id ORDER BY cl.checkin_date) AS INT),
                           cl.checkin_date) AS grp
                FROM (
                    SELECT DISTINCT task_id, checkin_date
                    FROM dbo.task_checkin_log
                    WHERE checkin_type != 'Undo'
                ) cl
                JOIN dbo.task t ON t.task_id = cl.task_id
                WHERE t.user_id = @UserId
            )
            SELECT ISNULL(MAX(cnt), 0)
            FROM (SELECT COUNT(*) AS cnt FROM Numbered GROUP BY task_id, grp) x;

            -- 5. 同一習慣累計最多次數
            SELECT ISNULL(MAX(cnt), 0)
            FROM (
                SELECT cl.task_id, COUNT(*) AS cnt
                FROM dbo.task_checkin_log cl
                JOIN dbo.task t ON t.task_id = cl.task_id
                WHERE t.user_id = @UserId AND cl.checkin_type != 'Undo'
                GROUP BY cl.task_id
            ) x;

            -- 6. 晨型人：連續幾天在 10:00 前有打卡（任一習慣）
            WITH EarlyDays AS (
                SELECT DISTINCT CAST(cl.checkin_at AS DATE) AS day
                FROM dbo.task_checkin_log cl
                JOIN dbo.task t ON t.task_id = cl.task_id
                WHERE t.user_id = @UserId
                  AND cl.checkin_type != 'Undo'
                  AND CAST(cl.checkin_at AS TIME) < '10:00:00'
            ),
            Numbered AS (
                SELECT day,
                       DATEADD(DAY, -CAST(ROW_NUMBER() OVER (ORDER BY day) AS INT), day) AS grp
                FROM EarlyDays
            )
            SELECT ISNULL(MAX(cnt), 0)
            FROM (SELECT COUNT(*) AS cnt FROM Numbered GROUP BY grp) x;

            -- 7. 是否同時有 Daily 和 NonDaily 的 Active 習慣
            SELECT CASE WHEN
                EXISTS (SELECT 1 FROM dbo.task WHERE user_id=@UserId AND status='Active' AND rhythm_type='Daily')
                AND
                EXISTS (SELECT 1 FROM dbo.task WHERE user_id=@UserId AND status='Active' AND rhythm_type='NonDaily')
                THEN 1 ELSE 0 END;

            -- 8. 是否曾達成 NonDaily 習慣的週目標
            WITH WeeklyCounts AS (
                SELECT cl.task_id, r.weekly_target_count,
                       DATEPART(YEAR, cl.checkin_date) AS yr,
                       DATEPART(WEEK, cl.checkin_date) AS wk,
                       COUNT(*) AS cnt
                FROM dbo.task_checkin_log cl
                JOIN dbo.task t ON t.task_id = cl.task_id
                JOIN dbo.task_schedule_rule r ON r.task_id = t.task_id
                WHERE t.user_id = @UserId
                  AND t.rhythm_type = 'NonDaily'
                  AND cl.checkin_type != 'Undo'
                  AND r.weekly_target_count IS NOT NULL
                GROUP BY cl.task_id, r.weekly_target_count,
                         DATEPART(YEAR, cl.checkin_date),
                         DATEPART(WEEK, cl.checkin_date)
            )
            SELECT CASE WHEN EXISTS (SELECT 1 FROM WeeklyCounts WHERE cnt >= weekly_target_count)
                        THEN 1 ELSE 0 END;

            -- 9. 是否某週所有 Active 習慣都有打卡（精準執行）
            WITH WeekTaskCounts AS (
                SELECT DATEPART(YEAR, cl.checkin_date) AS yr,
                       DATEPART(WEEK, cl.checkin_date) AS wk,
                       COUNT(DISTINCT cl.task_id) AS task_cnt
                FROM dbo.task_checkin_log cl
                JOIN dbo.task t ON t.task_id = cl.task_id
                WHERE t.user_id = @UserId AND t.status = 'Active' AND cl.checkin_type != 'Undo'
                GROUP BY DATEPART(YEAR, cl.checkin_date), DATEPART(WEEK, cl.checkin_date)
            )
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM WeekTaskCounts
                WHERE task_cnt >= (SELECT COUNT(*) FROM dbo.task WHERE user_id=@UserId AND status='Active')
                  AND (SELECT COUNT(*) FROM dbo.task WHERE user_id=@UserId AND status='Active') > 0
            ) THEN 1 ELSE 0 END;

            -- 10. Daily 習慣是否曾單週打卡 7 天（每日堅守者）
            WITH DailyWeekly AS (
                SELECT t.task_id,
                       DATEPART(YEAR, cl.checkin_date) AS yr,
                       DATEPART(WEEK, cl.checkin_date) AS wk,
                       COUNT(DISTINCT cl.checkin_date) AS day_cnt
                FROM dbo.task_checkin_log cl
                JOIN dbo.task t ON t.task_id = cl.task_id
                WHERE t.user_id = @UserId AND t.rhythm_type = 'Daily' AND cl.checkin_type != 'Undo'
                GROUP BY t.task_id, DATEPART(YEAR, cl.checkin_date), DATEPART(WEEK, cl.checkin_date)
            )
            SELECT CASE WHEN EXISTS (SELECT 1 FROM DailyWeekly WHERE day_cnt >= 7)
                        THEN 1 ELSE 0 END;

            -- 11. 帳號年齡（天）
            SELECT ISNULL(DATEDIFF(DAY, MIN(created_at), GETDATE()), 0)
            FROM dbo.user_account
            WHERE user_id = @UserId;
            """;

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);

        await r.ReadAsync(ct); int taskCount = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int activeCount = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int checkinCount = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int maxStreak = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int maxCount = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int earlyStreak = r.GetInt32(0);
        await r.NextResultAsync(ct); await r.ReadAsync(ct); bool bothRhythms = r.GetInt32(0) == 1;
        await r.NextResultAsync(ct); await r.ReadAsync(ct); bool metWeekly = r.GetInt32(0) == 1;
        await r.NextResultAsync(ct); await r.ReadAsync(ct); bool allMet = r.GetInt32(0) == 1;
        await r.NextResultAsync(ct); await r.ReadAsync(ct); bool dailyFull = r.GetInt32(0) == 1;
        await r.NextResultAsync(ct); await r.ReadAsync(ct); int accountDays = r.GetInt32(0);

        return new TaskStats
        {
            TaskCount = taskCount,
            ActiveTaskCount = activeCount,
            CheckinCount = checkinCount,
            MaxCheckinStreakSameTask = maxStreak,
            MaxCheckinCountSameTask = maxCount,
            MaxEarlyMorningStreak = earlyStreak,
            HasBothRhythmTypes = bothRhythms,
            MetWeeklyTargetOnce = metWeekly,
            AllActiveTasksMetTargetInWeek = allMet,
            DailyTaskFullWeek = dailyFull,
            AccountAgeDays = accountDays,
        };
    }
}
