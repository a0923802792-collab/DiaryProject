namespace DiaryProject.ViewModels.Review
{
    public class ReviewTimePageViewModel
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public DateTime SelectedDate { get; set; }

        public List<ReviewCalendarDayViewModel> CalendarDays { get; set; } = new();

        public List<ReviewDaySummaryViewModel> RecentDiaries { get; set; } = new();

        public ReviewDaySummaryPanelViewModel SelectedDayPanel { get; set; } = new();
    }
}