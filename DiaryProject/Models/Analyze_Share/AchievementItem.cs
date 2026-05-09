namespace MyMvcProject.Models;

// =====================================================================
// 單一成就的資料結構
// 負責描述「一個成就是什麼、解鎖了沒、目前進度」
//
// 建立方式：由 AchievementData.BuildAchievements() 工廠方法統一建立，
//           不應由外部直接 new AchievementItem { ... } 建構。
// =====================================================================
public sealed class AchievementItem
{
    /// <summary>成就編號（1–25），用於前端顯示排序</summary>
    public int Id { get; init; }

    /// <summary>成就 Emoji 圖示，例如 "✏️"（已解鎖）或 "🔒"（前端自行替換為鎖頭）</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>成就名稱，例如「初次執筆」</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>成就描述，例如「建立第 1 篇日記」；前端用於 title tooltip</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>是否已解鎖：true = 彩色顯示，false = 灰色 + 進度條</summary>
    public bool IsUnlocked { get; init; }

    /// <summary>
    /// 目前進度值（例如：已連續 3 天）。
    /// 前端用於進度條計算：<c>pct = Progress / MaxProgress * 100</c>
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// 解鎖所需值（例如：需要 5 天）。
    /// 若 MaxProgress &lt;= 1 代表此成就是布林型（達成/未達成），不顯示進度條。
    /// </summary>
    public int MaxProgress { get; init; }
}
