namespace DiaryProject.ViewModels.Diary;

    // 詳情頁 ViewModel（目前單一模型同時支援 normal / mood 顯示）。
    public class Diary_Detail
    {
        // Diary 主表
        public int DiaryId { get; set; }
        public string DiaryDate { get; set; } = string.Empty;
        public string DiaryTime { get; set; } = string.Empty;
        public string WeatherType { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;

        // 模板型態：normal / mood
        public string TemplateType { get; set; } = "normal";

        // normal 內容欄位
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        // mood 內容欄位
        public string MoodEmoji { get; set; } = string.Empty;
        public List<string> MoodChips { get; set; } = new List<string>();
        public int? EnergyValue { get; set; }
        public int? StressValue { get; set; }
        public int? SleepValue { get; set; }
        public string EventNote { get; set; } = string.Empty;
        public string ThoughtNote { get; set; } = string.Empty;
        public string NeedNote { get; set; } = string.Empty;

        // 多對多：DiaryTag + Tag
        public List<string> TagName { get; set; } = new List<string>();

        // 一對多：DiaryMedia
        public List<string> MediaUrl { get; set; } = new List<string>();
    }
