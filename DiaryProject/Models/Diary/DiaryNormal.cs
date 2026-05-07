namespace DiaryProject.Models.Diary
{
    public class DiaryNormal
    {
        public long DiaryId { get; set; }

        public string? Title { get; set; }

        public string? Body { get; set; }

        public Diary Diary { get; set; } = null!;
    }
}