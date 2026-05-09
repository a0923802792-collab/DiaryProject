namespace MyMvcProject.Models;

// =====================================================================
// 反應計數（對應 PostReactionCount 資料表）
//
// 反應類型白名單由 ShareWallData.AllowedReactionTypes 維護，
// 若未來新增反應類型，需同時修改：
//   1. 這裡新增屬性
//   2. ShareWallData.AllowedReactionTypes 加入新類型
//   3. DiaryRepository.UpsertReactionAsync
//   4. render/postList.js 的按鈕 HTML
// =====================================================================
public sealed class ShareWallReactions
{
    /// <summary>👍 按讚數</summary>
    public int Like { get; init; }

    /// <summary>😌 平靜數</summary>
    public int Peace { get; init; }

    /// <summary>🤗 擁抱數</summary>
    public int Hug { get; init; }

    /// <summary>🥺 感同身受數</summary>
    public int Empathy { get; init; }

    /// <summary>💪 加油數</summary>
    public int Cheer { get; init; }
}
