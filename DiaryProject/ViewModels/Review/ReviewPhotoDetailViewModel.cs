namespace DiaryProject.ViewModels.Review
{
    public class ReviewPhotoDetailViewModel
    {
        public long DiaryId { get; set; }

        public DateTime DiaryDate { get; set; }

        public string FullDateText => DiaryDate.ToString("yyyy/MM/dd");

        public string? PreviewText { get; set; }

        public string? MainMoodName { get; set; }

        public string? MainMoodEmoji { get; set; }

        public List<string> Tags { get; set; } = new();

        public bool IsFeatured { get; set; }

        public string StartMediaId { get; set; } = "";

        public List<ReviewPhotoItemViewModel> Photos { get; set; } = new();
    }
}