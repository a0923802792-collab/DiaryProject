using Microsoft.Data.SqlClient;

namespace MyMvcProject.Infrastructure;

/// <summary>
/// 資料庫連線工廠
///
/// 為什麼要獨立出來？
///   連線字串的建立邏輯（讀 appsettings、覆寫 catalog）
///   如果散落在各個 DataService，日後改密碼或換資料庫名稱，
///   就要到處找、到處改。集中在這裡，全專案只有一個地方負責。
///
/// 使用方式：
///   var connStr = DatabaseFactory.GetConnectionString(configuration);
///   await using var conn = DatabaseFactory.CreateConnection(configuration);
/// </summary>
public static class DatabaseFactory
{
    /// <summary>
    /// 從 appsettings 讀取 DefaultConnection，並將 InitialCatalog 覆寫為指定資料庫。
    /// </summary>
    /// <param name="configuration">DI 注入的 IConfiguration</param>
    /// <param name="catalog">要連線的資料庫名稱，預設為 master</param>
    /// <returns>完整連線字串</returns>
    public static string GetConnectionString(IConfiguration configuration, string? catalog = null)
    {
        var raw = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("appsettings 中找不到 ConnectionStrings:DefaultConnection。");

        // 資料庫名稱優先順序：
        //   1. 呼叫方明確指定的 catalog 參數
        //   2. appsettings.json 的 DatabaseCatalog 設定
        //   3. 以上都沒有則預設 MoodDiary
        var resolvedCatalog = catalog
            ?? configuration["DatabaseCatalog"]
            ?? "MoodDiary";

        // SqlConnectionStringBuilder 可安全修改連線字串的個別屬性，
        // 不需要自己用字串拼接（拼錯容易造成連線失敗或 SQL Injection）
        return new SqlConnectionStringBuilder(raw) { InitialCatalog = resolvedCatalog }.ConnectionString;
    }

    /// <summary>
    /// 建立並開啟一個 SqlConnection（已 OpenAsync，可直接使用）。
    /// 呼叫方需搭配 await using 確保連線正確關閉。
    ///
    /// 使用範例：
    ///   await using var conn = await DatabaseFactory.OpenConnectionAsync(cfg, ct);
    /// </summary>
    public static async Task<SqlConnection> OpenConnectionAsync(
        IConfiguration configuration,
        string? catalog = null,
        CancellationToken cancellationToken = default)
    {
        var conn = new SqlConnection(GetConnectionString(configuration, catalog));
        await conn.OpenAsync(cancellationToken);
        return conn;
    }

    /// <summary>
    /// 取得任務資料所用連線字串；合併後與主資料庫同樣指向 MoodDiary。
    /// </summary>
    public static string GetEmotionTaskConnectionString(IConfiguration configuration)
        => GetConnectionString(configuration);
}
