using DiaryProject.Data;
using DiaryProject.Models.Front;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace DiaryProject.Controllers
{
    // 2026-05-16
    // 通知 API
    // GET  /api/notifications           → 取最近 20 則 + 未讀數
    // POST /api/notifications/mark-read → 把全部未讀標成已讀
    [ApiController]
    [Route("api/notifications")]
    public class NotificationApiController : ControllerBase
    {
        private readonly AppDbContext _appDb;

        public NotificationApiController(AppDbContext appDb)
        {
            _appDb = appDb;
        }

        // 登入者的通知清單
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value <= 0)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            // 2026-05-17 新增：先看使用者的通知開關
            // 用 _appDb.Users 因為 User 是註冊在 AppDbContext
            var notifEnabled = await _appDb.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId.Value)
                .Select(u => u.IsNotificationEnabled)
                .FirstOrDefaultAsync();

            // 如果使用者關掉通知 → 直接回空陣列、未讀數 0
            if (!notifEnabled)
            {
                return Ok(new { notifications = new object[0], unreadCount = 0 });
            }

            var notifications = await _appDb.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    type = n.Type,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt,
                })
                .ToListAsync();

            var unreadCount = await _appDb.Notifications
                .AsNoTracking()
                .CountAsync(n => n.UserId == userId.Value && !n.IsRead);

            return Ok(new { notifications, unreadCount });
        }

        [HttpPost("mark-read")]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value <= 0)
                return Unauthorized(new { message = "尚未登入" });

            var unread = await _appDb.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread) n.IsRead = true;
            await _appDb.SaveChangesAsync();

            return Ok(new { ok = true, marked = unread.Count });
        }
    }
}
