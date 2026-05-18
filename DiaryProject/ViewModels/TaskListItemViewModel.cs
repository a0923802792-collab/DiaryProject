using System;
namespace DiaryProject.ViewModels
{
    public class TaskListItemViewModel
    {
        public int TaskId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string RhythmType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool IsCompletedToday { get; set; }

        public string RhythmTypeText { get; set; } = string.Empty;
        public string StatusText { get; set; }=string.Empty;

        public int? WeeklyTargetCount { get; set; }
        public int ThisWeekCompletedCount { get; set; }
        public bool IsWeeklyGoalReached { get; set; }
    }
}
