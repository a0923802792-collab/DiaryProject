namespace DiaryProject.Models;

// =====================================================================
// 單筆貼文資料（對外回傳）
// =====================================================================
public sealed class ShareWallPost
{
    /// <summary>日記 ID（對應 Diary.DiaryId）</summary>
    public long Id { get; init; }

    /// <summary>格式化日期，例如「2026 年 4 月 9 日」</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>格式化時間，例如「18:30」</summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>貼文分類名稱</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>標籤清單，格式為 #標籤名</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>標題</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>內文</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>封面圖片 URL，無圖時為 null</summary>
    public List<string> Images { get; init; } = [];

    /// <summary>各類型反應計數</summary>
    public ShareWallReactions Reactions { get; init; } = new();
}
