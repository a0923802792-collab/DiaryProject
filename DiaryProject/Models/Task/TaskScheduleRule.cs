using System;
namespace DiaryProject.Models.Task
{
    public class TaskScheduleRule
    {
        public int RuleId { get; set; }
        public int TaskId { get; set; }
        public int? WeeklyTargetCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
