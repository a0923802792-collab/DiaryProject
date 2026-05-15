using DiaryProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Controllers
{
    [ApiController]
    [Route("api/diary")]
    public class DiaryApiController : ControllerBase
    {
        private readonly DiarySystemDbContext _context;

        public DiaryApiController(DiarySystemDbContext context)
        {
            _context = context;
        }

        [HttpGet("today-moods")]
        public IActionResult TodayMoods([FromQuery] int? userId)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            var finalUserId = sessionUserId ?? userId;

            if (!finalUserId.HasValue || finalUserId.Value <= 0)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            var moods = _context.Diaries
                .AsNoTracking()
                .Where(d => d.UserId == finalUserId.Value
                    && d.Status == "published"
                    && d.DiaryDate == today
                    && d.TemplateType == "mood")
                .SelectMany(d => d.Moods.Select(m => new
                {
                    moodId = m.MoodId,
                    moodName = m.MoodName,
                    emoji = m.MoodEmoji
                }))
                .Distinct()
                .ToList();

            return Ok(new { moods });
        }

        // =====================================================
        // 2026-05-14
        // 新增：取得指定年月每一天的日記狀態（給首頁星球渲染用）
        // GET /api/diary/month-status?year=2026&month=5
        // =====================================================
        [HttpGet("month-status")]
        public IActionResult MonthStatus([FromQuery] int year, [FromQuery] int month)
        {
            // Step 1：從 Session 取出使用者 ID（如果沒登入就拒絕）
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value <= 0)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            // Step 2：驗證月份參數合理（1~12）
            if (month < 1 || month > 12)
            {
                return BadRequest(new { message = "月份必須是 1 到 12" });
            }

            // Step 3：算出該月的起訖日期（給資料庫查詢用）
            var startDate = new DateOnly(year, month, 1);            // 5/1
            var endDate = startDate.AddMonths(1);                    // 6/1（不含）

            // Step 4：查當月該使用者所有「已發布」的日記
            //         按 CreatedAt 排序，方便等下取「最早那篇」
            var diaries = _context.Diaries
                .AsNoTracking()
                .Include(d => d.Moods)                               // 連帶把心情資料載入（不用第二次查詢）
                .Where(d => d.UserId == userId.Value
                         && d.Status == "published"
                         && d.DiaryDate >= startDate
                         && d.DiaryDate < endDate)
                .OrderBy(d => d.CreatedAt)
                .ToList();

            // Step 5：把日記按「日期」分組，每天只留最早那篇
            var diariesByDate = diaries
                .GroupBy(d => d.DiaryDate)
                .ToDictionary(g => g.Key, g => g.First());
            // ↑ key 是日期、value 是該日期最早的那篇日記

            // Step 6：產生「本月每一天」的回傳列表
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var result = new List<object>();

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(year, month, day);
                var dateStr = date.ToString("yyyy-MM-dd");

                if (diariesByDate.TryGetValue(date, out var diary))
                {
                    // 這天有日記
                    var firstMood = diary.Moods?.FirstOrDefault();   // 取第一個心情（normal 模板會是 null）

                    result.Add(new
                    {
                        date = dateStr,
                        hasDiary = true,
                        diaryId = (long?)diary.DiaryId,
                        previewText = diary.PreviewText,
                        moodId = firstMood?.MoodId,
                        moodEmoji = firstMood?.MoodEmoji
                    });
                }
                else
                {
                    // 這天沒寫日記
                    result.Add(new
                    {
                        date = dateStr,
                        hasDiary = false,
                        diaryId = (long?)null,
                        previewText = (string?)null,
                        moodId = (string?)null,
                        moodEmoji = (string?)null
                    });
                }
            }

            return Ok(result);
        }

        // =====================================================
        // 新增：取得指定日記的第一張照片預覽
        // GET /api/media/preview?diaryId=123
        // =====================================================
        [HttpGet("~/api/media/preview")] // 使用 ~/ 可以覆寫 Controller 預設的路由前綴
        public IActionResult GetPhotoPreview([FromQuery] long diaryId)
        {
            // Step 1: 驗證是否有登入 (防護機制)
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value <= 0)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            // Step 2: 從 Media 資料庫尋找符合條件的照片
            var firstImage = _context.DiaryMedia
                .AsNoTracking()
                .Where(m => m.DiaryId == diaryId && m.MediaType == "image") // 條件：同ID 且是圖片
                .OrderBy(m => m.CreatedAt) // 依照上傳時間排序
                .FirstOrDefault(); // 只拿第一筆 (最早的一張)

            // Step 3: 如果沒找到照片，回傳 null 網址
            if (firstImage == null)
            {
                return Ok(new { fileUrl = (string)null });
            }

            // Step 4: 找到的話，回傳照片的路徑
            return Ok(new { fileUrl = firstImage.FileUrl });
        }

        [HttpGet("today-summary")]
        public IActionResult TodaySummary([FromQuery] int? userId)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            var finalUserId = sessionUserId ?? userId;

            if (!finalUserId.HasValue || finalUserId.Value <= 0)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            var today = DateOnly.FromDateTime(DateTime.Today);

            var diary = _context.Diaries
                .AsNoTracking()
                .Include(d => d.Tags)
                .Include(d => d.DiaryMood)
                .FirstOrDefault(d => d.UserId == finalUserId.Value
                    && d.Status == "published"
                    && d.DiaryDate == today);

            if (diary == null)
            {
                return Ok(new
                {
                    hasDiary = false,
                    diaryId = (int?)null,
                    tags = new List<string>(),
                    moodValue = 0,
                    sleepValue = 0,
                    stressValue = 0
                });
            }

            return Ok(new
            {
                hasDiary = true,
                diaryId = diary.DiaryId,
                tags = diary.Tags.Select(t => t.TagName).ToList(),
                moodValue = diary.DiaryMood?.EnergyValue ?? 0,
                sleepValue = diary.DiaryMood?.SleepValue ?? 0,
                stressValue = diary.DiaryMood?.StressValue ?? 0
            });
        }

    }
}