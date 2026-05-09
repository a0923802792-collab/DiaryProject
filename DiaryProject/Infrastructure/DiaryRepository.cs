using Microsoft.Data.SqlClient;

namespace MyMvcProject.Infrastructure;

// =====================================================================
// 原始資料列（Raw Row）— 資料庫欄位的直接對應，不含業務邏輯
//
// 為什麼要定義這些 Row 類別？
//   DiaryRepository 只負責「從資料庫讀資料」，
//   它不知道呼叫方（ShareWallData、其他頁面）想怎麼用這些資料。
//   用中性的 Row 物件回傳，讓呼叫方自己決定如何組裝成最終格式。
// =====================================================================

/// <summary>日記基本資料列（對應 Diary + DiaryNormal 聯查結果）</summary>
public sealed class DiaryRow
{
    public long DiaryId { get; init; }
    public DateTime DiaryDate { get; init; }
    public TimeSpan DiaryTime { get; init; }
    public string TemplateType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? PreviewText { get; init; }
}

/// <summary>標籤資料列（對應 DiaryTag + Tag 聯查結果）</summary>
public sealed class TagRow
{
    public long DiaryId { get; init; }
    public string TagName { get; init; } = string.Empty;
    public string TagType { get; init; } = string.Empty;
}

/// <summary>媒體資料列（對應 DiaryMedia）</summary>
public sealed class MediaRow
{
    public long DiaryId { get; init; }
    public string FileUrl { get; init; } = string.Empty;
}

/// <summary>反應計數資料列（對應 PostReactionCount）</summary>
public sealed class ReactionRow
{
    public long DiaryId { get; init; }
    public string ReactionType { get; init; } = string.Empty;
    public int Count { get; init; }
}

// =====================================================================
// DiaryFilter — 呼叫方傳入的查詢條件
//
// 為什麼不直接用 ShareWallQuery？
//   ShareWallQuery 是分享牆專屬的 API 參數，帶有排序等業務邏輯。
//   DiaryFilter 只描述「要撈哪些日記」，不含排序，保持通用性。
//   未來其他頁面也可以傳自己的 filter 進來。
// =====================================================================
public sealed class DiaryFilter
{
    /// <summary>只撈符合此分類標籤的日記（TagType='system' 或 'Category'），null 代表不限</summary>
    public string? Category { get; init; }

    /// <summary>只撈含有此標籤的日記（不限 TagType），null 代表不限</summary>
    public string? Tag { get; init; }

    /// <summary>關鍵字，比對標題或內文，null 代表不限</summary>
    public string? Keyword { get; init; }

    /// <summary>只撈符合此 Visibility 的日記，null 代表不限（預設 'shared'）</summary>
    public string? Visibility { get; init; } = "shared";

    /// <summary>只撈符合此 Status 的日記，null 代表不限（預設 'published'）</summary>
    public string? Status { get; init; } = "published";
}

