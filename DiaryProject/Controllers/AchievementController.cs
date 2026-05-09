using Microsoft.AspNetCore.Mvc;
using MyMvcProject.Infrastructure;
using MyMvcProject.Services;

namespace MyMvcProject.Controllers;

/// <summary>
/// 成就牆 API 控制器。
/// GET /api/achievement → 回傳 25 個成就與解鎖狀態。
/// </summary>
[ApiController]
[Route("api/achievement")]
public sealed class AchievementController(IConfiguration configuration) : ControllerBase
{
    // 一般日記 DB（master）的連線字串
    private readonly string _diaryConnStr = DatabaseFactory.GetConnectionString(configuration);
    // 習慣任務 DB 的連線字串
    private readonly string _taskConnStr = DatabaseFactory.GetEmotionTaskConnectionString(configuration);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await AchievementData.GetAchievementsAsync(
            _diaryConnStr, _taskConnStr, AchievementData.DefaultUserId, ct);

        return Ok(result);
    }
}
