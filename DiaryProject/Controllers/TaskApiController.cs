using DiaryProject.Services;
using Microsoft.AspNetCore.Mvc;

namespace DiaryProject.Controllers
{
    [ApiController]
    [Route("api/task")]
    public class TaskApiController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TaskApiController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpGet("list")]
        public IActionResult List([FromQuery] int? userId)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            var finalUserId = sessionUserId ?? userId;

            if (!finalUserId.HasValue || finalUserId.Value <= 0)
            {
                return Unauthorized(new { message = "尚未登入" });
            }

            try
            {
                var tasks = _taskService.GetTaskList(finalUserId.Value)
                    .Select(t => new
                    {
                        taskId = t.TaskId,
                        title = t.Title,
                        rhythmType = t.RhythmType,
                        status = t.Status
                    })
                    .ToList();

                return Ok(new { tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "取得任務資料失敗",
                    detail = ex.Message
                });
            }
        }
    }
}