// =====================================================================
// DiaryRepository — 純資料存取，不含任何業務邏輯
//
// 設計原則：
//   - 每個方法只做一件事：組 SQL、執行、回傳 Row 物件
//   - 不知道呼叫方是誰，不做任何組裝或格式化
//   - 所有業務判斷（「分類標籤」的定義、反應類型的對應）
//     都留給呼叫方（ShareWallData）處理
// =====================================================================
public static class DiaryRepository
{
    /// <summary>
    /// 依篩選條件查詢日記清單。
    /// 回傳的是原始欄位值，呼叫方負責格式化（日期字串、內文截斷等）。
    /// </summary>
    /// <param name="connectionString">資料庫連線字串</param>
    /// <param name="filter">篩選條件，null 則使用預設值（shared + published）</param>
    public static async Task<List<DiaryRow>> GetDiariesAsync(
        string connectionString,
        DiaryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new DiaryFilter();

        var sql = """
            SELECT d.DiaryId,
                   d.DiaryDate,
                   d.DiaryTime,
                   d.TemplateType,
                   d.PreviewText,
                   COALESCE(n.Title, N'情緒日記')                             AS Title,
                   COALESCE(n.Body,
                       COALESCE(m.EventNote, N'') + N' ' +
                       COALESCE(m.ThoughtNote, N'') + N' ' +
                       COALESCE(m.NeedNote, N''))                             AS Body
            FROM dbo.Diary AS d
            LEFT JOIN dbo.DiaryNormal AS n ON n.DiaryId = d.DiaryId AND d.TemplateType = 'normal'
            LEFT JOIN dbo.DiaryMood   AS m ON m.DiaryId = d.DiaryId AND d.TemplateType = 'mood'
            WHERE (d.TemplateType = 'normal' OR d.TemplateType = 'mood')
            """;

        // 動態附加條件：只有有值才加，保持 SQL 乾淨
        if (!string.IsNullOrWhiteSpace(filter.Visibility))
            sql += "\n  AND d.Visibility = @Visibility";

        if (!string.IsNullOrWhiteSpace(filter.Status))
            sql += "\n  AND d.Status = @Status";

        if (!string.IsNullOrWhiteSpace(filter.Category))
            sql += """

              AND EXISTS (
                  SELECT 1 FROM dbo.DiaryTag dt
                  INNER JOIN dbo.Tag t ON t.TagId = dt.TagId
                  WHERE dt.DiaryId = d.DiaryId
                    AND t.TagName  = @Category
                    AND (t.TagType = 'system' OR t.TagType = 'Category')
              )
            """;

        if (!string.IsNullOrWhiteSpace(filter.Tag))
            sql += """

              AND EXISTS (
                  SELECT 1 FROM dbo.DiaryTag dt
                  INNER JOIN dbo.Tag t ON t.TagId = dt.TagId
                  WHERE dt.DiaryId = d.DiaryId
                    AND t.TagName  = @Tag
              )
            """;

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
            sql += """

              AND (
                  (d.TemplateType = 'normal' AND (n.Title LIKE '%' + @Keyword + '%' OR n.Body LIKE '%' + @Keyword + '%'))
                  OR
                  (d.TemplateType = 'mood'   AND (N'情緒日記' LIKE '%' + @Keyword + '%' OR m.EventNote LIKE '%' + @Keyword + '%' OR m.ThoughtNote LIKE '%' + @Keyword + '%' OR m.NeedNote LIKE '%' + @Keyword + '%'))
                  OR
                  EXISTS (
                      SELECT 1 FROM dbo.DiaryTag dt
                      INNER JOIN dbo.Tag t ON t.TagId = dt.TagId
                      WHERE dt.DiaryId = d.DiaryId AND t.TagName LIKE '%' + @Keyword + '%'
                  )
                  OR
                  CONVERT(NVARCHAR(10), d.DiaryDate, 111) LIKE '%' + @Keyword + '%'
                  OR
                  (CAST(YEAR(d.DiaryDate) AS NVARCHAR) + N' 年 ' + CAST(MONTH(d.DiaryDate) AS NVARCHAR) + N' 月 ' + CAST(DAY(d.DiaryDate) AS NVARCHAR) + N' 日') LIKE '%' + @Keyword + '%'
              )
            """;

        sql += "\nORDER BY d.DiaryDate DESC, d.DiaryTime DESC, d.DiaryId DESC;";

        var rows = new List<DiaryRow>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);

