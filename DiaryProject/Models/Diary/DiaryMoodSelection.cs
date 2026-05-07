namespace DiaryProject.Models.Diary
{
    public class DiaryMoodSelection
    {
        public long DiaryId { get; set; }

        public string MoodId { get; set; } = "";

        public Diary Diary { get; set; } = null!;

        public Mood Mood { get; set; } = null!;
    }
}