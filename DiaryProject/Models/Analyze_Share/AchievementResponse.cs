namespace MyMvcProject.Models;

// =====================================================================
// GET /api/achievement 的回傳結構
// 負責包裝整份成就清單 + 解鎖統計
// =====================================================================
public sealed class AchievementResponse
{
    /// <summary>已解鎖的成就數量（前端顯示「已解鎖 X 個」）</summary>
    public int UnlockedCount { get; init; }

    /// <summary>成就總數（目前固定 25）</summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 全部成就清單（固定 25 筆，依 Id 1–25 順序排列）。
    /// 使用 IReadOnlyList 確保外部無法修改清單內容。
    /// </summary>
    public IReadOnlyList<AchievementItem> Items { get; init; } = [];
}
