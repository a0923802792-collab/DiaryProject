namespace DiaryProject.ViewModels.Review
{
    public class ReviewCalendarDayViewModel
    {
        public long? DiaryId { get; set; }

        public DateTime Date { get; set; }

        public string DateValue => Date.ToString("yyyy-MM-dd");

        public int DayNumber { get; set; }

        public bool IsCurrentMonth { get; set; }

        public bool IsFuture { get; set; }

        public bool HasDiary { get; set; }

        public bool HasPhoto { get; set; }

        public bool IsSelected { get; set; }

        public int DiaryCount { get; set; }

        public string? MainMoodName { get; set; }

        public string? MainMoodEmoji { get; set; }

        public int? StressValue { get; set; }
    }
}