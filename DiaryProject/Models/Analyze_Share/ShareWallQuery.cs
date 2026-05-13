namespace DiaryProject.Models;

// =====================================================================
// 查詢參數：前端透過 Query String 傳入篩選與排序條件
// 範例：GET /api/sharewall4?category=感情&tag=旅遊&q=關鍵字&sort=hot
// =====================================================================
public sealed class ShareWallQuery
{
    /// <summary>依分類名稱篩選（對應 Tag.TagType='system'），null 表示不篩選</summary>
    public string? Category { get; init; }

    /// <summary>依標籤名稱篩選（不含 # 符號），null 表示不篩選</summary>
    public string? Tag { get; init; }

    /// <summary>關鍵字搜尋，比對標題與內文，null 表示不篩選</summary>
    public string? Q { get; init; }

    /// <summary>排序方式：latest（最新，依日期時間）或 hot（熱門，依總回應數）</summary>
    public string Sort { get; init; } = "latest";

    /// <summary>頁碼（1-based），預設第 1 頁</summary>
    public int Page { get; init; } = 1;

    /// <summary>每頁筆數，預設 10 筆</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>訪客識別 ID（來自 identity.js），用於回傳「我的反應」，null 表示不查詢</summary>
    public string? VisitorId { get; init; }
}