        if (!string.IsNullOrWhiteSpace(filter.Visibility)) cmd.Parameters.AddWithValue("@Visibility", filter.Visibility);
        if (!string.IsNullOrWhiteSpace(filter.Status)) cmd.Parameters.AddWithValue("@Status", filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Category)) cmd.Parameters.AddWithValue("@Category", filter.Category);
        if (!string.IsNullOrWhiteSpace(filter.Tag)) cmd.Parameters.AddWithValue("@Tag", filter.Tag);
        if (!string.IsNullOrWhiteSpace(filter.Keyword)) cmd.Parameters.AddWithValue("@Keyword", filter.Keyword);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var previewOrd = reader.GetOrdinal("PreviewText");
            rows.Add(new DiaryRow
            {
                DiaryId = reader.GetInt64(reader.GetOrdinal("DiaryId")),
                DiaryDate = reader.GetDateTime(reader.GetOrdinal("DiaryDate")),
                DiaryTime = reader.GetTimeSpan(reader.GetOrdinal("DiaryTime")),
                TemplateType = reader.GetString(reader.GetOrdinal("TemplateType")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Body = reader.GetString(reader.GetOrdinal("Body")),
                PreviewText = reader.IsDBNull(previewOrd) ? null : reader.GetString(previewOrd),
            });
        }
        return rows;
    }

    /// <summary>
    /// 批次讀取指定 DiaryId 清單的所有分類標籤（TagType='system'）名稱。
    /// 用於前端分類選項，不受貼文篩選影響。
    /// </summary>
    public static async Task<List<string>> GetCategoryNamesAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TagName
            FROM dbo.Tag
            WHERE TagType = 'system'
            ORDER BY TagName;
            """;

        var list = new List<string>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(reader.GetString(0));
        return list;
    }

    /// <summary>
    /// 批次讀取指定 DiaryId 清單的所有標籤（含 TagName 與 TagType）。
    /// 回傳所有 TagType，由呼叫方決定哪些是「分類」、哪些是「一般標籤」。
    /// </summary>
    /// <param name="connection">已開啟的資料庫連線（共用連線，減少連線開銷）</param>
    /// <param name="diaryIds">目標 DiaryId 陣列</param>
    public static async Task<List<TagRow>> GetTagsAsync(
        SqlConnection connection,
        IReadOnlyCollection<long> diaryIds,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT dt.DiaryId, t.TagName, t.TagType
            FROM dbo.DiaryTag AS dt
            INNER JOIN dbo.Tag AS t ON t.TagId = dt.TagId
            WHERE dt.DiaryId IN ({string.Join(", ", diaryIds)})
            ORDER BY dt.DiaryId, t.TagType, t.TagName;
            """;

        var rows = new List<TagRow>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new TagRow
            {
                DiaryId = reader.GetInt64(reader.GetOrdinal("DiaryId")),
                TagName = reader.GetString(reader.GetOrdinal("TagName")),
                TagType = reader.GetString(reader.GetOrdinal("TagType")),
            });
        return rows;
    }

    /// <summary>
    /// 批次讀取指定 DiaryId 清單的圖片（每篇依 CreatedAt 排序，取全部回傳）。
    /// 呼叫方決定要取第一張還是全部。
    /// </summary>
    public static async Task<List<MediaRow>> GetImagesAsync(
        SqlConnection connection,
        IReadOnlyCollection<long> diaryIds,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT DiaryId, FileUrl
            FROM dbo.DiaryMedia
            WHERE MediaType = 'Image'
              AND DiaryId IN ({string.Join(", ", diaryIds)})
            ORDER BY DiaryId, CreatedAt;
            """;

        var rows = new List<MediaRow>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new MediaRow
            {
                DiaryId = reader.GetInt64(reader.GetOrdinal("DiaryId")),
                FileUrl = reader.GetString(reader.GetOrdinal("FileUrl")),
            });
        return rows;
    }

    /// <summary>
    /// 批次讀取指定 DiaryId 清單的反應計數（來自 PostReactionCount）。
    /// 回傳原始 ReactionType 字串和 Count，呼叫方決定如何對應到業務模型。
    /// </summary>
    public static async Task<List<ReactionRow>> GetReactionsAsync(
        SqlConnection connection,
        IReadOnlyCollection<long> diaryIds,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT DiaryId, ReactionType, Count AS ReactionCount
            FROM dbo.PostReactionCount
            WHERE DiaryId IN ({string.Join(", ", diaryIds)});
            """;

        var rows = new List<ReactionRow>();
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new ReactionRow
            {
                DiaryId = reader.GetInt64(reader.GetOrdinal("DiaryId")),
                ReactionType = reader.GetString(reader.GetOrdinal("ReactionType")),
                Count = reader.GetInt32(reader.GetOrdinal("ReactionCount")),
            });
        return rows;
    }

    /// <summary>
    /// 新增或累加一筆反應到 PostReactionCount。
    ///
    /// 若傳入 visitorId：
    ///   先嘗試在 PostReactionLog 插入 (DiaryId, ReactionType, VisitorId)，
    ///   若 UNIQUE 約束衝突（已按過）就直接返回 false，不累加計數。
    ///   成功插入後再 MERGE PostReactionCount 累加 +1。
    ///
    /// 若 visitorId 為 null：
    ///   直接 MERGE 累加（向下相容舊行為）。
    ///
    /// ★ 未來擴充：visitorId 改為真實 userId 後，此方法完全不需要改。
    /// </summary>
    /// <returns>true = 本次有效寫入；false = 重複，已略過</returns>
    public static async Task<bool> UpsertReactionAsync(
        string connectionString,
        long diaryId,
        string reactionType,
        string? visitorId = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // ── 有 visitorId：先做 DB 層防重複 ──────────────────────
        if (visitorId is not null)
        {
            const string logSql = """
                IF NOT EXISTS (
                    SELECT 1 FROM dbo.PostReactionLog
                    WHERE DiaryId = @DiaryId AND ReactionType = @ReactionType AND VisitorId = @VisitorId
                )
                BEGIN
                    INSERT INTO dbo.PostReactionLog (DiaryId, ReactionType, VisitorId, CreatedAt)
                    VALUES (@DiaryId, @ReactionType, @VisitorId, SYSUTCDATETIME());
                    SELECT 1;   -- 代表「本次是新增」
                END
                ELSE
                    SELECT 0;   -- 代表「已存在，略過」
                """;

            await using var logCmd = new SqlCommand(logSql, conn);
            logCmd.Parameters.AddWithValue("@DiaryId", diaryId);
            logCmd.Parameters.AddWithValue("@ReactionType", reactionType);
            logCmd.Parameters.AddWithValue("@VisitorId", visitorId);

            var result = await logCmd.ExecuteScalarAsync(cancellationToken);
            if (Convert.ToInt32(result) == 0) return false; // 重複，不累加
        }
        // ────────────────────────────────────────────────────────

        const string sql = """
            MERGE dbo.PostReactionCount AS target
            USING (SELECT @DiaryId AS DiaryId, @ReactionType AS ReactionType) AS source
                ON target.DiaryId      = source.DiaryId
               AND target.ReactionType = source.ReactionType
            WHEN MATCHED THEN
                UPDATE SET Count     = target.Count + 1,
                           UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (DiaryId, ReactionType, Count, UpdatedAt)
                VALUES (@DiaryId, @ReactionType, 1, SYSUTCDATETIME());
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@DiaryId", diaryId);
        cmd.Parameters.AddWithValue("@ReactionType", reactionType);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }
}
