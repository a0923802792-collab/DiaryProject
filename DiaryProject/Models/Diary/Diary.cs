using DiaryProject.Models.Diary;

namespace DiaryProject.Models.Diary
{
    public class Diary
    {
        public long DiaryId { get; set; }

        public int UserId { get; set; }

        public string TemplateType { get; set; } = "";

        public string? PreviewText { get; set; }

        public DateTime DiaryDate { get; set; }

        public TimeSpan DiaryTime { get; set; }

        public string? WeatherType { get; set; }

        public string Visibility { get; set; } = "private";

        public string Status { get; set; } = "draft";

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        public DiaryNormal? DiaryNormal { get; set; }

        public DiaryMood? DiaryMood { get; set; }

        public ICollection<DiaryTag> DiaryTags { get; set; } = new List<DiaryTag>();

        public ICollection<DiaryMoodSelection> DiaryMoodSelections { get; set; } = new List<DiaryMoodSelection>();

        public ICollection<DiaryMedia> DiaryMedias { get; set; } = new List<DiaryMedia>();
    }
}