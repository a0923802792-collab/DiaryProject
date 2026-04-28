using DiaryProject.ViewModels;
using DiaryProject.Data;
using DiaryProject.Models;

namespace DiaryProject.Services
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;
        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        public List<TaskListItemViewModel> GetTaskList(int userId)
        {
            DateTime today = DateTime.Today;

            // 使用者任務清單，包含是否已完成今日打卡
            var list = _context.Tasks
                .Where(x => x.UserId == userId && x.Status == "Active")
                .Select(x => new TaskListItemViewModel
                {
                    TaskId = x.TaskId,
                    Title = x.Title,
                    RhythmType = x.RhythmType,
                    Status = x.Status,
                    IsCompletedToday = _context.TaskChecking
                        .Any(c => c.TaskId == x.TaskId && c.CheckingDate == today),

                    RhythmTypeText = x.RhythmType == "Daily" ? "每日" : "非每日",
                    StatusText = x.Status == "Active" ? "進行中" : "已封存"

                })
                .ToList();

            return list;
        }
        public void CreateTask(TaskCreateViewModel vm, int userId)
        {
            var task = new TaskItem
            {
                UserId = userId,
                Title = vm.Title,
                RhythmType = vm.RhythmType,
                Status = "Active",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.Tasks.Add(task);
            _context.SaveChanges();

            var rule = new TaskScheduleRule
            {
                TaskId = task.TaskId,
                WeeklyTargetCount = vm.WeeklyTargetCount,
                StartDate = DateTime.Now.Date,
                EndDate = null
            };
            _context.TaskScheduleRules.Add(rule);
            _context.SaveChanges();
        }


        public bool CheckinTask(TaskCheckinViewModel vm)
        {
            bool exists = _context.TaskChecking
                .Any(x => x.TaskId == vm.TaskId && x.CheckingDate == vm.CheckinDate.Date);

            if (exists)
            {
                return false;
            }
            var checkin = new TaskChecking
            {
                TaskId = vm.TaskId,
                CheckingDate = vm.CheckinDate,
                CheckinAt = DateTime.Now,
                CheckinType = vm.CheckinType
            };

            _context.TaskChecking.Add(checkin);
            _context.SaveChanges();

            return true;
        }

        public void ArchiveTask(int taskId, int userId)
        {
            var task = _context.Tasks.FirstOrDefault(x => x.TaskId == taskId && x.UserId==userId);
            if (task == null)
            {
                return;
            }
            task.Status= "Archived";
            task.UpdatedAt= DateTime.Now;

            var rule = _context.TaskScheduleRules
                .FirstOrDefault(x => x.TaskId == taskId);

            if(rule != null && rule.EndDate == null)
            {
                rule.EndDate = DateTime.Now.Date;
            }

            _context.SaveChanges();
        }
    }
}
