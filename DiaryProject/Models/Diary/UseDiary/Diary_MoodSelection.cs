namespace DiaryProject.ViewModels.Diary;

    // 編輯頁情緒選項（來自 Mood 主檔）
    public class Diary_MoodSelection
    {
        public string MoodId { get; set; } = string.Empty;
        public string MoodName { get; set; } = string.Empty;
        public string MoodEmoji { get; set; } = string.Empty;

        // true = 這篇日記已選到此情緒
        public bool IsSelected { get; set; }
    }
