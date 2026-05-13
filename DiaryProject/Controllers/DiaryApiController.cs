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

        [HttpGet("month-status")]
        public IActionResult MonthStatus([FromQuery] int year, [FromQuery] int month)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (!sessionUserId.HasValue) return Unauthorized();

            var startDate = new DateOnly(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var diaries = _context.Diaries
                .AsNoTracking()
                .Include(d => d.Moods)
                .Where(d => d.UserId == sessionUserId.Value
                    && d.Status == "published"
                    && d.DiaryDate >= startDate
                    && d.DiaryDate < endDate)
                .Select(d => new
                {
                    date = d.DiaryDate.ToString("yyyy / MM / dd"),
                    diaryId = d.DiaryId,
                    previewText = d.PreviewText,
                    // 取第一個 mood 當「主要心情」
                    dominantMoodId = d.Moods.Select(m => m.MoodId).FirstOrDefault(),
                    dominantMoodEmoji = d.Moods.Select(m => m.MoodEmoji).FirstOrDefault()
                })
                .ToList();

            return Ok(diaries);
        }
    }
}