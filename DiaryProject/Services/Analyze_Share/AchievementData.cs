using DiaryProject.Infrastructure;
using DiaryProject.Models;

namespace DiaryProject.Services;

// =====================================================================
// AchievementData — 業務邏輯層
// 負責：呼叫 Repository 取得原始統計，評估 25 個成就是否解鎖
// 不直接碰 SqlConnection，不知道資料從哪個 DB 來
// =====================================================================
public static class AchievementData
{

    /// <summary>
    /// 取得全部 25 個成就的解鎖狀態。
    /// 若任一 DB 連線失敗（例如 MoodDiary 尚未建立），
    /// 該資料庫的成就全部顯示為未解鎖，不會整頁爆炸。
    /// </summary>
    public static async Task<AchievementResponse> GetAchievementsAsync(
        string diaryConnStr,
        string taskConnStr,
        int userId,
        CancellationToken ct = default)
    {
        // 兩個 DB 獨立查詢，任一失敗都用空統計繼續
        DiaryStats diary;
        TaskStats tasks;

        try { diary = await AchievementRepository.QueryDiaryStatsAsync(diaryConnStr, userId, ct); }
        catch { diary = new DiaryStats(); }

        try { tasks = await AchievementRepository.QueryTaskStatsAsync(taskConnStr, userId, ct); }
        catch { tasks = new TaskStats(); }

        var items = BuildAchievements(diary, tasks);

        return new AchievementResponse
        {
            UnlockedCount = items.Count(a => a.IsUnlocked),
            TotalCount = items.Count,
            Items = items,
        };
    }

    // ── 私有：評估 25 個成就 ─────────────────────────────────────────
    private static IReadOnlyList<AchievementItem> BuildAchievements(DiaryStats d, TaskStats t)
    {
        var list = new List<AchievementItem>
        {
            // ── 日記類（1-6）───────────────────────────────────────
            A(1,  "✏️", "初次執筆",       "建立第 1 篇日記",            d.DiaryCount >= 1,             d.DiaryCount,                1),
            A(2,  "📖", "三日筆耕",       "連續 3 天記錄日記",           d.MaxDiaryStreak >= 3,         d.MaxDiaryStreak,            3),
            A(3,  "🗓️", "七日不輟",       "連續 7 天記錄日記",           d.MaxDiaryStreak >= 7,         d.MaxDiaryStreak,            7),
            A(4,  "🌙", "月光書寫者",     "連續 30 天記錄日記",          d.MaxDiaryStreak >= 30,        d.MaxDiaryStreak,            30),
            A(5,  "📣", "首次分享",       "第 1 篇日記設為公開",          d.SharedDiaryCount >= 1,       d.SharedDiaryCount,          1),
            A(6,  "🏙️", "十篇分享",       "累計 10 篇公開分享",          d.SharedDiaryCount >= 10,      d.SharedDiaryCount,          10),

            // ── 互動類（7-10）──────────────────────────────────────
            A(7,  "🎉", "初獲共鳴",       "日記收到第 1 個反應",          d.TotalReactionsReceived >= 1, d.TotalReactionsReceived,    1),
            A(8,  "🌟", "人氣貼文",       "單篇日記反應總數 ≥ 10",        d.MaxReactionsOnSinglePost >= 10, d.MaxReactionsOnSinglePost, 10),
            A(9,  "🔥", "熱門創作者",     "單篇日記反應總數 ≥ 50",        d.MaxReactionsOnSinglePost >= 50, d.MaxReactionsOnSinglePost, 50),
            A(10, "💛", "給予溫暖",       "對別人送出 5 次反應",
                        d.ReactionsGiven >= 5, d.ReactionsGiven, 5),

            // ── 習慣建立類（11-13）─────────────────────────────────
            A(11, "🌱", "習慣萌芽",       "建立第 1 個習慣",             t.TaskCount >= 1,              t.TaskCount,                 1),
            A(12, "🧩", "多面手",         "同時有 3 個進行中的習慣",      t.ActiveTaskCount >= 3,        t.ActiveTaskCount,           3),
            A(13, "🗂️", "習慣收藏家",     "累計建立過 5 個習慣",          t.TaskCount >= 5,              t.TaskCount,                 5),

            // ── 晨型人（14）────────────────────────────────────────
            A(14, "🌅", "晨型人",         "連續 5 天在 10:00 前打卡",    t.MaxEarlyMorningStreak >= 5,  t.MaxEarlyMorningStreak,     5),

            // ── 習慣達成類（15-22）─────────────────────────────────
            A(15, "👣", "第一步",         "第 1 次習慣打卡",              t.CheckinCount >= 1,           t.CheckinCount,              1),
            A(16, "🔗", "三日連線",       "同一習慣連續完成 3 天",         t.MaxCheckinStreakSameTask >= 3,  t.MaxCheckinStreakSameTask, 3),
            A(17, "💪", "七日挑戰",       "同一習慣連續完成 7 天",         t.MaxCheckinStreakSameTask >= 7,  t.MaxCheckinStreakSameTask, 7),
            A(18, "🏆", "二十一天養成",   "同一習慣累計完成 21 次",        t.MaxCheckinCountSameTask >= 21, t.MaxCheckinCountSameTask, 21),
            A(19, "💎", "六十次突破",     "同一習慣累計完成 60 次",        t.MaxCheckinCountSameTask >= 60, t.MaxCheckinCountSameTask, 60),
            A(20, "📅", "週週達標",       "NonDaily 習慣達成當週目標",     t.MetWeeklyTargetOnce,         t.MetWeeklyTargetOnce ? 1 : 0, 1),
            A(21, "🎯", "精準執行",       "某週所有進行中習慣全部達標",    t.AllActiveTasksMetTargetInWeek, t.AllActiveTasksMetTargetInWeek ? 1 : 0, 1),
            A(22, "☀️", "每日堅守者",     "Daily 習慣單週全勤（7 天）",   t.DailyTaskFullWeek,           t.DailyTaskFullWeek ? 1 : 0, 1),

            // ── 綜合類（23-24）─────────────────────────────────────
            A(23, "🎲", "全類型挑戰",     "同時擁有 Daily 與 NonDaily 習慣", t.HasBothRhythmTypes,      t.HasBothRhythmTypes ? 1 : 0, 1),
            A(24, "🕰️", "老朋友",         "帳號使用滿 30 天",              t.AccountAgeDays >= 30,        t.AccountAgeDays,            30),
        };

        // 25 號成就：解鎖任意 15 個（動態計算，放最後）
        int unlocked = list.Count(x => x.IsUnlocked);
        list.Add(A(25, "👑", "生活大師", "解鎖任意 15 個成就", unlocked >= 15, unlocked, 15));

        return list;
    }

    /// <summary>建立一個 AchievementItem 的簡短工廠方法（減少重複程式碼）</summary>
    private static AchievementItem A(
        int id, string icon, string name, string desc,
        bool isUnlocked, int progress, int max) =>
        new()
        {
            Id = id,
            Icon = icon,
            Name = name,
            Description = desc,
            IsUnlocked = isUnlocked,
            Progress = Math.Min(progress, max),
            MaxProgress = max,
        };
}
