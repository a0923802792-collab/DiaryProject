namespace DiaryProject.Models.Front
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }   // 用戶通知
        public string Title { get; set; } // 標題 -> EX: "Max likes your post!"
        public string Type { get; set; }  // 類型 -> EX: "System", "Habit", "Social"
        public bool IsRead { get; set; } = false; // 已讀狀態 -> 預設為未讀
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}