using Microsoft.Data.SqlClient;
using DiaryProject.Infrastructure;
using DiaryProject.Models;

namespace DiaryProject.Services;

// =====================================================================
// 分享牆資料存取層
//
// 這個類別的職責：
//   - 呼叫 DiaryRepository 取得原始資料列（Row）
//   - 把 Row 組裝成分享牆專屬的回應物件（ShareWallPost）
//   - 處理分享牆業務規則（哪個 TagType 算分類、反應類型對應、排序邏輯）
//
// 不再包含：
//   - SQL 語句（交給 DiaryRepository）
//   - 連線字串建立（交給 DatabaseFactory）
// =====================================================================
public static class ShareWallData
{
    /// <summary>
    /// 分享牆允許的反應類型白名單（業務規則，不屬於 Repository）
    /// </summary>
    private static readonly string[] AllowedReactionTypes = ["like", "peace", "hug", "empathy", "cheer"];

    // -----------------------------------------------------------------
    // 公開方法
    // -----------------------------------------------------------------

    /// <summary>
    /// 依篩選條件查詢分享牆資料。
    /// 流程：呼叫 Repository 取得原始資料 → 組裝成 PostBuilder → 排序 → 回傳。
    /// </summary>
    public static async Task<ShareWallResponse> QueryAsync(
        string connectionString,
        ShareWallQuery query,
        CancellationToken cancellationToken = default)
    {
        // 分類清單不受貼文篩選影響，永遠顯示全部
        var categories = await DiaryRepository.GetCategoryNamesAsync(connectionString, cancellationToken);

        // 把 ShareWallQuery（業務層參數）轉換成 DiaryFilter（Repository 參數）
        var filter = new DiaryFilter
        {
            Category = query.Category,
            Tag = query.Tag,
            Keyword = query.Q,
            Visibility = "shared",
            Status = "published",
        };

        var diaryRows = await DiaryRepository.GetDiariesAsync(connectionString, filter, cancellationToken);
        if (diaryRows.Count == 0)
            return new ShareWallResponse { Categories = categories };

        // 以 DiaryId 為 key 建立暫存字典，方便後續批次填入標籤/圖片/反應
        var builders = diaryRows.ToDictionary(r => r.DiaryId, r => new PostBuilder
        {
            Id = r.DiaryId,
            SortDate = DateOnly.FromDateTime(r.DiaryDate),
            SortTime = r.DiaryTime,
            Date = $"{r.DiaryDate.Year} 年 {r.DiaryDate.Month} 月 {r.DiaryDate.Day} 日",
            Time = $"{r.DiaryTime.Hours:D2}:{r.DiaryTime.Minutes:D2}",
            Title = r.Title,
            // 優先使用 Body；Body 為空時退回 PreviewText
            Content = string.IsNullOrWhiteSpace(r.Body) ? r.PreviewText ?? string.Empty : r.Body,
        });

        var diaryIds = builders.Keys.ToArray();

        // 用同一條連線批次讀取關聯資料（減少連線開銷）
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        ApplyTags(builders, await DiaryRepository.GetTagsAsync(conn, diaryIds, cancellationToken));
        ApplyImages(builders, await DiaryRepository.GetImagesAsync(conn, diaryIds, cancellationToken));
        ApplyReactions(builders, await DiaryRepository.GetReactionsAsync(conn, diaryIds, cancellationToken));

        // 業務排序：hot 依總反應數；latest 依日期時間
        var ordered = query.Sort == "hot"
            ? builders.Values.OrderByDescending(p => p.TotalReactions).ThenByDescending(p => p.SortDate).ThenByDescending(p => p.SortTime)
            : builders.Values.OrderByDescending(p => p.SortDate).ThenByDescending(p => p.SortTime);

        // 分頁：計算 Skip / Take，多取一筆用來判斷 hasMore
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var paged = ordered.Skip(skip).Take(pageSize + 1).Select(p => p.ToResponse()).ToList();
        var hasMore = paged.Count > pageSize;
        if (hasMore) paged.RemoveAt(paged.Count - 1); // 移除多取的那一筆

        return new ShareWallResponse
        {
            Categories = categories,
            Posts = paged,
            HasMore = hasMore,
        };
    }

    /// <summary>
    /// 新增一筆反應。先驗證業務規則（合法類型白名單），再交給 Repository 寫入。
    /// </summary>
    public static async Task<(bool Success, string Error)> AddReactionAsync(
        string connectionString,
        long diaryId,
        string reactionType,
        string? visitorId = null,
        CancellationToken cancellationToken = default)
    {
        // 業務規則驗證：只允許白名單內的反應類型
        if (!AllowedReactionTypes.Contains(reactionType.ToLowerInvariant()))
            return (false, $"不允許的反應類型：{reactionType}");

        var inserted = await DiaryRepository.UpsertReactionAsync(
            connectionString, diaryId, reactionType.ToLowerInvariant(), visitorId, cancellationToken);

        // inserted = false 表示 DB 層判斷為重複，前端应視為成功（不需要顯示錯誤）
        return (true, string.Empty);
    }

