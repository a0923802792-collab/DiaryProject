using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DiaryProject.Models;

namespace DiaryProject.Controllers;

// ============================================================
// HomeController — 頁面導覽控制器
//
// 職責：只負責「回傳對應的 View」，完全不包含業務邏輯。
//
// 架構說明：
//   本專案採用「頁面 Controller + API Controller」分離設計：
//   - HomeController（這裡）→ 回傳 HTML 頁面（Razor View）
//   - ShareWallController  → 回傳 JSON 資料（給前端 JS 呼叫）
//   - ChartController      → 回傳 JSON 資料
//   - AchievementController→ 回傳 JSON 資料
//
// 頁面與路由對應：
//   /                     → Sharewall4()  → Views/Home/sharewall4.cshtml（預設首頁）
//   /Home/Index           → Index()        → Views/Home/Index.cshtml
//   /Home/Achievement     → Achievement()  → Views/Home/Achievement.cshtml
//   /Home/Chart           → Chart()        → Views/Home/Chart.cshtml
//   /Home/Privacy         → Privacy()      → Views/Home/Privacy.cshtml
//   /Home/Error           → Error()        → Views/Shared/Error.cshtml
// ============================================================
public class HomeController : Controller
{
    /// <summary>
    /// 分享牆頁面（預設首頁）。
    /// 只回傳 View，實際資料由前端呼叫 GET /api/sharewall4 取得。
    /// </summary>
    public IActionResult Sharewall4()
    {
        return View("sharewall4");
    }

    /// <summary>
    /// 首頁。目前為預留頁面，尚未實作功能。
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// 隱私政策頁面。
    /// </summary>
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>
    /// 成就牆頁面。
    /// 只回傳 View，實際成就資料由前端呼叫 GET /api/achievement 取得。
    /// </summary>
    public IActionResult Achievement()
    {
        return View();
    }

    /// <summary>
    /// 數據圖表頁面。
    /// 只回傳 View，實際圖表資料由前端呼叫 GET /api/chart 取得。
    /// </summary>
    public IActionResult Chart()
    {
        return View();
    }

    /// <summary>
    /// 錯誤頁面。
    /// ResponseCache 設定確保錯誤頁面不會被瀏覽器快取（每次都拿最新的錯誤資訊）。
    /// RequestId 用於在錯誤頁面顯示追蹤 ID，方便開發者對應 log。
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
