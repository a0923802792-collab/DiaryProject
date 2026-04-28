
using DiaryProject.ViewModels;
namespace DiaryProject.Services
{
    public interface ITaskService
    {
        List<TaskListItemViewModel> GetTaskList(int userId);
        void CreateTask(TaskCreateViewModel vm, int userId);
        bool CheckinTask(TaskCheckinViewModel vm);

        void ArchiveTask(int taskId, int userId);
    }
}
