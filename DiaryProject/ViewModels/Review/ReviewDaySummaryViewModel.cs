namespace DiaryProject.ViewModels.Review
{
    public class ReviewDaySummaryViewModel
    {
        public long DiaryId { get; set; }

        public DateTime DiaryDate { get; set; }

        public string DateText => DiaryDate.ToString("yyyy/MM/dd");

        public string TemplateType { get; set; } = "";

        public string? Title { get; set; }

        public string? PreviewText { get; set; }

        public string? WeatherType { get; set; }

        public string? MainMoodName { get; set; }

        public string? MainMoodEmoji { get; set; }

        public int? EnergyValue { get; set; }

        public int? StressValue { get; set; }

        public int? SleepValue { get; set; }

        public List<string> Tags { get; set; } = new();

        public List<string> PhotoUrls { get; set; } = new();
    }
}