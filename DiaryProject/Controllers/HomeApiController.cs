using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/home")]
public class HomeApiController : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        return Ok(new
        {
            userName = "測試使用者",
            message = "React 已成功連到 ASP.NET API",
            taskCount = 3
        });
    }
}