    /// <summary>
    /// 取得資料庫連線字串（委託給 DatabaseFactory）。
    /// Controller 透過這個方法取得連線字串，不需要直接依賴 Infrastructure 層。
    /// </summary>
    public static string BuildConnectionString(IConfiguration configuration)
        => DatabaseFactory.GetConnectionString(configuration);

    // -----------------------------------------------------------------
    // 私有組裝方法（業務邏輯：把 Raw Row 填入 PostBuilder）
    // -----------------------------------------------------------------

    /// <summary>
    /// 將標籤資料列填入對應的 PostBuilder。
    ///
    /// 業務規則（分享牆專屬）：
    ///   - TagType 為 'system' 或 'Category' 的第一個標籤 → 設為貼文分類
    ///   - 其餘標籤 → 格式化為 #標籤名
    /// </summary>
    private static void ApplyTags(
        IReadOnlyDictionary<long, PostBuilder> builders,
        IEnumerable<TagRow> rows)
    {
        foreach (var row in rows)
        {
            if (!builders.TryGetValue(row.DiaryId, out var b)) continue;

            if (string.IsNullOrWhiteSpace(b.Category) && IsCategoryTag(row.TagType))
                b.Category = row.TagName;
            else
                b.Tags.Add($"#{row.TagName}");
        }
    }

    /// <summary>
    /// 將圖片資料列填入對應的 PostBuilder。
    /// 業務規則：每篇只取第一張（Repository 已依 CreatedAt 排序，取到就跳過後續）。
    /// </summary>
    private static void ApplyImages(
        IReadOnlyDictionary<long, PostBuilder> builders,
        IEnumerable<MediaRow> rows)
    {
        foreach (var row in rows)
        {
            if (!builders.TryGetValue(row.DiaryId, out var b)) continue;
            b.Images.Add(row.FileUrl);
        }
    }

    /// <summary>
    /// 將反應計數資料列填入對應的 PostBuilder。
    ///
    /// 業務規則（分享牆專屬）：
    ///   - peace 與 relief 視為同一種（相容舊資料）
    ///   - 其他類型依 switch 對應到強型別欄位
    /// </summary>
    private static void ApplyReactions(
        IReadOnlyDictionary<long, PostBuilder> builders,
        IEnumerable<ReactionRow> rows)
    {
        foreach (var row in rows)
        {
            if (!builders.TryGetValue(row.DiaryId, out var b)) continue;

            switch (row.ReactionType.Trim().ToLowerInvariant())
            {
                case "like": b.Reactions.Like += row.Count; break;
                case "peace":
                case "relief": b.Reactions.Peace += row.Count; break;
                case "hug": b.Reactions.Hug += row.Count; break;
                case "empathy": b.Reactions.Empathy += row.Count; break;
                case "cheer": b.Reactions.Cheer += row.Count; break;
            }
        }
    }

    /// <summary>判斷 TagType 是否屬於「分類」（業務規則，不放在 Repository）</summary>
    private static bool IsCategoryTag(string tagType)
        => tagType.Equals("Category", StringComparison.OrdinalIgnoreCase)
        || tagType.Equals("system", StringComparison.OrdinalIgnoreCase);

    // -----------------------------------------------------------------
    // 內部暫存輔助類別
    // -----------------------------------------------------------------

    /// <summary>
    /// 查詢過程中暫存單筆貼文的可變狀態。
    /// 批次填入標籤/圖片/反應後，呼叫 ToResponse() 轉換為對外不可變的 ShareWallPost。
    /// </summary>
    private sealed class PostBuilder
    {
        public long Id { get; init; }
        public DateOnly SortDate { get; init; }
        public TimeSpan SortTime { get; init; }
        public string Date { get; init; } = string.Empty;
        public string Time { get; init; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Tags { get; } = [];
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public List<string> Images { get; } = [];
        public MutableReactions Reactions { get; } = new();

        public int TotalReactions =>
            Reactions.Like + Reactions.Peace + Reactions.Hug + Reactions.Empathy + Reactions.Cheer;

        public ShareWallPost ToResponse()
        {
            List<string> tags;
            if (Tags.Count > 0)
                tags = [.. Tags.Distinct(StringComparer.Ordinal)];
            else if (string.IsNullOrWhiteSpace(Category))
                tags = [];
            else
                tags = [$"#{Category}"];

            return new ShareWallPost
            {
                Id = Id,
                Date = Date,
                Time = Time,
                Category = string.IsNullOrWhiteSpace(Category) ? "未分類" : Category,
                Tags = tags,
                Title = Title,
                Content = Content,
                Images = [.. Images],
                Reactions = new ShareWallReactions
                {
                    Like = Reactions.Like,
                    Peace = Reactions.Peace,
                    Hug = Reactions.Hug,
                    Empathy = Reactions.Empathy,
                    Cheer = Reactions.Cheer,
                },
            };
        }
    }

    private sealed class MutableReactions
    {
        public int Like { get; set; }
        public int Peace { get; set; }
        public int Hug { get; set; }
        public int Empathy { get; set; }
        public int Cheer { get; set; }
    }
}
