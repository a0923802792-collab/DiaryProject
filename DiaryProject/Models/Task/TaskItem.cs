using System;
namespace DiaryProject.Models.Task
{
    public class TaskItem
    {
        public int TaskId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
       
        //任務節奏類型
        public string RhythmType { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
