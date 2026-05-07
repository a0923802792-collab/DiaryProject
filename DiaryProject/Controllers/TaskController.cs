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
        public IActionResult Index(int? selectedTaskId = null)
        {
            int userId = 1;
            List<TaskListItemViewModel> tasks;

            try
            {
                tasks = _itaskService.GetTaskList(userId);
            }
            catch
            {
                tasks = new List<TaskListItemViewModel>();
            }

            ViewBag.SelectedTaskId = selectedTaskId;
            ViewBag.ToastMessage = TempData["ToastMessage"]?.ToString();

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

            int userId = 1;
            int newTaskId = _itaskService.CreateTask(vm, userId);

            TempData["ToastMessage"] = "新增任務成功";
            return RedirectToAction("Index", new { selectedTaskId = newTaskId });
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

        public IActionResult Detail (int id)
        {
            int userid = 1;//未連接會員資料表 先以1號會員為例
            var taskDetail = _itaskService.GetTaskDetail(id, userid);
            if (taskDetail == null)
            {
                return NotFound();
            }
            return View(taskDetail);
        }
        public IActionResult DetailPanel(int id)
        {
            int userId = 1;

            var vm = _itaskService.GetTaskDetail(id, userId);

            if (vm == null)
            {
                return NotFound();
            }

            return PartialView("_TaskDetailPartial", vm);
        }



        [HttpGet]
        public IActionResult Edit(int id)
        {
            int userId = 1;

            var vm = _itaskService.GetTaskEditData(id, userId);

            if (vm == null)
            {
                return NotFound();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(TaskEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            int userId = 1;
            int taskId = _itaskService.UpdateTask(vm, userId);

            TempData["ToastMessage"] = "編輯任務成功";
            return RedirectToAction("Index", new { selectedTaskId = taskId });
        }

        public IActionResult EditPanel(int id)
        {
            int userId = 1;

            var vm = _itaskService.GetTaskEditData(id, userId);

            if (vm == null)
            {
                return NotFound();
            }

            return PartialView("_TaskEditPartial", vm);
        }
    }
}
