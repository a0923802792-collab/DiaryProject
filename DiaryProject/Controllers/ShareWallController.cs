using Microsoft.AspNetCore.Mvc;
using DiaryProject.Models;
using DiaryProject.Services;

namespace DiaryProject.Controllers;

/// <summary>
/// 分享牆 API 控制器。
/// 不依賴 Service 層，直接呼叫 Model 內的 ShareWallData 靜態類別。
/// </summary>
[ApiController]
[Route("api/sharewall4")]
public sealed class ShareWallController(IConfiguration configuration) : ControllerBase
{
    private readonly string _connectionString = ShareWallData.BuildConnectionString(configuration);

    /// <summary>
    /// 取得分享牆貼文清單。
    /// GET /api/sharewall4?category=感情&tag=旅遊&q=關鍵字&sort=hot
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] ShareWallQuery query,
        CancellationToken cancellationToken)
    {
        var response = await ShareWallData.QueryAsync(_connectionString, query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// 新增貼文反應。
    /// Session 有登入者時優先使用 Session 的 UserId；
    /// 若尚未登入，才退回前端傳入的 VisitorId。
    /// POST /api/sharewall4/react
    /// </summary>
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

        return Ok(new { ok = true });
    }
}