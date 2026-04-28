namespace DiaryProject.ViewModels
{
    public class TaskDetailViewModel
    {

        public int TaskId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string RhythmType { get; set; } = string.Empty;
        public string RhythmTypeText { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public int? WeeklyTargetCount { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime? LastCheckinDate { get; set; }

        public int TotalCheckinCount { get; set; }

        public bool IsCompletedToday { get; set; }
    }
}
