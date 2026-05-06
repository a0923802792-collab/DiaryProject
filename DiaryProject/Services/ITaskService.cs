
using DiaryProject.ViewModels;
namespace DiaryProject.Services
{
    public interface ITaskService
    {
        List<TaskListItemViewModel> GetTaskList(int userId);
        int CreateTask(TaskCreateViewModel vm, int userId);
        int UpdateTask(TaskEditViewModel vm, int userId);
        bool CheckinTask(TaskCheckinViewModel vm);

        void ArchiveTask(int taskId, int userId);

        TaskDetailViewModel GetTaskDetail(int taskId, int userId);
        TaskEditViewModel? GetTaskEditData(int taskId, int userId);

    }
}
