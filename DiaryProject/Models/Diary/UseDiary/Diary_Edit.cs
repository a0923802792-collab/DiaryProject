namespace DiaryProject.ViewModels.Diary;

// 新增/編輯日記使用的 ViewModel。
public class Diary_Edit
{
    public int DiaryId { get; set; }

    // normal / mood
    public string TemplateType { get; set; } = "normal";

    // 日記基本資料
    public string DiaryDate { get; set; } = string.Empty;
    public string DiaryTime { get; set; } = string.Empty;
    public string WeatherType { get; set; } = string.Empty;

    // 一般日記內容
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    // 標籤資料
    public List<string> TagName { get; set; } = new List<string>();
    public List<string> SystemTagName { get; set; } = new List<string>();
    public List<string> UserCustomTagName { get; set; } = new List<string>();

    // 心情模板選項與已選心情
    public List<Diary_MoodSelection> MoodSelection { get; set; } = new List<Diary_MoodSelection>();
    public List<string> MoodId { get; set; } = new List<string>();

    // 心情模板量表
    public int? EnergyValue { get; set; }
    public int? StressValue { get; set; }
    public int? SleepValue { get; set; }

    // 心情模板文字欄位
    public string EventNote { get; set; } = string.Empty;
    public string ThoughtNote { get; set; } = string.Empty;
    public string NeedNote { get; set; } = string.Empty;

    // 編輯頁既有媒體
    public List<Diary_EditMediaItem> MediaItems { get; set; } = new List<Diary_EditMediaItem>();
}

public class Diary_EditMediaItem
{
    public string Kind { get; set; } = string.Empty; // image / drawing
    public string Src { get; set; } = string.Empty;
}
