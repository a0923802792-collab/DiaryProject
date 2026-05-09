using Microsoft.AspNetCore.Mvc;
using MyMvcProject.Models;
using MyMvcProject.Services;

namespace MyMvcProject.Controllers;

/// <summary>
/// 分享牆 API 控制器。
/// 不依賴 Service 層，直接呼叫 Model 內的 ShareWallData 靜態類別。
/// </summary>
[ApiController]
[Route("api/sharewall4")]
public sealed class ShareWallController(IConfiguration configuration) : ControllerBase
{
    // 連線字串在建構子時建立一次，不重複建立
    private readonly string _connectionString = ShareWallData.BuildConnectionString(configuration);

    /// <summary>
    /// 取得分享牆貼文清單。
    /// 支援分類、標籤、關鍵字篩選與排序，全部由後端 SQL 執行。
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
    /// 新增貼文反應（按讚等）。
    /// 使用 MERGE 語法確保冪等性：同一 (DiaryId, ReactionType) 重複呼叫只會累加計數。
    /// POST /api/sharewall4/react  Body: { "diaryId": 9, "reactionType": "like" }
    /// </summary>
    [HttpPost("react")]
    public async Task<IActionResult> React(
        [FromBody] ReactRequest req,
        CancellationToken cancellationToken)
    {
        var (success, error) = await ShareWallData.AddReactionAsync(
            _connectionString, req.DiaryId, req.ReactionType, req.VisitorId, cancellationToken);

        if (!success) return BadRequest(new { error });
        return Ok(new { ok = true });
    }
}