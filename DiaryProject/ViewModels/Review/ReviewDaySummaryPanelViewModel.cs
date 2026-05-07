namespace DiaryProject.ViewModels.Review
{
    public class ReviewDaySummaryPanelViewModel
    {
        public DateTime Date { get; set; }

        public string DateText => Date.ToString("yyyy/MM/dd");

        public bool HasDiary => Diary != null;

        public bool IsFuture { get; set; }

        public ReviewDaySummaryViewModel? Diary { get; set; }
    }
}