using System;
namespace DiaryProject.Models
{
    public class TaskChecking
    {
        public int CheckingId { get; set; }
        public int TaskId { get; set; }
        public DateTime CheckingDate { get; set; }
        public DateTime CheckinAt { get; set; }
        public string CheckinType { get; set; } = string.Empty;

    }
}
