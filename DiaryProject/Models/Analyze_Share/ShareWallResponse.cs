namespace MyMvcProject.Models;

// =====================================================================
// API 回傳：分享牆完整資料
// =====================================================================
public sealed class ShareWallResponse
{
    /// <summary>所有可用分類清單（來自 Tag 資料表 TagType='system'），不受篩選影響</summary>
    public List<string> Categories { get; init; } = [];

    /// <summary>依篩選條件過濾後的貼文清單</summary>
    public List<ShareWallPost> Posts { get; init; } = [];

    /// <summary>是否還有更多資料可供載入（用於無限捲動）</summary>
    public bool HasMore { get; init; }
}
