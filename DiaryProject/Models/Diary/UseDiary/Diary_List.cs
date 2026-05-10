using System.Collections.Generic;

namespace DiaryProject.ViewModels.Diary;

// 列表頁：單篇日記卡片資料
public class Diary_List
    {
        public long DiaryId { get; set; }
        public string DiaryDate { get; set; } = string.Empty;

        // normal / mood
        public string TemplateType { get; set; } = "normal";

        // 一般模板欄位
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;

        // 心情模板欄位
        public string MoodEmoji { get; set; } = string.Empty;
        public string EventNote { get; set; } = string.Empty;
        public string ThoughtNote { get; set; } = string.Empty;
        public string NeedNote { get; set; } = string.Empty;

        // 列表摘要：
        // normal: Body
        // mood  : EventNote + ThoughtNote + NeedNote
        public string PreviewText { get; set; } = string.Empty;

        // 標籤與媒體數
        public List<string> TagName { get; set; } = new List<string>();
        public int ImageCount { get; set; }
        public int DrawingCount { get; set; }
        public bool IsShared { get; set; }

        // 來自 PostReactionCount（格式例："👍 5"）
        public List<string> Reactions { get; set; } = new List<string>();
    }


