using System.Collections.Generic;

namespace DiaryProject.ViewModels.Diary;

// 列表頁 ViewModel
// Diaries：要顯示的卡片資料
// Total、Normal/MoodCount：右上角統計使用
public class Diary_ListAll
    {
        public List<Diary_List> Diaries { get; set; } = new List<Diary_List>();

        public int TotalCount { get; set; }
        public int NormalCount { get; set; }
        public int MoodCount { get; set; }
    }

