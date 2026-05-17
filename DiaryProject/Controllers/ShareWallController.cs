using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DiaryProject.Data;
using DiaryProject.Models;
using DiaryProject.Models.Front;
using DiaryProject.Services;

namespace DiaryProject.Controllers;

/// <summary>
/// 分享牆 API 控制器
/// </summary>
[ApiController]
[Route("api/sharewall4")]
public sealed class ShareWallController : ControllerBase
{
    private readonly string _connectionString;
    private readonly AppDbContext _appDb;
    private readonly DiarySystemDbContext _diaryDb;

    // 2026-05-16
    // 加入 AppDbContext（寫通知）和 DiarySystemDbContext（查日記主人 + reactor 暱稱）
    public ShareWallController(
        IConfiguration configuration,
        AppDbContext appDb,
        DiarySystemDbContext diaryDb)
    {
        _connectionString = ShareWallData.BuildConnectionString(configuration);
        _appDb = appDb;
        _diaryDb = diaryDb;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] ShareWallQuery query,
        CancellationToken cancellationToken)
    {
        var response = await ShareWallData.QueryAsync(_connectionString, query, cancellationToken);
        return Ok(response);
    }

    [HttpPost("react")]
    public async Task<IActionResult> React(
        [FromBody] ReactRequest req,
        CancellationToken cancellationToken)
    {
        var sessionUserId = HttpContext.Session.GetInt32("UserId");
        var visitorId = sessionUserId.HasValue
            ? $"user-{sessionUserId.Value}"
            : req.VisitorId;

        var (success, error) = await ShareWallData.AddReactionAsync(
            _connectionString,
            req.DiaryId,
            req.ReactionType,
            visitorId,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new { error });
        }

        // 2026-05-16
        // 新增：reaction 成功後，寫一筆通知給日記主人。
        // 只有「登入者對別人的日記」按 reaction 時才寫通知，匿名訪客或自己按自己都不算。
        if (sessionUserId.HasValue)
        {
            await TryCreateNotificationAsync(
                req.DiaryId,
                sessionUserId.Value,
                req.ReactionType,
                cancellationToken);
        }

        return Ok(new { ok = true });
    }

    /// <summary>
    /// 嘗試寫入通知。失敗不影響 reaction 主流程，靜默吞掉錯誤。
    /// </summary>
    private async Task TryCreateNotificationAsync(
        long diaryId,
        int reactorUserId,
        string reactionType,
        CancellationToken ct)
    {
        try
        {
            // 1. 找日記主人
            var ownerId = await _diaryDb.Diaries
                .AsNoTracking()
                .Where(d => d.DiaryId == diaryId)
                .Select(d => (int?)d.UserId)
                .FirstOrDefaultAsync(ct);

            if (ownerId == null) return;
            if (ownerId.Value == reactorUserId) return; // 不通知自己按自己

            // 2. 找 reactor 暱稱
            var reactorName = await _appDb.Users
                .AsNoTracking()
                .Where(u => u.UserId == reactorUserId)
                .Select(u => u.Nickname)
                .FirstOrDefaultAsync(ct);

            // 3. 組通知文字
            var emoji = ReactionToEmoji(reactionType);
            var displayName = string.IsNullOrWhiteSpace(reactorName) ? "有人" : reactorName;
            var title = $"{displayName} {emoji} 了你的日記";

            // 4. 寫入
            _appDb.Notifications.Add(new Notification
            {
                UserId = ownerId.Value,
                Title = title,
                Type = "Social",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
            });
            await _appDb.SaveChangesAsync(ct);
        }
        catch
        {
            // 通知寫入失敗不該讓 reaction 失敗，靜默忽略
        }
    }

    private static string ReactionToEmoji(string reactionType)
    {
        return reactionType.Trim().ToLowerInvariant() switch
        {
            "like" => "👍 讚",
            "love" => "❤️ 愛",
            "hug" => "🤗 關懷",
            "empathy" => "🥺 共感",
            "cheer" => "💪 大力支持",
            _ => "👀 看了"
        };
    }
}