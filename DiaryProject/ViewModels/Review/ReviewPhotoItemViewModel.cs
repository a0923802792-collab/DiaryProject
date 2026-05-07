namespace DiaryProject.ViewModels.Review
{
    public class ReviewPhotoItemViewModel
    {
        public string MediaId { get; set; } = "";

        public long DiaryId { get; set; }

        public string FileUrl { get; set; } = "";

        public string MediaType { get; set; } = "";

        public DateTime DiaryDate { get; set; }

        public string DateText => DiaryDate.ToString("M/d");

        public string FullDateText => DiaryDate.ToString("yyyy/MM/dd");

        public string? PreviewText { get; set; }

        public string? MainMoodName { get; set; }

        public string? MainMoodEmoji { get; set; }

        public List<string> Tags { get; set; } = new();

        public bool IsFeatured { get; set; }
    }
}