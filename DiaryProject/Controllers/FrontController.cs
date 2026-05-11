using Microsoft.AspNetCore.Mvc;
using DiaryProject.Models;
using Microsoft.EntityFrameworkCore;

namespace DiaryProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FrontController : ControllerBase
    {
        private readonly DiarySystemDbContext _db;

        public FrontController(DiarySystemDbContext db)
        {
            _db = db;
        }

        // API 1：查某個月份所有有日記的日期
        // GET /api/front/month?userId=1&year=2026&month=5
        [HttpGet("month")]
        public async Task<IActionResult> GetMonthDiaries(int userId, int year, int month)
        {
            var diaries = await _db.Diaries
                .Where(d => d.UserId == userId
                         && d.DiaryDate.Year == year
                         && d.DiaryDate.Month == month
                         && d.Status != "deleted")
                .Select(d => new
                {
                    DiaryId = d.DiaryId,
                    DiaryDate = d.DiaryDate.ToString("yyyy-MM-dd"),
                    TemplateType = d.TemplateType,
                    PreviewText = d.PreviewText,
                })
                .ToListAsync();

            return Ok(diaries);
        }

        // API 2：查某一天的日記完整內容
        // GET /api/front/by-date?userId=1&date=2026-05-01
        [HttpGet("by-date")]
        public async Task<IActionResult> GetDiaryByDate(int userId, string date)
        {
            if (!DateOnly.TryParse(date, out var parsedDate))
                return BadRequest("日期格式錯誤，請用 yyyy-MM-dd");

            var diary = await _db.Diaries
                .Include(d => d.DiaryNormal)
                .Include(d => d.DiaryMood)
                .FirstOrDefaultAsync(d => d.UserId == userId
                                       && d.DiaryDate == parsedDate
                                       && d.Status != "deleted");

            if (diary == null)
                return NotFound("這天沒有日記");

            return Ok(new
            {
                diaryId = diary.DiaryId,
                diaryDate = diary.DiaryDate.ToString("yyyy-MM-dd"),
                templateType = diary.TemplateType,
                previewText = diary.PreviewText,

                // 一般日記
                title = diary.DiaryNormal?.Title,
                body = diary.DiaryNormal?.Body,

                // 心情日記
                energyValue = diary.DiaryMood?.EnergyValue,
                stressValue = diary.DiaryMood?.StressValue,
                sleepValue = diary.DiaryMood?.SleepValue,
            });
        }

        // GET /api/front/today-summary?userId=1
        [HttpGet("today-summary")]
        public async Task<IActionResult> GetTodaySummary(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var diary = await _db.Diaries
                .Include(d => d.DiaryMood)
                .Include(d => d.Tags)
                .FirstOrDefaultAsync(d =>
                    d.UserId == userId &&
                    d.DiaryDate == today &&
                    d.Status != "deleted");

            if (diary == null)
            {
                return Ok(new
                {
                    hasDiary = false,
                    tags = new List<string>(),
                    moodValue = 0,
                    sleepValue = 0,
                    stressValue = 0,
                });
            }

            var tagNames = diary.Tags
                .Select(t => t.TagName)
                .ToList();

            return Ok(new
            {
                hasDiary = true,
                tags = tagNames,
                moodValue = diary.DiaryMood?.EnergyValue ?? 0,
                sleepValue = diary.DiaryMood?.SleepValue ?? 0,
                stressValue = diary.DiaryMood?.StressValue ?? 0,
            });
        }

        // GET /api/front/today-moods?userId=1
        [HttpGet("today-moods")]
        public async Task<IActionResult> GetTodayMoods(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var diary = await _db.Diaries
                .Include(d => d.Moods)
                .FirstOrDefaultAsync(d =>
                    d.UserId == userId &&
                    d.DiaryDate == today &&
                    d.Status != "deleted");

            if (diary == null)
                return Ok(new { moods = new List<object>() });

            var moods = diary.Moods.Select(m => new
            {
                moodId = m.MoodId,
                moodName = m.MoodName,
                emoji = m.MoodEmoji,
            }).ToList();

            return Ok(new { moods });
        }
    }
}