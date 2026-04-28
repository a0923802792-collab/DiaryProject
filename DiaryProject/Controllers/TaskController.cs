using DiaryProject.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DiaryProject.Data;
using DiaryProject.Models;
using DiaryProject.Services;
namespace DiaryProject.Controllers
{
    public class TaskController : Controller
    {
        private readonly ITaskService _itaskService;
        public TaskController(ITaskService taskservier)
        {
            _itaskService = taskservier;
        }

        //首頁
        public IActionResult Index()
        {
            //未連接會員資料表 先以1號會員為例
            int userid = 1;
            var tasks = _itaskService.GetTaskList(userid);
            return View(tasks);
        }

        //新增任務
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(TaskCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }
            int userid = 1;
            _itaskService.CreateTask(vm, userid);

            return RedirectToAction("Index");
        }


        [HttpPost]
        public IActionResult Checkin(TaskCheckinViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Index");

            }
            bool result = _itaskService.CheckinTask(vm);
            if (!result)
            {
                //處理打卡失敗的情況，例如顯示錯誤訊息
                TempData["Msg"] = "今天已完成任務";
            }
            else
            {
                TempData["Msg"] = "任務完成~";
            }
            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Archive(int taskId)
        {
            int userid = 1;//未連接會員資料表 先以1號會員為例
            _itaskService.ArchiveTask(taskId, userid);
            return RedirectToAction("Index");
        }
    }
}
